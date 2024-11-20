using standa_controller_software.command_manager;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.controller_interfaces.sync;
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

        public override Task AwaitQueuedItems(SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        public override Task ForceStop()
        {
            return Task.CompletedTask;

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
            else if (controller is BaseSyncController syncController)
            {
                SlaveControllers.Add(syncController.Name, syncController);
                SlaveControllersLocks.Add(syncController.Name, controllerLock);
            }
        }
        protected override Task Stop(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
    }
}
