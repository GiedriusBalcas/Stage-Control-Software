using standa_controller_software.device_manager.controllers;
using standa_controller_software.device_manager.devices;
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
        }

        public void AddController(IController controller)
        {
            //check if controllers name is unique
            if (Controllers.ContainsKey(controller.Name))
                throw new Exception($"Exception thrown when trying to add controller with non unique name {controller.Name}");
            
            Controllers.Add(controller.Name, controller);

            ControllerLocks.Add(controller.Name, new SemaphoreSlim(1, 1));
        }

        public TController GetDeviceController<TController>(string deviceName) where TController : IController
        {
            var correctTypeControllers = Controllers.Values.OfType<TController>();

            var selectedController = correctTypeControllers
                .FirstOrDefault(controller => controller.GetDevices().Any(dev => dev.Name == deviceName));

            if (selectedController == null)
            {
                throw new Exception($"No controller of type {typeof(TController).Name} found for device {deviceName}.");
            }

            return (TController)selectedController;
        }

        public TDevice? GetDevice<TDevice>(string name) where TDevice : class, IDevice
        {
            return Controllers.Values
                .SelectMany(controller => controller.GetDevices())
                .OfType<TDevice>() // Filter by the specified device type
                .FirstOrDefault(device => device.Name == name);
        }
    }
}
