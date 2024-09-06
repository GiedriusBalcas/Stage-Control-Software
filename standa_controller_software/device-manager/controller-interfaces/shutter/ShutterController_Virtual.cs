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
            foreach (var device in Devices)
            {
                device.Value.IsOn = _deviceInfo[device.Key]._isOn;
                device.Value.DelayOn = _deviceInfo[device.Key]._delayOn;
                device.Value.DelayOff = _deviceInfo[device.Key]._delayOff;
                // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Updated state for device {device.Value.Name}, State: {device.Value.IsOn}");
            }

            return Task.CompletedTask;
            //await Task.Delay(10);
        }

        protected override Task ChangeState(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();

            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                var state = (bool)command.Parameters[i][0];
                _deviceInfo[device.Name]._isOn = state;
                device.IsOn = state;
            }
            return Task.CompletedTask;
        }

        protected override Task ChangeStateOnInterval(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();

            //var duration = (float)command.Parameters[0] * 1000000;
            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                _deviceInfo[device.Name]._isOn = true;
                device.IsOn = true;
            }
            ////await Task.Run(() => DelayMicroseconds((int)duration), token);
            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                _deviceInfo[device.Name]._isOn = false;
                device.IsOn = false;
            }
            return Task.CompletedTask;

        }

        protected override Task SetDelayAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();

            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                var delayOn = (uint)command.Parameters[i][0];
                var delayOff = (uint)command.Parameters[i][1];

                _deviceInfo[device.Name]._delayOn = (int)delayOn;
                _deviceInfo[device.Name]._delayOff = (int)delayOff;
            }
            return Task.CompletedTask;
        }

        public override void AddSlaveController(BaseController controller)
        {
            // TODO: implement the add SlaveController
            SlaveControllers.Add(controller.Name, controller);
        }
    }
}
