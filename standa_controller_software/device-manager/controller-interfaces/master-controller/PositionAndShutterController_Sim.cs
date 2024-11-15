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
    public partial class PositionAndShutterController_Sim : BaseMasterSyncController
    {
        
        private SyncController_Sim _syncController;

        public PositionAndShutterController_Sim(string name, ConcurrentQueue<string> log) : base(name, log)
        {
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
                        _syncController._positionerSyncInMap[deviceName] = () => positionerController.InvokeSyncIn(deviceName);
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
                                _syncController._positionerSyncInMap[deviceName] = () => slavePositionerController.InvokeSyncIn(deviceName);
                                slavePositionerController.OnSyncOut += _syncController.GotSyncOut;
                            }
                        }
                    }

                    if(slaveController is ShutterController_Sim slaveShutterController)
                    {
                        _syncController!._shutterChangeState = (bool wantedState) =>
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
       
        public override BaseController GetVirtualCopy()
        {
            var controllerCopy = new PositionAndShutterController_Virtual(this.Name, _log);
            foreach (var slaveController in SlaveControllers)
            {
                controllerCopy.AddSlaveController(slaveController.Value.GetVirtualCopy(), SlaveControllersLocks[slaveController.Key]);
            }

            return controllerCopy;
        }
        
        protected override async Task Stop(Command command, SemaphoreSlim semaphore)
        {
            _buffer.Clear();
            
            var stopCommand = new Command
            {
                TargetController = _syncController.Name,
                Action = CommandDefinitions.Stop,
                Await = true,
            };

            await ExecuteSlaveCommand(stopCommand);

            _launchPending = true;
            _processingCompletionSource?.TrySetResult(true);
            _processingLastItemTakenSource?.TrySetResult(true);

            }
    }
}
