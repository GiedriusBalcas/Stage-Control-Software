using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            public int _delayOn = 1000;
            public int _delayOff = 100;
        }
        private ConcurrentDictionary<string, DeviceInformation> _deviceInfo = new ConcurrentDictionary<string, DeviceInformation>();

        public ShutterController_Virtual(string name) : base(name)
        {
        }

        public override void AddDevice(IDevice device)
        {
            base.AddDevice(device);
            if (device is IShutterDevice shuttterDevice)
            {
                _deviceInfo.TryAdd(shuttterDevice.Name, new DeviceInformation());
            }
        }
        public override IController GetCopy()
        {
            var controller = new ShutterController_Virtual(Name);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value.GetCopy());
            }

            return controller;
        }

        public override async Task UpdateStateAsync(ConcurrentQueue<string> log)
        {
            foreach (var device in Devices)
            {
                device.Value.IsOn = _deviceInfo[device.Key]._isOn;
                device.Value.DelayOn = _deviceInfo[device.Key]._delayOn;
                device.Value.DelayOff = _deviceInfo[device.Key]._delayOff;
                log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Updated state for device {device.Value.Name}, State: {device.Value.IsOn}");
            }
            await Task.Delay(10);
        }

        protected override async Task ChangeState(Command command, IShutterDevice device, CancellationToken token)
        {
            var state = (bool)command.Parameters[0];
            _deviceInfo[device.Name]._isOn = state;
            await Task.Delay(10, token);
        }
    }
}
