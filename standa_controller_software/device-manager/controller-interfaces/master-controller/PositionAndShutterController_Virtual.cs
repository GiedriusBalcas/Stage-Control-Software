using standa_controller_software.command_manager;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public class PositionAndShutterController_Virtual : BaseController
    {
        
        public PositionAndShutterController_Virtual(string name) : base(name)
        {
            _methodMap[CommandDefinitionsLibrary.MoveAbsolute] = new MethodInformation()
            {
                MethodHandle = MoveAbsolute,
                Quable = true,
                State = MethodState.Free,
            };
            _methodMap[CommandDefinitionsLibrary.UpdateMoveSettings] = new MethodInformation()
            {
                MethodHandle = UpdateMoveSettings,
                Quable = true,
                State = MethodState.Free,
            };
            _methodMap[CommandDefinitionsLibrary.WaitUntilStop] = new MethodInformation()
            {
                MethodHandle = WaitUntilStop,
                Quable = true,
                State = MethodState.Free,
            };


            //_methodMap["UpdateMoveSettings"] = UpdateMoveSettings;

            //_methodMap["WaitUntilStop"] = WaitUntilStop;
        }

        private async Task UpdateMoveSettings(Command command, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            var targetControllerName = command.TargetController;
            if (SlaveControllers.TryGetValue(targetControllerName, out BaseController positionerController))
            {
                await positionerController.ExecuteCommandAsync(command, slim, log);
            }
        }

        private async Task WaitUntilStop(Command command, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            var targetControllerName = command.TargetController;
            if (SlaveControllers.TryGetValue(targetControllerName, out BaseController positionerController))
            {
                await positionerController.ExecuteCommandAsync(command, slim, log);
            }
        }

        private async Task MoveAbsolute(Command command, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            var targetControllerName = command.TargetController;
            if (SlaveControllers.TryGetValue(targetControllerName, out BaseController positionerController))
            {
                await positionerController.ExecuteCommandAsync(command, slim, log);
            }
                // add_sync_in_action( Position, Time )

        }

        public override void AddDevice(BaseDevice device)
        {
            throw new NotImplementedException();
        }
        public override void AddSlaveController(BaseController controller)
        {
            if(controller is ShutterController_Virtual shutterController)
            {
                SlaveControllers.Add(shutterController.Name, shutterController);
            }
            else if (controller is BasePositionerController positionerController)
            {
                SlaveControllers.Add(positionerController.Name, positionerController);
                //positionerController.OnSyncOut += OnSyncOutReveived;
            }
        }

        private void OnSyncOutReveived(string deviceName)
        {
            
        }

        public override Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
        {
            throw new NotImplementedException();
        }

        public override async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {

            List<BaseDevice> devices = new List<BaseDevice>();

            foreach (var deviceName in command.TargetDevices)
            {
                Dictionary<char, BaseDevice> slaveDevices = new Dictionary<char, BaseDevice>();
                foreach(var slaveController in SlaveControllers)
                {
                    slaveController.Value.GetDevices().ForEach(slaveDevice => slaveDevices.Add(slaveDevice.Name, slaveDevice));
                }

                if (slaveDevices.TryGetValue(deviceName, out BaseDevice device))
                {
                    devices.Add(device);
                }
                else
                {
                    // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Device {deviceName} not found in controller {command.TargetController}");
                }
            }

            if (_methodMap.TryGetValue(command.Action, out var method))
            {
                if (command.Await)
                    await method.MethodHandle(command, semaphore, log);
                else
                    _ = method.MethodHandle(command, semaphore, log);
            }
            else
            {
                throw new InvalidOperationException("Invalid action");
            }
        }

        public override BaseController GetCopy()
        {
            var controllerCopy = new PositionAndShutterController_Virtual(this.Name);
            foreach (var slaveController in SlaveControllers)
            {
                controllerCopy.AddSlaveController(slaveController.Value.GetCopy());
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

    }
}
