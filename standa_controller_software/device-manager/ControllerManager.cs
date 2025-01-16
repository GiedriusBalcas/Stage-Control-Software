using Microsoft.Extensions.Logging;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
using standa_controller_software.device_manager.devices;
using standa_controller_software.device_manager.devices.shutter;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace standa_controller_software.device_manager
{
    public class ControllerManager
    {
        private ILogger<ControllerManager> _logger;
        private ILoggerFactory _loggerFactory;

        public ToolInformation ToolInformation { get; set; }
        public Dictionary<string, BaseController> Controllers { get; private set; } = new Dictionary<string, BaseController>();
        public Dictionary<string, SemaphoreSlim> ControllerLocks { get; private set; } = new Dictionary<string, SemaphoreSlim>();

        public string Name { get; set; }
        public ControllerManager(ILogger<ControllerManager> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        public async void AddController(BaseController controller)
        {
            // Check if controller's name is unique
            if (Controllers.ContainsKey(controller.Name))
                throw new Exception($"Exception thrown when trying to add controller with non-unique name {controller.Name}");

            Controllers.Add(controller.Name, controller);
            ControllerLocks.Add(controller.Name, new SemaphoreSlim(1, 1));

        }

        public TController GetDeviceController<TController>(char deviceName) where TController : BaseController
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

        public bool TryGetDevice<TDevice>(char name, out TDevice device) where TDevice : BaseDevice
        {
            device = Controllers.Values
                .SelectMany(controller => controller.GetDevices())
                .OfType<TDevice>() // Filter by the specified device type
                .FirstOrDefault(device => device.Name == name);

            return device is not null;
        }

        public bool TryGetDeviceController<TController>(char name, out TController selectedController) where TController : BaseController
        {
            var correctTypeControllers = Controllers.Values.OfType<TController>();

            selectedController = correctTypeControllers
                .FirstOrDefault(controller => controller.GetDevices().Any(dev => dev.Name == name));


            return selectedController != null;
        }

        public List<TDevice> GetDevices<TDevice>() where TDevice : BaseDevice
        {
            return Controllers.Values
                .SelectMany(controller => controller.GetDevices())
                .OfType<TDevice>()
                .ToList(); // Filter by the specified device type 
        }

        public ControllerManager CreateAVirtualCopy()
        {
            var controllerManager_copy = new ControllerManager(_loggerFactory.CreateLogger<ControllerManager>(), _loggerFactory);

            foreach (var controllerEntry in Controllers)
            {
                var originalController = controllerEntry.Value;
                
                // Create a new instance of the replacement type or the original type
                var newController = originalController.GetVirtualCopy(); // Assuming IController has a Clone method
                

                if (newController is not null)
                {
                    // Add the new controller to the new manager
                    controllerManager_copy.AddController(newController);
                }
                else
                {
                    throw new NullReferenceException($"Null encountered when trying to make a copy of controller {controllerEntry.Value.Name}");
                }
            }

            List<BaseMasterController> masterControllers_copy = controllerManager_copy.Controllers.Values
                .OfType<BaseMasterController>() // Filters only BaseMasterController instances
                .ToList();

            foreach (var masterController_copy in masterControllers_copy)
            {
                var controllerToCopy = Controllers[masterController_copy.Name] as BaseMasterController;
                foreach (var (slaveControllerName, slaveController) in controllerToCopy.SlaveControllers)
                {
                    masterController_copy.AddSlaveController(controllerManager_copy.Controllers[slaveControllerName], controllerManager_copy.ControllerLocks[slaveControllerName]);
                    controllerManager_copy.Controllers[slaveControllerName].MasterController = masterController_copy;
                }
            }

            ToolInformation toolInfo = new ToolInformation(controllerManager_copy.GetDevices<BasePositionerDevice>(), new ShutterDevice('u', "undefined"), this.ToolInformation.PositionCalcFunctions);
            if (controllerManager_copy.TryGetDevice<BaseShutterDevice>(this.ToolInformation.Name, out BaseShutterDevice shutterDevice))
                toolInfo = new ToolInformation(controllerManager_copy.GetDevices<BasePositionerDevice>(), shutterDevice, this.ToolInformation.PositionCalcFunctions);
            controllerManager_copy.ToolInformation = toolInfo;

            return controllerManager_copy;
        }
    }
}