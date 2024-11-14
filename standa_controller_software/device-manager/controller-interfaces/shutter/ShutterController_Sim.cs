using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.shutter
{
    public class ShutterController_Sim : BaseShutterController
    {

        private class DeviceInformation
        {
            public bool _isOn = false;
            public int _delayOn = 1000;
            public int _delayOff = 100;
        }
        private ConcurrentDictionary<char, DeviceInformation> _deviceInfo = new ConcurrentDictionary<char, DeviceInformation>();

        public ShutterController_Sim(string name) : base(name)
        {

        }
        public override void AddDevice(BaseDevice device)
        {
            base.AddDevice(device);
            if (device is BaseShutterDevice shuttterDevice)
            {
                _deviceInfo.TryAdd(shuttterDevice.Name, new DeviceInformation()
                {
                    _delayOff = shuttterDevice.DelayOff,
                    _delayOn = shuttterDevice.DelayOn,
                });
            }
        }
        public override BaseController GetVirtualCopy()
        {
            var controller = new ShutterController_Sim(Name);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value.GetCopy());
            }

            return controller;
        }

        public override Task UpdateStatesAsync(ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }

        protected override Task ChangeStateImplementation(BaseShutterDevice device, bool wantedState)
        {
            _deviceInfo[device.Name]._isOn = wantedState;
            device.IsOn = wantedState;

            return Task.CompletedTask;
        }

        protected override async Task ChangeStateOnIntervalImplementation(BaseShutterDevice device, float duration)
        {
            await ChangeStateImplementation(device, true);
            await Task.Delay((int)Math.Round(duration * 1000));
            await ChangeStateImplementation(device, false);
        }
    }
}
