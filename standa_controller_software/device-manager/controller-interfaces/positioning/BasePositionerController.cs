using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.positioning
{
    public abstract class BasePositionerController : BaseController
    {

        private Dictionary<string, Func<Command, List<BasePositionerDevice>, SemaphoreSlim, ConcurrentQueue<string>, Task>> _methodMap = 
            new Dictionary<string, Func<Command, List<BasePositionerDevice>, SemaphoreSlim, ConcurrentQueue<string>, Task>>();
        protected ConcurrentDictionary<char, CancellationTokenSource> deviceCancellationTokens = new ConcurrentDictionary<char, CancellationTokenSource>();
        protected Dictionary<char, BasePositionerDevice> Devices { get; }

        public BasePositionerController(string name) : base(name)
        {
            _methodMap["MoveAbsolute"] = MoveAbsolute;
            _methodMap["UpdateMoveSettings"] = UpdateMoveSettings;
            _methodMap["WaitUntilStop"] = WaitUntilStop;
            Devices = new Dictionary<char, BasePositionerDevice>();
            //methodMap["UpdateStates"] = UpdateStatesCall;
        }

        public override void AddDevice(BaseDevice device)
        {
            if (device is BasePositionerDevice positioningDevice)
            {
                Devices.Add(positioningDevice.Name, positioningDevice);
            }
            else
                throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");
        }
        public override Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
        {
            semaphore.Release();
            if (device is BasePositionerDevice positioningDevice && Devices.ContainsValue(positioningDevice))
            {
                positioningDevice.IsConnected = true;
            }
            else
                throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");
            
            return Task.CompletedTask;
        }


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

            List<BasePositionerDevice> devices = new List<BasePositionerDevice> ();
            Dictionary<char, CancellationToken> cancelationTokens = new Dictionary<char, CancellationToken>();

            foreach (var deviceName in command.TargetDevices) 
            {
                if (Devices.TryGetValue(deviceName, out BasePositionerDevice device))
                {
                    devices.Add(device);
                }
                else
                {
                    // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Device {deviceName} not found in controller {command.TargetController}");
                }

                var tokenSource = new CancellationTokenSource();
                if (deviceCancellationTokens.ContainsKey(deviceName) && command.Action == CommandDefinitionsLibrary.MoveAbsolute.ToString())
                {
                    deviceCancellationTokens[deviceName].Cancel();
                    deviceCancellationTokens[deviceName] = tokenSource;
                }
                else
                {
                    deviceCancellationTokens.TryAdd(deviceName, tokenSource);
                }
                cancelationTokens.Add(deviceName, tokenSource.Token);



            }

            if (_methodMap.TryGetValue(command.Action, out var method))
            {
                if (command.Await)
                    await method(command, devices, semaphore, log);
                else
                    _ = method(command, devices, semaphore, log);
            }
            else
            {
                throw new InvalidOperationException("Invalid action");
            }
            
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Completed {command.Action} command on device {string.Join(' ', command.TargetDevices)}");
        }

        public override List<BaseDevice> GetDevices()
        {
            return Devices.Values.Cast<BaseDevice>().ToList();
        }

        public override abstract Task UpdateStatesAsync(ConcurrentQueue<string> log);

        protected abstract Task MoveAbsolute(Command command, List<BasePositionerDevice> devices, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        protected abstract Task UpdateMoveSettings(Command command, List<BasePositionerDevice> devices, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        protected abstract Task WaitUntilStop(Command command, List<BasePositionerDevice> devices, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        protected abstract Task WaitUntilStopPolar(Command command, List<BasePositionerDevice> devices, SemaphoreSlim semaphore, ConcurrentQueue<string> log);

        public override abstract BaseController GetCopy();
    }
}
