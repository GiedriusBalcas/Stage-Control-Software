using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.command_manager.command_parameter_library.Positioners;
using standa_controller_software.command_manager.command_parameter_library.Synchronization;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.sync;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public abstract class BaseMasterPositionerAndShutterController : BaseMasterController, IQuableController
    {
        public struct PositionerSyncItemInfo
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

        protected struct MovementInformation
        {
            public Dictionary<string, PositionerSyncItemInfo> PositionerInfoGroups;
            public ExecutionInformation ExecutionInformation;
        }

        protected Queue<MovementInformation> _buffer;
        protected Command[]? _updateMoveSettingsCommands = null;
        protected bool _launchPending = true;
        protected bool _updateLaunchPending;
        protected TaskCompletionSource<bool> _processingCompletionSource;
        protected TaskCompletionSource<bool> _processingLastItemTakenSource;

        public BaseMasterPositionerAndShutterController(string name, ConcurrentQueue<string> log) : base(name, log)
        {
            _multiControllerMethodMap[CommandDefinitions.ChangeShutterState] = new MultiControllerMethodInformation()
            {
                MethodHandle = ChangeState,
            };
            _multiControllerMethodMap[CommandDefinitions.MoveAbsolute] = new MultiControllerMethodInformation()
            {
                MethodHandle = MoveAbsolute,
            };
            _multiControllerMethodMap[CommandDefinitions.UpdateMoveSettings] = new MultiControllerMethodInformation()
            {
                MethodHandle = UpdateMoveSettings,
            };

            _buffer = new Queue<MovementInformation>();
        }

        public override BaseController GetVirtualCopy()
        {
            var virtualCopy = new PositionAndShutterController_Virtual(Name, _log)
            {
                ID = this.ID,
                MasterController = this.MasterController,
            };

            return virtualCopy;
        }

        public virtual async Task AwaitQueuedItems(SemaphoreSlim semaphore)
        {
            await ProcessQueue(semaphore);
            await AwaitExecutionEnd();
        }

        protected virtual Task MoveAbsolute(Command[] commands, SemaphoreSlim semaphore)
        {
            Dictionary<string, PositionerSyncItemInfo> posInfoGroups = new Dictionary<string, PositionerSyncItemInfo>();
            List<MoveAbsoluteParameters> moveAbsoluteParameterList = new List<MoveAbsoluteParameters>();
            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                var commandParameters = command.Parameters as MoveAbsoluteParameters ?? throw new Exception("Unable to retrive MoveAbsolute parameters.");

                moveAbsoluteParameterList.Add(commandParameters);

                posInfoGroups[command.TargetController] = new PositionerSyncItemInfo
                {
                    Devices = command.TargetDevices,
                    TargetPositions = command.TargetDevices.Select(deviceName => commandParameters.PositionerInfo[deviceName].TargetPosition).ToArray(),
                    AllocatedTimes = command.TargetDevices.Select(deviceName => commandParameters.AllocatedTime).ToArray(),
                };
            }

            var rethrow_ms = moveAbsoluteParameterList.Select(moveParam => moveParam.WaitUntilTime * 1000f).Max();
            var maxAllocatedTime_ms = posInfoGroups.Select(info => info.Value.AllocatedTimes.Max()).Max() * 1000;
            bool isShutterUsed = moveAbsoluteParameterList.Any(moveParam => moveParam.IsShutterUsed);

            var shutter_on_delays = moveAbsoluteParameterList.Select(moveParam => moveParam.ShutterInfo.DelayOn).Where(n => !float.IsNaN(n));
            float shutter_on_delay_ms = shutter_on_delays.Any() ? shutter_on_delays.Max() : float.NaN;

            var shutter_off_delays = moveAbsoluteParameterList.Select(moveParam => moveParam.ShutterInfo.DelayOff).Where(n => !float.IsNaN(n));
            float shutter_off_delay_ms = shutter_off_delays.Any() ? Math.Max(maxAllocatedTime_ms - shutter_off_delays.Min(), float.IsNaN(shutter_on_delay_ms) ? 0f : shutter_on_delay_ms) : float.NaN;

            var executionParameters = new ExecutionInformation()
            {
                Devices = commands.SelectMany(comm => comm.TargetDevices).ToArray(),
                Launch = _launchPending,
                Rethrow = rethrow_ms ?? 0f,
                Shutter = isShutterUsed,
                Shutter_delay_on = shutter_on_delay_ms,
                Shutter_delay_off = shutter_off_delay_ms,
            };

            var moveInfo = new MovementInformation()
            {
                PositionerInfoGroups = posInfoGroups,
                ExecutionInformation = executionParameters
            };
            _buffer.Enqueue(moveInfo);

            _launchPending = false;

            return Task.CompletedTask;
        }
        protected virtual async Task UpdateMoveSettings(Command[] commands, SemaphoreSlim semaphore)
        {
            //TODO: store the update commands until the second one or awaitQueuedItems is hit.

            var isUpdateNeeded = commands.Any(command =>
            {
                if (command.Parameters is UpdateMovementSettingsParameters parameters)
                {
                    return parameters.AccelChangePending || !parameters.Blending;
                }
                else
                    return false;
            });

            if (isUpdateNeeded = true)
            {
                await ProcessQueue(semaphore);

                if (_updateMoveSettingsCommands != null)
                    _log.Enqueue("Last move settings update missed.");

                _updateMoveSettingsCommands = commands;
                _launchPending = true;
            }
        }
        protected virtual Task ChangeState(Command[] commands, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }

        protected async Task SendCommandIfAvailable()
        {
            if (_buffer.Count > 0)
            {
                var movementInformation = _buffer.Dequeue();
                var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                var execInfo = movementInformation.ExecutionInformation;

                await SendBufferItemToControllers(PosInfoControllerGroups, execInfo);
            }

        }
        protected async Task SendBufferItemToControllers(Dictionary<string, PositionerSyncItemInfo>? PosInfoControllerGroups, ExecutionInformation execInfo)
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


                await ExecuteSlaveCommand(command);
            }

            var syncController = SlaveControllers.Values.FirstOrDefault(controller =>
            {
                return controller is BaseSyncController syncController;
            });
            if (syncController == null)
                throw new Exception("Unable to retrieve sync controller in master controller.");

            var commandSync = new Command
            {
                Action = CommandDefinitions.AddSyncControllerBufferItem,
                Await = true,
                Parameters = new AddSyncControllerBufferItemParameters
                {
                    Devices = execInfo.Devices,
                    Launch = execInfo.Launch,
                    Rethrow = execInfo.Rethrow,
                    Shutter = execInfo.Shutter,
                    ShutterDelayOff = execInfo.Shutter_delay_off,
                    ShutterDelayOn = execInfo.Shutter_delay_on,
                },
                TargetController = syncController.Name,
            };

            await ExecuteSlaveCommand(commandSync);
        }
        protected async Task<int> GetMinFreeBufferItemCount()
        {
            // TODO: only check the devices in question
            int minFreeItemCount = int.MaxValue;
            foreach (var (controllerName, controller) in SlaveControllers)
            {
                if (controller is BasePositionerController || controller is BaseSyncController)
                {
                    foreach (var device in controller.GetDevices())
                    {
                        var command = new Command
                        {
                            Action = CommandDefinitions.GetBufferCount,
                            Await = true,
                            Parameters = new GetBufferCountParameters
                            {
                                Device = device.Name,
                            },
                            TargetController = controller.Name,
                            TargetDevices = controller.GetDevices().Select(device => device.Name).ToArray(),
                        };

                        var semaphore = await GatherSemaphoresForController([controllerName]);
                        try
                        {
                            var bufferSpace = await controller.ExecuteCommandAsync<int>(command, semaphore[controllerName]);
                            minFreeItemCount = Math.Min(minFreeItemCount, bufferSpace);
                        }
                        finally
                        {
                            ReleaseSemeaphores(semaphore);
                        }
                    }
                }
            }

            if (minFreeItemCount == int.MaxValue)
                return 0;
            else
                return minFreeItemCount;
        }
        protected async Task FillControllerBuffers(SemaphoreSlim semaphore)
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
        protected async Task ProcessQueue(SemaphoreSlim semaphore)
        {
            await AwaitExecutionEnd();
            
            if (_updateMoveSettingsCommands != null)
            {
                foreach (Command command in _updateMoveSettingsCommands)
                {
                    await ExecuteSlaveCommand(command);
                }

                _updateMoveSettingsCommands = null;
            }

            await FillControllerBuffers(semaphore);
            _log.Enqueue("master: filled slaves to the brim");

            await StartExecutionOnSyncController(semaphore);
            _log.Enqueue("master: sent Sync Controller to execute its buffer");

            _launchPending = true;
            

        }
        protected async Task AwaitExecutionEnd()
        {
            if (_processingCompletionSource is not null)
            {
                if (!_processingCompletionSource.Task.IsCompleted)
                {
                    await _processingCompletionSource.Task;
                    _log.Enqueue("master: awaited the exec_end");
                }
                else
                {
                    _log.Enqueue("master: exec_end was allready complete.");

                }
            }
        }
        protected async Task StartExecutionOnSyncController(SemaphoreSlim semaphore)
        {
            var syncController = SlaveControllers.Values.FirstOrDefault(controller =>
                {
                    return controller is BaseSyncController syncController;
                });
            if (syncController == null)
                throw new Exception("Unable to retrieve sync controller in master controller.");

            var command = new Command
            {
                Action = CommandDefinitions.StartQueueExecution,
                Await = true,
                TargetController = syncController.Name,
            };


            _processingCompletionSource = new TaskCompletionSource<bool>();
            _processingLastItemTakenSource = new TaskCompletionSource<bool>();

            await ExecuteSlaveCommand(command);
        }

    }
}
