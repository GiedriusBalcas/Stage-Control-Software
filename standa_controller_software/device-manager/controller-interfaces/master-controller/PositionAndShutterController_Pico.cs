using OpenTK.Graphics.ES11;
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
    public partial class PositionAndShutterController_Pico : BaseMasterController
    {
        private struct PositionerInfo
        {
            public char[] Devices;
            public float[] TargetPositions;
            public float[] AllocatedTimes;
            public float[] Velocities;
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


        private SyncController_Pico _syncController;

        private Queue<MovementInformation> _buffer;
        private bool _launchPending = true;
        private bool _needToLaunch = false;
        private TaskCompletionSource<bool> _processingCompletionSource;
        private TaskCompletionSource<bool> _processingLastItemTakenSource;


        public PositionAndShutterController_Pico(string name) : base(name)
        {
            _methodMap_multiControntroller[CommandDefinitions.ChangeShutterState] = new MultiControllerMethodInformation()
            {
                MethodHandle = ChangeState,
                Quable = true,
                State = MethodState.Free,
            };
            _methodMap_multiControntroller[CommandDefinitions.MoveAbsolute] = new MultiControllerMethodInformation()
            {
                MethodHandle = MoveAbsolute,
                Quable = true,
                State = MethodState.Free,
            };
            _methodMap_multiControntroller[CommandDefinitions.UpdateMoveSettings] = new MultiControllerMethodInformation()
            {
                MethodHandle = UpdateMoveSettings,
                Quable = true,
                State = MethodState.Free,
            };

            _buffer = new Queue<MovementInformation>();
            IsQuable = true;
        }

        private void OnSyncControllerExecutionEnd()
        {
            _processingCompletionSource.TrySetResult(true);
            _processingLastItemTakenSource.TrySetResult(true);

            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: Sync controller signaled that execution finalized.");
        }
        private void OnSyncControllerLastBufferItemTaken()
        {
            if(_processingLastItemTakenSource is not null)
                _processingLastItemTakenSource.TrySetResult(true);

            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: Sync controller signaled that last item was taken.");
        }
        private async Task OnSyncControllerBufferSpaceAvailable()
        {
           _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: Sync controller signaled buffer has free slot");

            if (_buffer.Count > 0)
            {
                var movementInformation = _buffer.Dequeue();
                var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                var execInfo = movementInformation.ExecutionInformation;

                foreach (var (slaveControllerName, semaphoreOfSlaveController) in SlaveControllersLocks)
                {
                    await semaphoreOfSlaveController.WaitAsync();
                }

                await SendBufferItemToControllers(PosInfoControllerGroups, execInfo, SlaveControllersLocks, _log);

                foreach (var (controllerName, slaveSemaphore) in SlaveControllersLocks)
                {
                    if (slaveSemaphore.CurrentCount == 0)
                        slaveSemaphore.Release();
                }
            }

        }

        private async Task ProcessQueue(Dictionary<string, SemaphoreSlim> semaphores, ConcurrentQueue<string> log)
        {
            log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: asked to process queue");

            _processingCompletionSource = new TaskCompletionSource<bool>();
            _processingLastItemTakenSource = new TaskCompletionSource<bool>();

            // Send command to device to start processing
            log.Enqueue($"master: process queue allowed");
            
            await FillControllerBuffers(semaphores, log);


            log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: send to pico to start execution");

            await _syncController.StartExecution();


            foreach (var (controllerName, slaveSemaphore) in SlaveControllersLocks)
            {
                if (slaveSemaphore.CurrentCount == 0)
                    slaveSemaphore.Release();
            }

            // Set the flag to indicate processing has started

            _launchPending = true;

        }

        private async Task FillControllerBuffers(Dictionary<string, SemaphoreSlim> semaphores, ConcurrentQueue<string> log)
        {
            log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: trying to fill slave buffers");


            int minFreeItemCount = await GetMinFreeBufferItemCount();



            int bufferCount = _buffer.Count;

            for (int i = 0; i < Math.Min(minFreeItemCount - 2, bufferCount); i++)
            {
                var movementInformation = _buffer.Dequeue();
                var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                var execInfo = movementInformation.ExecutionInformation;

                await SendBufferItemToControllers(PosInfoControllerGroups, execInfo, semaphores, log);
            }
        }

        private async Task SendBufferItemToControllers(Dictionary<string, PositionerInfo>? PosInfoControllerGroups, ExecutionInformation execInfo, Dictionary<string, SemaphoreSlim> semaphores, ConcurrentQueue<string> log)
        {
            var kaka = 0;

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
                        Time = posInfoList.AllocatedTimes[index],
                        Velocity = posInfoList.Velocities[index]
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

                var controller = SlaveControllers[controllerName];
                var semaphore = semaphores[controllerName];
                await controller.ExecuteCommandAsync(command, semaphore, log);
            }

            // Sending the sync_execution_info

            await _syncController.AddBufferItem(
                execInfo.Devices,
                execInfo.Launch,
                execInfo.Rethrow,
                execInfo.Shutter,
                execInfo.Shutter_delay_on,
                execInfo.Shutter_delay_off);
            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: sent an item to the buffer");

        }


        private async Task<int> GetMinFreeBufferItemCount()
        {
            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: trying to get min free buffer item count");

            int minFreeItemCount = int.MaxValue;
            foreach (var (controllerName, controller) in SlaveControllers)
            {
                if (controller is PositionerController_Sim positionerController)
                {
                    var currentBufferSpace = positionerController.CheckBufferFreeSpace();
                    _log.Enqueue($"master: Minimum buffer space in positioner {positionerController.Name}: {currentBufferSpace}");
                    minFreeItemCount = Math.Min(minFreeItemCount, currentBufferSpace);
                }
                else if (controller is PositionerController_XIMC positionerControllerXimc)
                {
                    foreach(var device in positionerControllerXimc.GetDevices())
                    {
                        var currentBufferSpace = positionerControllerXimc.CheckBufferFreeSpace(device.Name);
                        _log.Enqueue($"master: Minimum buffer space in positioner {device.Name}: {currentBufferSpace}");
                        minFreeItemCount = Math.Min(minFreeItemCount, currentBufferSpace);
                    }
                }
            }


            int syncControllerBufferItemCount = await _syncController.GetBufferItemCount();
            _log.Enqueue($"master: Minimum buffer space in sync controller : {syncControllerBufferItemCount}.");

            minFreeItemCount = Math.Min(minFreeItemCount, syncControllerBufferItemCount);
            return minFreeItemCount;
        }

        private Task ChangeState(Command[] commands, SemaphoreSlim semaphore, Dictionary<string, SemaphoreSlim> slaveSemaphors, ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }

        private async Task UpdateMoveSettings(Command[] commands, SemaphoreSlim semaphore, Dictionary<string, SemaphoreSlim> slaveSemaphors, ConcurrentQueue<string> log)
        {


            //-------------------------------------------------------------//



            var isUpdateNeeded = commands.Any(command =>
            {
                if (command.Parameters is UpdateMovementSettingsParameters parameters)
                {
                    return parameters.AccelChangePending;
                }
                else
                    return false;
            });

            isUpdateNeeded = true;
            if (isUpdateNeeded)
            {
                _launchPending = true;
                _needToLaunch = false;



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
                    await FillControllerBuffers(slaveSemaphors, log);
                    _needToLaunch = true;
                    _log.Enqueue("master: filled slaves to the brim");


                }

                //// await exec_end
                //if (_processingCompletionSource is not null)
                //    if (!_processingCompletionSource.Task.IsCompleted)
                //    {
                //        await _processingCompletionSource.Task;
                //        _log.Enqueue("master: awaited the exec_end");
                //    }
                //    else
                //    {
                //        _log.Enqueue("master: exec_end was allready complete.");

                //    }

                // execute sync controller
                if (_needToLaunch)
                {
                    //await SlaveControllersLocks[_syncController.Name].WaitAsync();
                
                    _processingCompletionSource = new TaskCompletionSource<bool>();
                    _processingLastItemTakenSource = new TaskCompletionSource<bool>();
                
                    await _syncController.StartExecution();

                    //if(SlaveControllersLocks[_syncController.Name].CurrentCount == 0)
                    //    SlaveControllersLocks[_syncController.Name].Release();
                    _log.Enqueue("master: sent Sync Controller to execute its buffer");

                }


                foreach (var (controllerName, slaveSemaphore) in SlaveControllersLocks)
                {
                    if (slaveSemaphore.CurrentCount == 0)
                        slaveSemaphore.Release();
                }

                // await last item taken
                if (_processingLastItemTakenSource is not null)
                    if (!_processingLastItemTakenSource.Task.IsCompleted)
                    {
                        await _processingLastItemTakenSource.Task;
                        _log.Enqueue("master: awaited last item taken signal");
                    }

                // TEST await exec_end
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
                //isUpdateNeeded = true;
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

            //-------------------------------------------------------------//

            ////          TEST
            ////// await exec_end
            if (_processingCompletionSource is not null)
                if (!_processingCompletionSource.Task.IsCompleted)
                {
                    await _processingCompletionSource.Task;
                    _log.Enqueue("master: awaited the exec_end");
                }


        }

        private Task MoveAbsolute(Command[] commands, SemaphoreSlim semaphore, Dictionary<string, SemaphoreSlim> slaveSemaphors, ConcurrentQueue<string> log)
        {
            _log.Enqueue("master: moveAbsolute command encountered");

            float rethrow = 0f;
            var commandParametersFromFirstCommand = commands.FirstOrDefault().Parameters as MoveAbsoluteParameters ?? throw new Exception("Unable to retrive MoveAbsolute parameters.");

            bool isLeadInUsed = commandParametersFromFirstCommand.IsLeadInUsed;
            bool isLeadOutUsed = commandParametersFromFirstCommand.IsLeadOutUsed;

            Dictionary<string, PositionerInfo> posInfoGroups = new Dictionary<string, PositionerInfo>();

            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                var commandParameters = command.Parameters as MoveAbsoluteParameters ?? throw new Exception("Unable to retrive MoveAbsolute parameters.");
                var kaka = commandParameters.WaitUntilTime;
                if (kaka is not null)
                    rethrow = (float)kaka;

                posInfoGroups[command.TargetController] = new PositionerInfo
                {
                    Devices = command.TargetDevices,
                    TargetPositions = command.TargetDevices.Select(deviceName => commandParameters.PositionerInfo[deviceName].TargetPosition).ToArray(),
                    AllocatedTimes = command.TargetDevices.Select(deviceName => commandParameters.AllocatedTime).ToArray(),
                    Velocities = command.TargetDevices.Select(deviceName => commandParameters.PositionerInfo[deviceName].TargetSpeed).ToArray(),
                };
            }

            var executionParameters = new ExecutionInformation()
            {
                Devices = commands.SelectMany(comm => comm.TargetDevices).ToArray(),
                Launch = _launchPending,
                //Rethrow = posInfoGroups.Values.SelectMany(info => info.AllocatedTimes).Max()*1000 -5,//,
                Rethrow = rethrow == 0f ? 0 : rethrow * 1000f,
                //Rethrow = posInfoGroups.Values.SelectMany(info => info.AllocatedTimes).Max() * 1000 *0.5f,
                Shutter = commandParametersFromFirstCommand.IsShutterUsed,
                Shutter_delay_on = commandParametersFromFirstCommand.IsShutterUsed ? commandParametersFromFirstCommand.ShutterInfo.DelayOn *1000: 0f,
                Shutter_delay_off = commandParametersFromFirstCommand.IsShutterUsed ? commandParametersFromFirstCommand.ShutterInfo.DelayOff *1000: 0f,
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

        public override void AddDevice(BaseDevice device)
        {
            throw new NotImplementedException();
        }
        public override void AddSlaveController(BaseController controller, SemaphoreSlim controllerLock)
        {
            if (controller is ShutterController_Sim shutterController)
            {
                SlaveControllers.Add(shutterController.Name, shutterController);
                SlaveControllersLocks.Add(shutterController.Name, controllerLock);
            }
            else if (controller is BasePositionerController positionerController)
            {
                SlaveControllers.Add(positionerController.Name, positionerController);
                SlaveControllersLocks.Add(positionerController.Name, controllerLock);
            }
            else if (controller is SyncController_Pico syncController)
            {

                SlaveControllers.Add(syncController.Name, syncController);
                SlaveControllersLocks.Add(syncController.Name, controllerLock);
                _syncController = syncController;
                _syncController.BufferHasFreeSpace += async () => await OnSyncControllerBufferSpaceAvailable();
                _syncController.ExecutionCompleted += () => OnSyncControllerExecutionEnd();
                _syncController.LastBufferItemTaken += () => OnSyncControllerLastBufferItemTaken();

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

        public override async Task AwaitQueuedItems(SemaphoreSlim semaphore, Dictionary<string, SemaphoreSlim> slaveSemaphors, ConcurrentQueue<string> log)
        {
            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: await queued items encountered.");

            if (_processingLastItemTakenSource is not null && !_processingLastItemTakenSource.Task.IsCompleted)
            {
                await _processingCompletionSource.Task;
            }

            if (_buffer.Count > 0)
            {
                foreach (var (slaveControllerName, semaphoreOfSlaveController) in SlaveControllersLocks)
                {
                    if (!slaveSemaphors.ContainsKey(slaveControllerName))
                        await semaphoreOfSlaveController.WaitAsync();
                }

                await ProcessQueue(SlaveControllersLocks, log);

                foreach (var (controllerName, slaveSemaphore) in SlaveControllersLocks)
                {
                    if (slaveSemaphore.CurrentCount == 0)
                        slaveSemaphore.Release();
                }

                await _processingCompletionSource.Task;
            }


        }

        public override async Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: master: stop encountered.");

            _buffer.Clear();
            _launchPending = true;
            _processingCompletionSource?.TrySetResult(true);
            _processingLastItemTakenSource?.TrySetResult(true);
            // lets ask the sync controller to just launch 40 times for now.
            // TODO: do something better here.

            //char[] deviceNames = SlaveControllers.Values
            //    .Where(controller => controller is BasePositionerController)
            //    .SelectMany(controller => controller.GetDevices())
            //    .Select(device => device.Name)
            //    .ToArray();




            //await _syncController.AddBufferItem(
            //deviceNames,
            //true,
            //1, //   [ms]
            //false,
            //0,
            //0);
            //for (int i = 0; i< 40; i++)
            //{
            //    await _syncController.AddBufferItem(
            //    deviceNames,
            //    false,
            //    1, //   [ms]
            //    false,
            //    0,
            //    0);
            //}



            //// Set the flag to indicate processing has started
            //_processingCompletionSource = new TaskCompletionSource<bool>();
            //_processingLastItemTakenSource = new TaskCompletionSource<bool>();

            //await _syncController.StartExecution();

            //foreach (var (controllerName, slaveSemaphore) in SlaveControllersLocks)
            //{
            //    if (slaveSemaphore.CurrentCount == 0)
            //        slaveSemaphore.Release();
            //}
            //await _processingCompletionSource.Task;

            //_processingCompletionSource?.TrySetResult(true);
            //_processingLastItemTakenSource?.TrySetResult(true);



        }
    }
}
