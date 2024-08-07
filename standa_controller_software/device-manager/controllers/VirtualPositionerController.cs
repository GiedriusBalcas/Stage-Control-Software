using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controllers
{
    public class VirtualPositionerController : IPositionerController
    {
        public Dictionary<string, IPositionerDevice> Devices { get; private set; } = new Dictionary<string, IPositionerDevice>();

        public string Name { get; set; }

        private ConcurrentDictionary<string, CancellationTokenSource> deviceCancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();

        public VirtualPositionerController(string name)
        {
            Name = name;
        }

        public void AddDevice(IDevice device)
        {
            if (device is IPositionerDevice positioningDevice)
                Devices.Add(positioningDevice.DeviceId, positioningDevice);
            else
                throw new Exception($"Unable to add device: {device.DeviceId}. Controller {this.Name} only accepts positioning devices.");
        }

        public async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            if (Devices.TryGetValue(command.TargetDevice, out IPositionerDevice device))
            {
                log.Enqueue($"{DateTime.Now}: Executing {command.Action} command on device {device.DeviceId}");

                if (command.Action == "MoveAbsolute")
                {
                    //int targetPosition = int.Parse((string)command.Parameters[0]);
                    int targetPosition = Convert.ToInt32(command.Parameters[0]);
                    var tokenSource = new CancellationTokenSource();

                    if (deviceCancellationTokens.ContainsKey(device.DeviceId))
                    {
                        deviceCancellationTokens[device.DeviceId].Cancel();
                        deviceCancellationTokens[device.DeviceId] = tokenSource;
                    }
                    else
                    {
                        deviceCancellationTokens.TryAdd(device.DeviceId, tokenSource);
                    }

                    await MoveDeviceAsync(device as IPositionerDevice, targetPosition, tokenSource.Token, log);
                }

                log.Enqueue($"{DateTime.Now}: Completed {command.Action} command on device {device.DeviceId}, New Position: {device.Position}");
            }
            else
            {
                log.Enqueue($"{DateTime.Now}: Device {command.TargetDevice} not found in controller {command.TargetController}");
            }
        }

        private async Task MoveDeviceAsync(IPositionerDevice device, int targetPosition, CancellationToken token, ConcurrentQueue<string> log)
        {
            int speed = 100;  // Units per second
            int distance = Math.Abs(targetPosition - device.Position);
            float duration = distance / speed * 1000;  // Simulate duration in milliseconds

            try
            {
                for (int i = 0; i < duration; i+= 100)
                {
                    await Task.Delay(100, token);  // Check for cancellation every 100 ms
                    if (token.IsCancellationRequested)
                    {
                        log.Enqueue($"{DateTime.Now}: Move to {targetPosition} on device {device.DeviceId} was canceled");
                        return;
                    }
                }
                device.Position = targetPosition;
            }
            catch (TaskCanceledException)
            {
                log.Enqueue($"{DateTime.Now}: Move to {targetPosition} on device {device.DeviceId} was canceled");
            }
        }

        public async Task UpdateStateAsync(ConcurrentQueue<string> log)
        {
            foreach (var device in Devices.Values)
            {
                log.Enqueue($"{DateTime.Now}: Updated state for device {device.DeviceId}, Position: {device.Position}");
            }
            await Task.Delay(50);
        }

        public List<IDevice> GetDevices()
        {
            return Devices.Values.Cast<IDevice>().ToList();
        }
    }
}
