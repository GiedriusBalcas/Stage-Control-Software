using standa_controller_software.device_manager.controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager
{
    public class ControllerManager
    {
        public Dictionary<string, IController> Controllers { get; private set; } = new Dictionary<string, IController>();
        public Dictionary<string, SemaphoreSlim> ControllerLocks { get; private set; } = new Dictionary<string, SemaphoreSlim>();

        public ControllerManager()
        {
            // Initialize controllers and locks
            var virtualPositionerController = new VirtualPositionerController("VirtualPositionerController#1");

            Controllers.Add(virtualPositionerController.Name, virtualPositionerController);

            ControllerLocks.Add(virtualPositionerController.Name, new SemaphoreSlim(1, 1));
        }
    }
}
