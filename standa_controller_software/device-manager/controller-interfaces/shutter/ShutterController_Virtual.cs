using standa_controller_software.command_manager;
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
    public class ShutterController_Virtual : BaseShutterController
    {

        private class DeviceInformation
        {
            public bool _isOn = false;
            public int _delayOn = 0;
            public int _delayOff = 0;
        }
        private ConcurrentDictionary<char, DeviceInformation> _deviceInfo = new ConcurrentDictionary<char, DeviceInformation>();

        public ShutterController_Virtual(string name) : base(name) { }
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
        public override BaseController GetCopy()
        {
            var controller = new ShutterController_Virtual(Name);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value.GetCopy());
            }

            return controller;
        }

        public override Task UpdateStatesAsync(ConcurrentQueue<string> log)
        {
            //foreach (var device in Devices)
            //{
            //    device.Value.IsOn = _deviceInfo[device.Key]._isOn;
            //    device.Value.DelayOn = _deviceInfo[device.Key]._delayOn;
            //    device.Value.DelayOff = _deviceInfo[device.Key]._delayOff;
            //    // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Updated state for device {device.Value.Name}, State: {device.Value.IsOn}");
            //}

            return Task.CompletedTask;
            //await Task.Delay(10);
        }


        protected override Task ChangeStateImplementation(BaseShutterDevice device, bool wantedState)
        {
            device.IsOn = wantedState;
            _deviceInfo[device.Name]._isOn = wantedState;
            return Task.CompletedTask;
        }

        protected override Task ChangeStateOnIntervalImplementation(BaseShutterDevice device, float duration)
        {
            return Task.CompletedTask;
        }
    }
}
