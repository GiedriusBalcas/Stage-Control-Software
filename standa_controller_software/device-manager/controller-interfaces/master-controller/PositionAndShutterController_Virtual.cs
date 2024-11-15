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
    public class PositionAndShutterController_Virtual : BaseMasterPositionerAndShutterController
    {
        
        public PositionAndShutterController_Virtual(string name, ConcurrentQueue<string> log) : base(name, log)
        {
            
        }

        protected override async Task ChangeState(Command[] commands, SemaphoreSlim semaphore)
        {
            foreach (Command command in commands)
            {
                await ExecuteSlaveCommand(command);
            }
        }
        protected override async Task UpdateMoveSettings(Command[] commands, SemaphoreSlim semaphore)
        {
            foreach (Command command in commands)
            {
                await ExecuteSlaveCommand(command);
            }
        }

        protected override async Task MoveAbsolute(Command[] commands, SemaphoreSlim semaphore)
        {
            foreach(Command command in commands)
            {
                await ExecuteSlaveCommand(command);
            }

        }

        public override void AddSlaveController(BaseController controller, SemaphoreSlim controllerLock)
        {
            if(controller is ShutterController_Virtual shutterController)
            {
                SlaveControllers.Add(shutterController.Name, shutterController);
                SlaveControllersLocks.Add(shutterController.Name, controllerLock);
            }
            else if (controller is BasePositionerController positionerController)
            {
                SlaveControllers.Add(positionerController.Name, positionerController);
                SlaveControllersLocks.Add(positionerController.Name, controllerLock);
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
        
        public virtual Task AwaitQueuedItems(SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }

        protected override Task Stop(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
    }
}
