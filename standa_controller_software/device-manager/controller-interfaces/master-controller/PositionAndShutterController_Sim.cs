﻿using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.controller_interfaces.sync;
using standa_controller_software.device_manager.devices;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public partial class PositionAndShutterController_Sim : BaseMasterPositionerAndShutterController
    {

        private SyncController_Sim? _syncController;

        public PositionAndShutterController_Sim(string name, ILoggerFactory loggerFactory) : base(name, loggerFactory)
        {
            _logger = _loggerFactory.CreateLogger<PositionAndShutterController_Sim>();
        }

        public override void AddSlaveController(BaseController controller, SemaphoreSlim controllerLock)
        {
            if (controller is ShutterController_Sim shutterController)
            {
                SlaveControllers.Add(shutterController.Name, shutterController);
                SlaveControllersLocks.Add(shutterController.Name, controllerLock);
                if (_syncController is not null)
                {
                    _syncController.ShutterChangeState = (bool wantedState) =>
                    {
                        //_ = shutterController.ChangeStatePublic(wantedState);
                        var device = shutterController.GetDevices().FirstOrDefault();

                        if (device is BaseShutterDevice shutterDevice)
                        {
                            shutterDevice.IsOn = wantedState;
                            shutterController.ChangeStatePublic(shutterDevice.Name, wantedState);
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
                    if (_syncController is not null)
                    {
                        _syncController.PositionerSyncInMap[deviceName] = () => positionerController.InvokeSyncIn(deviceName);
                        positionerController.OnSyncOut += _syncController.GotSyncOut;
                    }
                }
            }
            else if (controller is SyncController_Sim syncController)
            {

                SlaveControllers.Add(syncController.Name, syncController);
                SlaveControllersLocks.Add(syncController.Name, controllerLock);
                _syncController = syncController;
                _syncController.SendMessage += GotMessageFromSyncExecuter;

                foreach (var (slaveControllerName, slaveController) in SlaveControllers)
                {
                    if (slaveController is PositionerController_Sim slavePositionerController)
                    {
                        foreach (var device in slavePositionerController.GetDevices())
                        {
                            char deviceName = device.Name;
                            if (_syncController is not null)
                            {
                                _syncController.PositionerSyncInMap[deviceName] = () => slavePositionerController.InvokeSyncIn(deviceName);
                                slavePositionerController.OnSyncOut += _syncController.GotSyncOut;
                            }
                        }
                    }

                    if (slaveController is ShutterController_Sim slaveShutterController)
                    {
                        _syncController!.ShutterChangeState = (bool wantedState) =>
                        {
                            var device = slaveShutterController.GetDevices().FirstOrDefault();
                            if (device is BaseShutterDevice shutterDevice)
                            {
                                shutterDevice.IsOn = wantedState;
                                slaveShutterController.ChangeStatePublic(shutterDevice.Name, wantedState);
                            }
                        };
                    }
                }
            }
        }
        
        private void GotMessageFromSyncExecuter(string Message)
        {
            if (Message == "0x01") // Arduino signaled buffer space is available
            {
                SendCommandIfAvailable().GetAwaiter().GetResult();
            }
            else if (Message == "0x02") // Arduino signaled execution end
            {
                _processingCompletionSource?.TrySetResult(true);
                _processingLastItemTakenSource?.TrySetResult(true);

                _logger.LogInformation("Sync controller signaled execution completed");
            }
            else if (Message == "0x03") // Arduino signaled buffer is empty
            {
                _processingLastItemTakenSource?.TrySetResult(true);

                _logger.LogInformation("Sync controller signaled las item taken");
            }
        }
        protected override async Task Stop(Command command, SemaphoreSlim semaphore)
        {
            _buffer.Clear();

            if(_syncController is not null)
            {
                var stopCommand = new Command
                {
                    TargetController = _syncController.Name,
                    TargetDevices = _syncController.GetDevices().Select(device => device.Name).ToArray(),
                    Parameters = _syncController.Name,
                    Action = CommandDefinitions.Stop,
                    Await = true,
                };

                await ExecuteSlaveCommand(stopCommand);
            }

            _launchPending = true;
            _processingCompletionSource?.TrySetResult(true);
            _processingLastItemTakenSource?.TrySetResult(true);
        }


        protected override float CalculateAlocatedTime(float totalTime, float constSpeedEndTime, float constSpeedStartTime)
        {
            //(commandParameters.PositionerInfo[deviceName].MovementInformation.TotalTime + commandParameters.PositionerInfo[deviceName].MovementInformation.ConstantSpeedEndTime - commandParameters.PositionerInfo[deviceName].MovementInformation.ConstantSpeedStartTime) / 2

            return (totalTime  + constSpeedEndTime - constSpeedStartTime) / 2;
        }

    }
}
