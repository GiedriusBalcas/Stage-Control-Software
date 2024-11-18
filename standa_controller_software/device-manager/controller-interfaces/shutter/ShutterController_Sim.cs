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

        public ShutterController_Sim(string name, ConcurrentQueue<string> log) : base(name, log)
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
        
        protected override Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            foreach(var (deviceName, device) in Devices)
            {
                //device.IsOn = _deviceInfo[device.Name]._isOn;
            }
            return Task.CompletedTask;
        }

        protected override Task ChangeState_implementation(BaseShutterDevice device, bool wantedState)
        {
            _deviceInfo[device.Name]._isOn = wantedState;
            device.IsOn = wantedState;

            return Task.CompletedTask;
        }
        protected override async Task ChangeStateOnInterval_implementation(BaseShutterDevice device, float duration)
        {
            await ChangeState_implementation(device, true);
            await Task.Delay((int)Math.Round(duration * 1000));
            await ChangeState_implementation(device, false);
        }
        protected override Task ConnectDevice_implementation(BaseDevice device)
        {
            return Task.CompletedTask;
        }
    }
}
