using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.devices;
using standa_controller_software.device_manager.devices.shutter;
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



            var shutterDeviceDefinitions = new List<DeviceInfo>
            {
                new DeviceInfo { Name = "Shutter", Type = typeof(ShutterDevice) },
            };

            var shutterControllerTypeDefinitions = new List<ControllerInfo>
            {
                new ControllerInfo
                {
                    Name = "Virtual Shutter Controller",
                    Type = typeof(VirtualShutterController),
                    AllowedDevices = shutterDeviceDefinitions
                },

                new ControllerInfo
                {
                    Name = "Arduino Shutter Controller",
                    Type = typeof(ShutterController_Arduino),
                    AllowedDevices = shutterDeviceDefinitions
                }
            };

            ControllerDefinitions.Add(typeof(BasePositionerController), positionerControllerTypeDefinitions);
            ControllerDefinitions.Add(typeof(BaseShutterController), shutterControllerTypeDefinitions);
        }
    }
}
