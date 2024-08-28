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
        private ConcurrentDictionary<char, DeviceInformation> _deviceInfo = new ConcurrentDictionary<char, DeviceInformation>();

        public VirtualShutterController(string name) : base(name)
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
        public override BaseController GetCopy()
        {
            var controller = new ShutterController_Virtual(Name);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value.GetCopy());
            }

            return controller;
        }

        public override async Task UpdateStatesAsync(ConcurrentQueue<string> log)
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

        protected override async Task ChangeState(Command command, List<BaseShutterDevice> devices, Dictionary<char, CancellationToken> cancellationTokens, SemaphoreSlim semaphore)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var state = (bool)command.Parameters[i][0];
                await Task.Delay(2, cancellationTokens[device.Name]);
                _deviceInfo[device.Name]._isOn = state;
                device.IsOn = state;
            }
        }

        protected override async Task ChangeStateOnInterval(Command command, List<BaseShutterDevice> devices, Dictionary<char, CancellationToken> cancellationTokens, SemaphoreSlim semaphore)
        {
            float duration = 0;
            var token = cancellationTokens.Values.Where(val => val != null).First();
            
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                duration = (float)command.Parameters[i][0] * 1000000 - device.DelayOff * 1000 - device.DelayOn * 1000;
                var state = true;
                await Task.Run(() => DelayMicroseconds((int)device.DelayOn * 1000), token);
                _deviceInfo[device.Name]._isOn = state;
            }

            await Task.Run(() => DelayMicroseconds((int)duration), token);
            
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var state = false;
                _deviceInfo[device.Name]._isOn = state;
            }

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

        protected override Task SetDelayAsync(Command command, List<BaseShutterDevice> devices, Dictionary<char, CancellationToken> cancellationTokens, SemaphoreSlim semaphore)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var delayOn = (uint)command.Parameters[i][0];
                var delayOff = (uint)command.Parameters[i][1];

                _deviceInfo[device.Name]._delayOn = (int)delayOn;
                _deviceInfo[device.Name]._delayOff = (int)delayOff;
            }
            return Task.CompletedTask;
        }
    }
}
