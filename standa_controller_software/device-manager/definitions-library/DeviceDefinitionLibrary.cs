﻿using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.controller_interfaces.sync;
using standa_controller_software.device_manager.devices;
using standa_controller_software.device_manager.devices.positioning;
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
                new DeviceInfo { Name = "Attenuator Positioner", Type = typeof(AttenuatorPositionerDevice) }
            };

            var positionerControllerTypeDefinitions = new List<ControllerInfo>
            {
                new ControllerInfo
                {
                    Name = "Virtual Positioner Controller",
                    Type = typeof(PositionerController_Sim),
                    AllowedDevices = positionerDeviceDefinitions,
                    VirtualType = typeof(PositionerController_Virtual)
                },
                new ControllerInfo
                {
                    Name = "Positioner Controller XIMC",
                    Type = typeof(PositionerController_XIMC),
                    AllowedDevices = positionerDeviceDefinitions,
                    VirtualType = typeof(PositionerController_Virtual)
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
                    Type = typeof(ShutterController_Sim),
                    AllowedDevices = shutterDeviceDefinitions
                },
                new ControllerInfo
                {
                    Name = "Arduino Shutter Controller",
                    Type = typeof(ShutterController_Arduino),
                    AllowedDevices = shutterDeviceDefinitions
                }
            };

            var masterControllerTypeDefinitions = new List<ControllerInfo>
            {
                new ControllerInfo
                {
                    Name = "Virtual Master Controller",
                    Type = typeof(PositionAndShutterController_Sim),
                    AllowedDevices = shutterDeviceDefinitions
                },
                new ControllerInfo
                {
                    Name = "Pico Master Controller",
                    Type = typeof(PositionAndShutterController_Pico),
                    AllowedDevices = shutterDeviceDefinitions
                },
            };


            var syncControllerTypeDefinitions = new List<ControllerInfo>
            {
                new ControllerInfo
                {
                    Name = "Virtual Sync Controller",
                    Type = typeof(SyncController_Sim),
                    AllowedDevices = new List<DeviceInfo>()
                },
                new ControllerInfo
                {
                    Name = "Pico Sync Controller",
                    Type = typeof(SyncController_Pico),
                    AllowedDevices = new List<DeviceInfo>()
                },
            };

            ControllerDefinitions.Add(typeof(BasePositionerController), positionerControllerTypeDefinitions);
            ControllerDefinitions.Add(typeof(BaseShutterController), shutterControllerTypeDefinitions);
            ControllerDefinitions.Add(typeof(BaseMasterController), masterControllerTypeDefinitions);
            ControllerDefinitions.Add(typeof(BaseController), syncControllerTypeDefinitions);
        }
    }
}
