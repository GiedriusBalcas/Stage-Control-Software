using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.command_manager.command_parameter_library.Positioners;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.sync;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public abstract class BaseMasterSyncController : BaseMasterController
    {
        private struct PositionerInfo
        {
            public char[] Devices;
            public float[] TargetPositions;
            public float[] AllocatedTimes;
        }

        public struct ExecutionInformation
        {
            public char[] Devices;
            public bool Launch;
            public float Rethrow;
            public bool Shutter;
            public float Shutter_delay_on;
            public float Shutter_delay_off;
        }

        private struct MovementInformation
        {
            public Dictionary<string, PositionerInfo> PositionerInfoGroups;
            public ExecutionInformation ExecutionInformation;
        }


        private Queue<MovementInformation> _buffer;
        private bool _launchPending = true;
        private bool _updateLaunchPending;
        private TaskCompletionSource<bool> _processingCompletionSource;
        private TaskCompletionSource<bool> _processingLastItemTakenSource;
        public BaseMasterSyncController(string name, ConcurrentQueue<string> log) : base(name, log)
        {
            _multiControllerMethodMap[CommandDefinitions.ChangeShutterState] = new MultiControllerMethodInformation()
            {
                MethodHandle = ChangeState,
                State = MethodState.Free,
            };
            _multiControllerMethodMap[CommandDefinitions.MoveAbsolute] = new MultiControllerMethodInformation()
            {
                MethodHandle = MoveAbsolute,
                State = MethodState.Free,
            };
            _multiControllerMethodMap[CommandDefinitions.UpdateMoveSettings] = new MultiControllerMethodInformation()
            {
                MethodHandle = UpdateMoveSettings,
                State = MethodState.Free,
            };

            _buffer = new Queue<MovementInformation>();
        }

        private async Task SendCommandIfAvailable()
        {
            if (_buffer.Count > 0)
            {
                var movementInformation = _buffer.Dequeue();
                var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                var execInfo = movementInformation.ExecutionInformation;

                await SendBufferItemToControllers(PosInfoControllerGroups, execInfo);
            }

        }

        private async Task SendBufferItemToControllers(Dictionary<string, PositionerInfo>? PosInfoControllerGroups, ExecutionInformation execInfo)
        {
            foreach (var (controllerName, posInfoList) in PosInfoControllerGroups)
            {
                var deviceNamesCurrent = posInfoList.Devices;

                var parameters = new AddSyncInActionParameters
                {
                    MovementInformation = new Dictionary<char, PositionTimePair>()
                };

                foreach (var deviceName in deviceNamesCurrent)
                {
                    var index = Array.IndexOf(deviceNamesCurrent, deviceName);
                    parameters.MovementInformation[deviceName] = new PositionTimePair
                    {
                        Position = posInfoList.TargetPositions[index],
                        Time = posInfoList.AllocatedTimes[index]
                    };
                }

                var command = new Command()
                {
                    Action = CommandDefinitions.AddSyncInAction,
                    TargetController = controllerName,
                    TargetDevices = posInfoList.Devices,
                    Parameters = parameters,
                    Await = true,
                };


                var slavePositionerSemaphore = await GatherSemaphoresForController([controllerName]);
                try
                {
                    await SlaveControllers[controllerName].ExecuteCommandAsync(command, slavePositionerSemaphore[controllerName]);
                }
                finally
                {
                    ReleaseSemeaphores(slavePositionerSemaphore);
                }
            }

            // Sending the sync_execution_info
            var slaveSyncSemaphore = await GatherSemaphoresForController([_syncController.Name]);
            try
            {
                _syncController.AddBufferItem(
                    execInfo.Devices,
                    execInfo.Launch,
                    execInfo.Rethrow,
                    execInfo.Shutter,
                    execInfo.Shutter_delay_on,
                    execInfo.Shutter_delay_off);
            }
            finally
            {
                ReleaseSemeaphores(slaveSyncSemaphore);
            }
        }

        private async Task<int> GetMinFreeBufferItemCount()
        {
            int minFreeItemCount = int.MaxValue;
            foreach (var (controllerName, controller) in SlaveControllers)
            {
                if (controller is BasePositionerController positionerController && positionerController is IQuableController quableController)
                {
                    var command = new Command
                    {
                        Action = CommandDefinitions.GetBufferCount,
                        Await = true,
                        Parameters = new GetBufferCountParameters
                        {
                            Devices = positionerController.GetDevices().Select(device => device.Name).ToArray(),
                        },
                        TargetController = positionerController.Name,
                        TargetDevices = positionerController.GetDevices().Select(device => device.Name).ToArray(),
                    };

                    var semaphore = await GatherSemaphoresForController([controllerName]);
                    try
                    {
                        var bufferSpace = await positionerController.ExecuteCommandAsync<uint>(command, semaphore[controllerName]);
                    }
                    finally
                    {
                        ReleaseSemeaphores(semaphore);
                    }

                    minFreeItemCount = Math.Min(minFreeItemCount, );
                }
            }
            _syncController.CheckFreeItemSpace();
            minFreeItemCount = Math.Min(minFreeItemCount, _syncController.CheckFreeItemSpace());
            return minFreeItemCount;
        }
        private async Task FillControllerBuffers(SemaphoreSlim semaphore)
        {
            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: trying to fill slave buffers");

            int minFreeItemCount = await GetMinFreeBufferItemCount();
            int bufferCount = _buffer.Count;

            for (int i = 0; i < Math.Min(minFreeItemCount - 2, bufferCount); i++)
            {
                var movementInformation = _buffer.Dequeue();
                var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                var execInfo = movementInformation.ExecutionInformation;

                await SendBufferItemToControllers(PosInfoControllerGroups, execInfo);
            }
        }
        private Task ChangeState(Command[] commands, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }

        private async Task UpdateMoveSettings(Command[] commands, SemaphoreSlim semaphore)
        {

            var isUpdateNeeded = commands.Any(command =>
            {
                if (command.Parameters is UpdateMovementSettingsParameters parameters)
                {
                    return parameters.AccelChangePending || !parameters.Blending;
                }
                else
                    return false;
            });

            isUpdateNeeded = true;
            if (isUpdateNeeded)
            {
                _launchPending = true;



                // await exec_end
                if (_processingCompletionSource is not null)
                    if (!_processingCompletionSource.Task.IsCompleted)
                    {
                        await _processingCompletionSource.Task;
                        _log.Enqueue("master: awaited the exec_end");
                    }
                    else
                    {
                        _log.Enqueue("master: exec_end was allready complete.");

                    }

                // fill slaves from master buffer.
                if (_buffer.Count > 0)
                {
                    //await Task.Delay(100);
                    await FillControllerBuffers(semaphore);
                    _log.Enqueue("master: filled slaves to the brim");

                    _processingCompletionSource = new TaskCompletionSource<bool>();
                    _processingLastItemTakenSource = new TaskCompletionSource<bool>();

                    await StartExecutionOnSyncController();

                    _log.Enqueue("master: sent Sync Controller to execute its buffer");

                }


                // await exec_end, we can't actually update the move settings during movement of last item.
                if (_processingCompletionSource is not null)
                    if (!_processingCompletionSource.Task.IsCompleted)
                    {
                        await _processingCompletionSource.Task;
                        _log.Enqueue("master: awaited the exec_end");
                    }
                    else
                    {
                        _log.Enqueue("master: exec_end was allready complete.");

                    }

                // update params
                foreach (Command command in commands)
                {

                    if (SlaveControllers.TryGetValue(command.TargetController, out var slaveController))
                    {


                        await SlaveControllersLocks[slaveController.Name].WaitAsync();


                        await slaveController.ExecuteCommandAsync(command, slaveSemaphors[command.TargetController], log);

                        if (SlaveControllersLocks[slaveController.Name].CurrentCount == 0)
                            SlaveControllersLocks[slaveController.Name].Release();
                    }
                    else
                        throw new Exception($"Slave controller {command.TargetController} was not found.");
                    _log.Enqueue("master: updated movement settings");

                }
            }
            else
            {
                _log.Enqueue("master: did not updated movement settings");

                foreach (var (controllerName, slaveSemaphore) in SlaveControllersLocks)
                {
                    if (slaveSemaphore.CurrentCount == 0)
                        slaveSemaphore.Release();
                }
            }

        }

        private async Task StartExecutionOnSyncController()
        {
            var syncController = SlaveControllers.Values.FirstOrDefault(controller =>
                {
                    return controller is BaseSyncController syncController;
                });
            if (syncController == null)
                throw new Exception("Unable to retrieve sync controller in master controller.");

            var syncControllerLock = await GatherSemaphoresForController([syncController.Name]);
            var command = new Command
            {
                Action = CommandDefinitions.StartQueueExecution,
                Await = true,
                TargetController = syncController.Name,
            };

            try
            {
                await syncController.ExecuteCommandAsync(command, syncControllerLock[syncController.Name]);
            }
            finally
            {
                ReleaseSemeaphores(syncControllerLock);
            }
        }
    }
}
