using standa_controller_software.command_manager;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces
{
    public abstract class BaseShutterController : IController
    {
        private Dictionary<string, Func<Command, IShutterDevice, CancellationToken, Task>> _methodMap = new Dictionary<string, Func<Command, IShutterDevice, CancellationToken, Task>>();
        private ConcurrentDictionary<string, CancellationTokenSource> _deviceCancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        protected Dictionary<string, IShutterDevice> Devices { get; }
        public string Name { get; private set; }

        protected BaseShutterController(string name)
        {
            Name = name;
            _methodMap["ChangeState"] = ChangeState;
            Devices = new Dictionary<string, IShutterDevice>();
            //methodMap["UpdateStates"] = UpdateStatesCall;
        }

        protected abstract Task ChangeState(Command command, IShutterDevice device, CancellationToken token);

        public virtual void AddDevice(IDevice device)
        {
            if (device is IShutterDevice shutterDevice)
            {
                Devices.Add(shutterDevice.Name, shutterDevice);
            }
            else
                throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");
        }

        public async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            if (Devices.TryGetValue(command.TargetDevice, out IShutterDevice device))
            {
                log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Executing {command.Action} command on device {device.Name}");

                var tokenSource = new CancellationTokenSource();

                if (_deviceCancellationTokens.TryGetValue(device.Name, out CancellationTokenSource? token) && command.Action == "MoveAbsolute")
                {
                    token.Cancel();
                    _deviceCancellationTokens[device.Name] = tokenSource;
                }
                else
                {
                    _deviceCancellationTokens.TryAdd(device.Name, tokenSource);
                }

                if (_methodMap.TryGetValue(command.Action, out var method))
                {
                    if (command.Await)
                        await method(command, device, tokenSource.Token);
                    else
                        _ = method(command, device, tokenSource.Token); // Start method without awaiting
                }
                else
                {
                    throw new InvalidOperationException("Invalid action");
                }

                log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Completed {command.Action} command on device {device.Name}");
            }
            else
            {
                log.Enqueue($"{DateTime.Now:HH:mm:ss.fff}: Device {command.TargetDevice} not found in controller {command.TargetController}");
            }
        }

        public abstract IController GetCopy();

        public List<IDevice> GetDevices()
        {
            return Devices.Values.Cast<IDevice>().ToList();
        }

        public abstract Task UpdateStateAsync(ConcurrentQueue<string> log);
    }
}
