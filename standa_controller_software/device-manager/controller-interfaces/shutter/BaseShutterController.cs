using standa_controller_software.command_manager;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.shutter
{
    public abstract class BaseShutterController : BaseController
    {
        private Dictionary<string, Func<Command, BaseShutterDevice, CancellationToken, Task>> _methodMap = new Dictionary<string, Func<Command, BaseShutterDevice, CancellationToken, Task>>();
        private ConcurrentDictionary<char, CancellationTokenSource> _deviceCancellationTokens = new ConcurrentDictionary<char, CancellationTokenSource>();
        protected Dictionary<char, BaseShutterDevice> Devices { get; }

        protected BaseShutterController(string name) : base(name)
        {
            _methodMap[CommandDefinitionsLibrary.ChangeShutterState.ToString()] = ChangeState;
            _methodMap[CommandDefinitionsLibrary.ChangeShutterStateOnInterval.ToString()] = ChangeStateOnInterval;

            Devices = new Dictionary<char, BaseShutterDevice>();
            //methodMap["UpdateStates"] = UpdateStatesCall;
        }

        protected abstract Task ChangeStateOnInterval(Command command, BaseShutterDevice device, CancellationToken token);
        protected abstract Task SetDelayAsync(Command command, BaseShutterDevice device, CancellationToken token);

        protected abstract Task ChangeState(Command command, BaseShutterDevice device, CancellationToken token);

        public override void AddDevice(BaseDevice device)
        {
            if (device is BaseShutterDevice shutterDevice)
            {
                Devices.Add(shutterDevice.Name, shutterDevice);
            }
            else
                throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");
        }

        public override async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            if (Devices.TryGetValue(command.TargetDevice, out BaseShutterDevice device))
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

        public override abstract BaseController GetCopy();

        public override List<BaseDevice> GetDevices()
        {
            return Devices.Values.Cast<BaseDevice>().ToList();
        }

        public override abstract Task UpdateStateAsync(ConcurrentQueue<string> log);
    }
}
