using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.controller_interfaces.sync;
using standa_controller_software.device_manager.devices;
using System.Collections.Concurrent;
using System.Threading;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public partial class PositionAndShutterController_Sim : BaseMasterController
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


        private SyncController_Sim _syncController;

        private Queue<MovementInformation> _buffer;
        private bool _launchPending = true;
        private bool _updateLaunchPending;
        private TaskCompletionSource<bool> _processingCompletionSource;
        private TaskCompletionSource<bool> _processingLastItemTakenSource;


        public PositionAndShutterController_Sim(string name, ConcurrentQueue<string> log) : base(name, log)
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


        private void GotMessageFromSyncExecuter(string Message)
        {
            if (Message == "0x01") // Arduino signaled buffer space is available
            {
                SendCommandIfAvailable().GetAwaiter().GetResult();
            }
            else if (Message == "0x02") // Arduino signaled execution end
            {
                _processingCompletionSource.TrySetResult(true); 
                _processingLastItemTakenSource.TrySetResult(true);

                _log?.Enqueue("Sync controller signaled execution completed");
            }
            else if (Message == "0x03") // Arduino signaled buffer is empty
            {
                _processingLastItemTakenSource.TrySetResult(true);

                _log?.Enqueue("Sync controller signaled las item taken");
            }
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

        private async Task ProcessQueue(SemaphoreSlim semaphore)
        {
            
            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: asked to process queue");

            if(_processingCompletionSource is not null && !_processingCompletionSource.Task.IsCompleted)
            {
                await _processingCompletionSource.Task;
            }

            _processingCompletionSource = new TaskCompletionSource<bool>();
            _processingLastItemTakenSource = new TaskCompletionSource<bool>();

            // Send command to device to start processing
            _log.Enqueue($"master: process queue allowed");

            await FillControllerBuffers();

            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: send to pico to start execution");

            var syncControllerLock = await GatherSemaphoresForController([_syncController.Name]);
            try
            {
                await _syncController.ExecuteQueue(syncControllerLock[_syncController.Name]);
            }
            finally
            {
                ReleaseSemeaphores(syncControllerLock);
            }

            // Set the flag to indicate processing has started
            _launchPending = true;

        }

        private async Task FillControllerBuffers()
        {
            int minFreeItemCount = GetMinFreeBufferItemCount();
            int bufferCount = _buffer.Count;

            for (int i = 0; i < Math.Min(minFreeItemCount, bufferCount); i++)
            {
                var movementInformation = _buffer.Dequeue();
                var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                var execInfo = movementInformation.ExecutionInformation;

                    await SendBufferItemToControllers(PosInfoControllerGroups, execInfo);
            }

            _log.Enqueue("master: Filled controllers");

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


        private int GetMinFreeBufferItemCount()
        {
            int minFreeItemCount = int.MaxValue;
            foreach (var (controllerName, controller) in SlaveControllers)
            {
                if (controller is PositionerController_Sim positionerController)
                {
                    minFreeItemCount = Math.Min(minFreeItemCount, positionerController.CheckBufferFreeSpace());
                }
            }
            _syncController.CheckFreeItemSpace();
            minFreeItemCount = Math.Min(minFreeItemCount, _syncController.CheckFreeItemSpace());
            return minFreeItemCount;
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

            Dictionary<string, SemaphoreSlim> allNeededSemaphors = slaveSemaphors;
            


             isUpdateNeeded = true;
            if (isUpdateNeeded)
            {

                foreach (var (controllerName, slaveSemaphore) in SlaveControllersLocks)
                {
                    if (!slaveSemaphors.ContainsKey(controllerName))
                    {
                        allNeededSemaphors[controllerName] = SlaveControllersLocks[controllerName];
                        await allNeededSemaphors[controllerName].WaitAsync();
                    }
                }

                _launchPending = true;
                bool _needToLaunch = false;


                foreach (var (controllerName, slaveSemaphore) in allNeededSemaphors)
                {
                    slaveSemaphore.Release();
                }

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

                foreach (var (controllerName, slaveSemaphore) in allNeededSemaphors)
                {
                    await slaveSemaphore.WaitAsync();
                }

                // fill slaves from master buffer.
                if (_buffer.Count > 0)
                {
                    //await Task.Delay(100);
                    await FillControllerBuffers(allNeededSemaphors, log);
                    
                    _log.Enqueue("master: filled slaves to the brim");

                    _processingCompletionSource = new TaskCompletionSource<bool>();
                    _processingLastItemTakenSource = new TaskCompletionSource<bool>();

                    _ = _syncController.ExecuteQueue(_log);

                    _log.Enqueue("master: sent Sync Controller to execute its buffer");

                }


                foreach (var (controllerName, slaveSemaphore) in allNeededSemaphors)
                {
                    slaveSemaphore.Release();
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


                foreach (var (controllerName, slaveSemaphore) in allNeededSemaphors)
                {
                    await slaveSemaphore.WaitAsync();
                }

                // update params
                foreach (Command command in commands)
                {

                    if (SlaveControllers.TryGetValue(command.TargetController, out var slaveController) && allNeededSemaphors.ContainsKey(slaveController.Name))
                    {

                        await slaveController.ExecuteCommandAsync(command, allNeededSemaphors[command.TargetController], log);
                    }
                    else
                        throw new Exception($"Slave controller {command.TargetController} was not found.");
                    _log.Enqueue("master: updated movement settings");

                }
            }
            else
            {
                _log.Enqueue("master: did not updated movement settings");

                foreach (var (controllerName, slaveSemaphore) in allNeededSemaphors)
                {
                    slaveSemaphore.Release();
                }
            }

        }



        private Task MoveAbsolute(Command[] commands, SemaphoreSlim semaphore)
        {
            var commandParametersFromFirstCommand = commands.FirstOrDefault().Parameters as MoveAbsoluteParameters ?? throw new Exception("Unable to retrive MoveAbsolute parameters.");

            bool isLeadInUsed = commandParametersFromFirstCommand.IsLeadInUsed;
            bool isLeadOutUsed = commandParametersFromFirstCommand.IsLeadOutUsed;



            Dictionary<string, PositionerInfo> posInfoGroups = new Dictionary<string, PositionerInfo>();
            List<MoveAbsoluteParameters> moveAbsoluteParameterList = new List<MoveAbsoluteParameters>();
            float rethrow = 0f;
            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                var commandParameters = command.Parameters as MoveAbsoluteParameters ?? throw new Exception("Unable to retrive MoveAbsolute parameters.");
                
                moveAbsoluteParameterList.Add(commandParameters);
                
                posInfoGroups[command.TargetController] = new PositionerInfo
                {
                    Devices = command.TargetDevices,
                    TargetPositions = command.TargetDevices.Select(deviceName => commandParameters.PositionerInfo[deviceName].TargetPosition).ToArray(),
                    AllocatedTimes = command.TargetDevices.Select(deviceName => commandParameters.AllocatedTime).ToArray(),
                };
            }
            //}
            var rethrow_ms = moveAbsoluteParameterList.Select(moveParam => moveParam.WaitUntilTime *1000f).Max();
            var maxAllocatedTime = posInfoGroups.Select(info => info.Value.AllocatedTimes.Max()).Max()*1000;
            bool isShutterUsed = moveAbsoluteParameterList.Any(moveParam => moveParam.IsShutterUsed);

            var shutter_on_delays = moveAbsoluteParameterList.Select(moveParam => moveParam.ShutterInfo.DelayOn).Where(n => !float.IsNaN(n));
            float shutter_on_delay_ms = shutter_on_delays.Any() ? shutter_on_delays.Max() : float.NaN;

            var shutter_off_delays = moveAbsoluteParameterList.Select(moveParam => moveParam.ShutterInfo.DelayOff).Where(n => !float.IsNaN(n));
            float shutter_off_delay_ms = shutter_off_delays.Any() ? Math.Max(maxAllocatedTime - shutter_off_delays.Min(), shutter_on_delay_ms) : float.NaN;

            //bool isShutterUsed = commands.Any( cmd => cmd.Parameters. );

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
        
        public override void AddSlaveController(BaseController controller, SemaphoreSlim controllerLock)
        {
            if (controller is ShutterController_Sim shutterController)
            {
                SlaveControllers.Add(shutterController.Name, shutterController);
                SlaveControllersLocks.Add(shutterController.Name, controllerLock);
                if (_syncController is not null)
                {
                    _syncController._shutterChangeState = (bool wantedState) =>
                    {
                        //_ = shutterController.ChangeStatePublic(wantedState);
                        var device = shutterController.GetDevices().FirstOrDefault();
                        if(device is BaseShutterDevice shutterDevice)
                        {
                            shutterDevice.IsOn = wantedState;
                        }
                    };
                }
            }
            else if (controller is PositionerController_Sim positionerController)
            {
                SlaveControllers.Add(positionerController.Name, positionerController);
                SlaveControllersLocks.Add(positionerController.Name, controllerLock);

                foreach (var device in positionerController.GetDevices())
                {
                    char deviceName = device.Name;
                    if(_syncController is not null)
                    {
                        _syncController._positionerSyncInMap[deviceName] = () => positionerController.InvokeSynIn(deviceName);
                        positionerController.OnSyncOut += _syncController.GotSyncOut;
                    }
                }
            }
            else if(controller is SyncController_Sim syncController)
            {

                SlaveControllers.Add(syncController.Name, syncController);
                SlaveControllersLocks.Add(syncController.Name, controllerLock);
                _syncController = syncController;
                _syncController.SendMessage += GotMessageFromSyncExecuter;

                foreach(var (slaveControllerName, slaveController) in SlaveControllers)
                {
                    if(slaveController is PositionerController_Sim slavePositionerController)
                    {
                        foreach (var device in slavePositionerController.GetDevices())
                        {
                            char deviceName = device.Name;
                            if (_syncController is not null)
                            {
                                _syncController._positionerSyncInMap[deviceName] = () => slavePositionerController.InvokeSynIn(deviceName);
                                slavePositionerController.OnSyncOut += _syncController.GotSyncOut;
                            }
                        }
                    }

                    if(slaveController is ShutterController_Sim slaveShutterController)
                    {
                        _syncController._shutterChangeState = (bool wantedState) =>
                        {
                            var device = slaveShutterController.GetDevices().FirstOrDefault();
                            if (device is BaseShutterDevice shutterDevice)
                            {
                                shutterDevice.IsOn = wantedState;
                            }
                        };
                    }
                }
            }
        }
        public override Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
        {
            throw new NotImplementedException();
        }
        public override async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            throw new Exception("Master controller shouldnt call this method, sir.");
        }
        public override BaseController GetCopy()
        {
            var controllerCopy = new PositionAndShutterController_Virtual(this.Name);
            foreach (var slaveController in SlaveControllers)
            {
                controllerCopy.AddSlaveController(slaveController.Value.GetCopy(), SlaveControllersLocks[slaveController.Key]);
            }

            return controllerCopy;
        }
        public override List<BaseDevice> GetDevices()
        {
            return new List<BaseDevice>();
        }
        public override Task UpdateStatesAsync(ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }
        public override async Task AwaitQueuedItems(SemaphoreSlim semaphore)
        {

            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: await queued items encountered.");
            if (_processingLastItemTakenSource != null && !_processingLastItemTakenSource.Task.IsCompleted)
            {
                await _processingCompletionSource.Task;
            }
            if (_processingLastItemTakenSource == null || _processingLastItemTakenSource.Task.IsCompleted)
            {
                await ProcessQueue(semaphore);
                await _processingCompletionSource.Task;
            }


            // Await the TaskCompletionSource's Task without blocking the thread


        }

        public override Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            _buffer.Clear();
            _syncController.Stop();
            _launchPending = true;
            _processingCompletionSource?.TrySetResult(true);
            _processingLastItemTakenSource?.TrySetResult(true);

            return Task.CompletedTask;
        }
    }
}
