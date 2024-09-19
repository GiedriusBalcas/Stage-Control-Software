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
        private ConcurrentDictionary<char, CancellationTokenSource> _deviceCancellationTokens = new ConcurrentDictionary<char, CancellationTokenSource>();
        protected Dictionary<char, BaseShutterDevice> Devices { get; }

        protected BaseShutterController(string name) : base(name)
        {
            //_methodMap[CommandDefinitionsLibrary.ChangeShutterState.ToString()] = ChangeState;
            //_methodMap[CommandDefinitionsLibrary.ChangeShutterStateOnInterval.ToString()] = ChangeStateOnInterval;
            _methodMap[CommandDefinitions.ChangeShutterState] = new MethodInformation
            {
                MethodHandle = ChangeState,
                Quable = false,
                State = MethodState.Waiting,
            };

            Devices = new Dictionary<char, BaseShutterDevice>();
            //methodMap["UpdateStates"] = UpdateStatesCall;
        }
        public override void AddDevice(BaseDevice device)
        {
            if (device is BaseShutterDevice shutterDevice)
            {
                Devices.Add(shutterDevice.Name, shutterDevice);
            }
            else
                throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");
        }
        public override Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
        {
            semaphore.Release();
            if (device is BaseShutterDevice shutterDevice && Devices.ContainsValue(shutterDevice))
            {
                shutterDevice.IsConnected = true;
            }
            else
                throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");

            return Task.CompletedTask;
        }

        protected abstract Task ChangeStateOnInterval(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        protected abstract Task SetDelayAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log);

        protected abstract Task ChangeState(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        private string FormatParameters(object[][] parameters)
        {
            var formattedParameters = parameters
                .Select(paramArray =>
                {
                    if (paramArray == null)
                    {
                        return "[null]";
                    }
                    return $"[{string.Join(", ", paramArray.Select(p => p?.ToString() ?? "null"))}]";
                });

            return string.Join(" ", formattedParameters); // Join all sub-arrays with a space
        }
        public override async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Executing {command.Action} command on device {string.Join(' ', command.TargetDevices)}, parameters: {FormatParameters(command.Parameters)}");

            Dictionary<char, CancellationToken> cancelationTokens = new Dictionary<char, CancellationToken>();
            List<BaseShutterDevice> devices = new List<BaseShutterDevice>();
            foreach (var deviceName in command.TargetDevices)
            {
                if (Devices.TryGetValue(deviceName, out BaseShutterDevice device))
                {
                    devices.Add(device);
                }
                else
                {
                    // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Device {command.TargetDevices} not found in controller {command.TargetController}");
                }
                var tokenSource = new CancellationTokenSource();
                if (_deviceCancellationTokens.ContainsKey(deviceName))
                {
                    _deviceCancellationTokens[deviceName].Cancel();
                    _deviceCancellationTokens[deviceName] = tokenSource;
                }
                else
                {
                    _deviceCancellationTokens.TryAdd(deviceName, tokenSource);
                }
                cancelationTokens.Add(deviceName, tokenSource.Token);
            }

            if (_methodMap.TryGetValue(command.Action, out var method))
            {
                if (command.Await)
                    await method.MethodHandle(command, semaphore, log);
                else
                    _ = method.MethodHandle(command, semaphore, log);// Start method without awaiting
            }
            else
            {
                throw new InvalidOperationException("Invalid action");
            }

            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Completed {command.Action} command on device {command.TargetDevices}");
        }

        public override abstract BaseController GetCopy();

        public override List<BaseDevice> GetDevices()
        {
            return Devices.Values.Cast<BaseDevice>().ToList();
        }

        public override Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }
        public override abstract Task UpdateStatesAsync(ConcurrentQueue<string> log);
    }
}
