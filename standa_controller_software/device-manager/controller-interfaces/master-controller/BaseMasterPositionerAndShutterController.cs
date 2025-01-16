using Microsoft.Extensions.Logging;
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


        //public int NumberOfItemsInQueue { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        //public Action NumberOfItemsInQueueChanged { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
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

        protected ConcurrentQueue<MovementInformation> _buffer;
        protected Command[]? _updateMoveSettingsCommands = null;
        protected bool _launchPending = true;
        protected TaskCompletionSource<bool> _processingCompletionSource;
        protected TaskCompletionSource<bool> _processingLastItemTakenSource;


        public BaseMasterPositionerAndShutterController(string name, ILoggerFactory loggerFactory) : base(name, loggerFactory)
        {
            _loggerFactory.CreateLogger<BaseMasterPositionerAndShutterController>();

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

            _buffer = new ConcurrentQueue<MovementInformation>();
        }

        public override BaseController GetVirtualCopy()
        {
            var virtualCopy = new PositionAndShutterController_Virtual(Name, _loggerFactory)
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
        public override async Task ForceStop()
        {
            _processingCompletionSource?.TrySetResult(true);
            _processingLastItemTakenSource?.TrySetResult(true);
            _buffer = new ConcurrentQueue<MovementInformation>();

            await AwaitExecutionEnd();

            _updateMoveSettingsCommands = null;
            _launchPending = true;
            _processingCompletionSource?.TrySetResult(true);
            _processingLastItemTakenSource?.TrySetResult(true);
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
                    return parameters.AccelChangePending;
                }
                else
                    return false;
            });

            if (isUpdateNeeded = true)
            {

                await ProcessQueue(semaphore);

                if (_updateMoveSettingsCommands != null)
                    _logger.LogError("Last move settings update missed.");

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
                if(_buffer.TryDequeue(out var movementInformation))
                {
                    var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                    var execInfo = movementInformation.ExecutionInformation;

                    await SendBufferItemToControllers(PosInfoControllerGroups, execInfo);
                }
                else
                {
                    throw new Exception("master: Was Unable to dequeue a command from the buffer");
                }
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
                if (controller is BasePositionerController)
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
                else if(controller is BaseSyncController)
                {
                    var command = new Command
                    {
                        Action = CommandDefinitions.GetBufferCount,
                        Await = true,
                        Parameters = new GetBufferCountParameters
                        {
                            Device = '\0',
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

            if (minFreeItemCount == int.MaxValue)
                return 0;
            else
                return minFreeItemCount;
        }
        protected async Task FillControllerBuffers(SemaphoreSlim semaphore)
        {
            _logger.LogDebug($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: trying to fill slave buffers");

            int minFreeItemCount = await GetMinFreeBufferItemCount();
            if(minFreeItemCount < 2)
                _logger.LogDebug($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: available buffer spave is slaves is less than 2.");


            int bufferCount = _buffer.Count;

            for (int i = 0; i < Math.Min(minFreeItemCount - 2, bufferCount); i++)
            {
                if(_buffer.TryDequeue(out var movementInformation))
                {
                    var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                    var execInfo = movementInformation.ExecutionInformation;

                    await SendBufferItemToControllers(PosInfoControllerGroups, execInfo);
                }
                else
                {
                    throw new Exception("master: Was Unable to dequeue a command from the buffer");
                }
            }

        }
        protected async Task ProcessQueue(SemaphoreSlim semaphore)
        {
            await AwaitExecutionEnd();
            
            if (_updateMoveSettingsCommands != null)
            {
                foreach (Command command in _updateMoveSettingsCommands)
                {
                    // first, let's await until the device is stationary unless its a blended movement.
                    var targetDevices = command.TargetDevices;
                    var targetController = command.TargetController;

                    if (command.Parameters is UpdateMovementSettingsParameters updateParameters && updateParameters.Blending != true)
                    {
                        var waitUntilStopCommand = new Command
                        {
                            Action = CommandDefinitions.WaitForStop,
                            Await = true,
                            TargetController = targetController,
                            TargetDevices = targetDevices,
                        };

                        await ExecuteSlaveCommand(waitUntilStopCommand);
                    }

                    await ExecuteSlaveCommand(command);
                }

                _updateMoveSettingsCommands = null;
            }
            //await AwaitExecutionEnd();

            await FillControllerBuffers(semaphore);
            _logger.LogDebug("master: filled slaves to the brim");

            await StartExecutionOnSyncController(semaphore);
            _logger.LogDebug("master: sent Sync Controller to execute its buffer");

            _launchPending = true;
            await AwaitExecutionEnd();


        }
        protected async Task AwaitExecutionEnd()
        {
            if (_processingCompletionSource is not null)
            {
                if (!_processingCompletionSource.Task.IsCompleted)
                {
                    await _processingCompletionSource.Task;
                    _logger.LogDebug("master: awaited the exec_end");
                }
                else
                {
                    _logger.LogDebug("master: exec_end was allready complete.");

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
