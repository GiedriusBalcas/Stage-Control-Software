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
                    //AllocatedTimes = command.TargetDevices.Select(deviceName => (commandParameters.PositionerInfo[deviceName].MovementInformation.TotalTime + commandParameters.PositionerInfo[deviceName].MovementInformation.ConstantSpeedEndTime) / 2 ).ToArray(),
                    AllocatedTimes = command.TargetDevices.Select(deviceName => 
                    CalculateAlocatedTime(
                        commandParameters.PositionerInfo[deviceName].MovementInformation.TotalTime, 
                        commandParameters.PositionerInfo[deviceName].MovementInformation.ConstantSpeedEndTime,
                        commandParameters.PositionerInfo[deviceName].MovementInformation.ConstantSpeedStartTime) 
                    ).ToArray(),
                };
            }

            var rethrow_ms = moveAbsoluteParameterList.Select(moveParam => moveParam.WaitUntilTime * 1000f).Max();
            var maxTotalMovementTime_ms = moveAbsoluteParameterList.Max(cmd => cmd.PositionerInfo.Values.Max(posInfo => posInfo.MovementInformation.TotalTime)) * 1000f;
                
            bool isShutterUsed = moveAbsoluteParameterList.Any(moveParam => moveParam.IsShutterUsed);

            var shutter_on_delays = moveAbsoluteParameterList.Select(moveParam => moveParam.ShutterInfo.DelayOn).Where(n => !float.IsNaN(n));
            float shutter_on_delay_ms = shutter_on_delays.Any() ? shutter_on_delays.Max() : float.NaN;

            var shutter_off_delays = moveAbsoluteParameterList.Select(moveParam => moveParam.ShutterInfo.DelayOff).Where(n => !float.IsNaN(n));
            float shutter_off_delay_ms = shutter_off_delays.Any() ? Math.Max(maxTotalMovementTime_ms - shutter_off_delays.Min(), float.IsNaN(shutter_on_delay_ms) ? 0f : shutter_on_delay_ms) : float.NaN;

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

        protected virtual float CalculateAlocatedTime(float totalTime, float constSpeedEndTime, float constSpeedStartTime)
        {
            return totalTime;
        }
        protected virtual async Task UpdateMoveSettings(Command[] commands, SemaphoreSlim semaphore)
        {
            //TODO: store the update commands until the second one or awaitQueuedItems is hit.
            _logger.LogInformation("update move settings encountered.");

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
                _logger.LogInformation("update move settings. update is needed.");

                await ProcessQueue(semaphore);
                _logger.LogInformation("update move settings. awaited ProcessQueue().");

                if (_updateMoveSettingsCommands != null)
                    _logger.LogError("Last move settings update missed.");

                _updateMoveSettingsCommands = commands;
                _launchPending = true;
                _logger.LogInformation("update move settings. updated next move settings update command.");
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
                TargetDevices = execInfo.Devices
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
            _logger.LogInformation($"trying to fill slave buffers");

            int minFreeItemCount = await GetMinFreeBufferItemCount();
            if(minFreeItemCount < 2)
                _logger.LogInformation($"available buffer space is slaves is less than 2.");


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
                    throw new Exception("Was Unable to dequeue a command from the buffer");
                }
            }

        }
        protected async Task ProcessQueue(SemaphoreSlim semaphore)
        {
            _logger.LogInformation("process queue encountered.");

            await AwaitExecutionEnd();
            _logger.LogInformation("process queue. awaited execution end.");


            if (_updateMoveSettingsCommands != null)
            {
                _logger.LogInformation("process queue. theres pending move settings update.");


                foreach (Command command in _updateMoveSettingsCommands)
                {
                    // first, let's await until the device is stationary unless its a blended movement.
                    _logger.LogInformation("process queue. waiting for positioners to stop moving.");

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
                            Parameters = targetDevices,
                        };

                        await ExecuteSlaveCommand(waitUntilStopCommand);
                    }
                    _logger.LogInformation("process queue. awaited for positioners to stop moving.");

                    _logger.LogInformation("process queue. executing move settings update command for slaves.");

                    await ExecuteSlaveCommand(command);
                }
                _logger.LogInformation("process queue. done updating.");

                _updateMoveSettingsCommands = null;
            }
            //await AwaitExecutionEnd();

            await FillControllerBuffers(semaphore);
            _logger.LogInformation("process queue. filled slave controllers with buffer items.");


            await StartExecutionOnSyncController(semaphore);
            _logger.LogInformation("process queue. executed StartExecutionOnSyncController.");


            _launchPending = true;
            await AwaitExecutionEnd();
            _logger.LogInformation("process queue. awaited execution end via AwaitExecutionEnd().");


        }
        protected async Task AwaitExecutionEnd()
        {
            _logger.LogInformation("AwaitExecutionEnd encountered.");

            if (_processingCompletionSource is not null)
            {
                if (!_processingCompletionSource.Task.IsCompleted)
                {
                    _logger.LogInformation("AwaitExecutionEnd. theres ongoing process. Gonna await _processingCompletionSource.Task");

                    await _processingCompletionSource.Task;
                    _logger.LogInformation("AwaitExecutionEnd. awaited _processingCompletionSource.Task");

                }
                else
                {
                    _logger.LogInformation("AwaitExecutionEnd. _processingCompletionSource.Task.IsCompleted was completed.");
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
                TargetDevices = [],
                Parameters = syncController.Name,
            };


            _processingCompletionSource = new TaskCompletionSource<bool>();
            _processingLastItemTakenSource = new TaskCompletionSource<bool>();

            await ExecuteSlaveCommand(command);
        }

    }
}
