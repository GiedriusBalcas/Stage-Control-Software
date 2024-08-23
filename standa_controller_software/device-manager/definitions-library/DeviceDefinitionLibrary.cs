using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;

namespace standa_controller_software.device_manager
{

    public static class DeviceDefinitionLibrary
    {
        public static ControllerTypeDictionary ControllerDefinitions { get; private set; }

        static DeviceDefinitionLibrary()
        {
            ControllerDefinitions = new ControllerTypeDictionary();
            InitializeLibrary();
        }

        private static void InitializeLibrary()
        {
            var positionerDeviceDefinitions = new List<DeviceInfo>
            {
                new DeviceInfo { Name = "Linear Positioner", Type = typeof(LinearPositionerDevice) },
                new DeviceInfo { Name = "Rotary Positioner", Type = typeof(LinearPositionerDevice) }
            };

            var positionerControllerTypeDefinitions = new List<ControllerInfo>
            {
                new ControllerInfo
                {
                    Name = "Virtual Positioner Controller",
                    Type = typeof(VirtualPositionerController),
                    AllowedDevices = positionerDeviceDefinitions
                }
            };

            ControllerDefinitions.Add(typeof(BasePositionerController), positionerControllerTypeDefinitions);
        }
    }
}
