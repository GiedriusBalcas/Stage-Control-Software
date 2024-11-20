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

        public ShutterController_Virtual(string name, ConcurrentQueue<string> log) : base(name, log) { }
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
        public override Task ForceStop()
        {
            return Task.CompletedTask;

        }

        protected override Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }

        protected override Task ChangeState_implementation(BaseShutterDevice device, bool wantedState)
        {
            device.IsOn = wantedState;
            _deviceInfo[device.Name]._isOn = wantedState;
            return Task.CompletedTask;
        }
        protected override Task ChangeStateOnInterval_implementation(BaseShutterDevice device, float duration)
        {
            return Task.CompletedTask;
        }
        protected override Task ConnectDevice_implementation(BaseDevice device)
        {
            return Task.CompletedTask;
        }
    }
}
