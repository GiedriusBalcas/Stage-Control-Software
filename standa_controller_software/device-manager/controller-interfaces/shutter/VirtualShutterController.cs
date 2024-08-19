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
    public class VirtualShutterController : BaseShutterController
    {

        private class DeviceInformation
        {
            public bool _isOn = false;
            public int _delayOn = 1000;
            public int _delayOff = 100;
        }
        private ConcurrentDictionary<string, DeviceInformation> _deviceInfo = new ConcurrentDictionary<string, DeviceInformation>();

        public VirtualShutterController(string name) : base(name)
        {
        }


        public override void AddDevice(IDevice device)
        {
            base.AddDevice(device);
            if (device is IShutterDevice shuttterDevice)
            {
                _deviceInfo.TryAdd(shuttterDevice.Name, new DeviceInformation()
                {
                    _delayOff = shuttterDevice.DelayOff,
                    _delayOn = shuttterDevice.DelayOn,
                });
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
            device.IsOn = state;
            await Task.Delay(10, token);
        }

        protected override async Task ChangeStateOnInterval(Command command, IShutterDevice device, CancellationToken token)
        {
            var duration = (float)command.Parameters[0] * 1000000 - device.DelayOff*1000 - device.DelayOn*1000;

            var state = true;
            await Task.Run(() => DelayMicroseconds((int)device.DelayOn*1000), token);

            _deviceInfo[device.Name]._isOn = state;

            await Task.Run(() => DelayMicroseconds((int)duration), token);

            state = false;
            _deviceInfo[device.Name]._isOn = state;

        }

        private static async Task DelayMicroseconds(int microseconds)
        {
            if (microseconds <= 0) return;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Spin until the requested number of microseconds has passed
            while (stopwatch.ElapsedTicks < microseconds * (Stopwatch.Frequency / 1_000_000))
            {
                // Using Thread.Yield() to give up the remainder of the time slice to another thread
                // This helps avoid too much CPU consumption
                Thread.Yield();
            }

            stopwatch.Stop();
        }
    }
}
