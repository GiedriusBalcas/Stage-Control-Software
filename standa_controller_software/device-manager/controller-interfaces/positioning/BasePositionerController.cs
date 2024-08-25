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

        private ConcurrentDictionary<char, CancellationTokenSource> deviceCancellationTokens = new ConcurrentDictionary<char, CancellationTokenSource>();
        private Dictionary<string, Func<Command, BasePositionerDevice, CancellationToken, SemaphoreSlim, Task>> _methodMap = new Dictionary<string, Func<Command, BasePositionerDevice, CancellationToken, SemaphoreSlim, Task>>();
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
        public override void ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
        {
            semaphore.Release();
            if (device is BasePositionerDevice positioningDevice && Devices.ContainsValue(positioningDevice))
            {
                positioningDevice.IsConnected = true;
            }
            else
                throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");
        }

        public override async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            if (Devices.TryGetValue(command.TargetDevice, out BasePositionerDevice device))
            {
                log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Executing {command.Action} command on device {device.Name}, parameters: {string.Join(" ", command.Parameters)}");

                var tokenSource = new CancellationTokenSource();

                if (deviceCancellationTokens.ContainsKey(device.Name) && command.Action == "MoveAbsolute")
                {
                    deviceCancellationTokens[device.Name].Cancel();
                    deviceCancellationTokens[device.Name] = tokenSource;
                }
                else
                {
                    deviceCancellationTokens.TryAdd(device.Name, tokenSource);
                }

                if (_methodMap.TryGetValue(command.Action, out var method))
                {
                    if (command.Await)
                        await method(command, device, tokenSource.Token, semaphore);
                    else
                        _ = method(command, device, tokenSource.Token, semaphore); // Start method without awaiting
                }
                else
                {
                    throw new InvalidOperationException("Invalid action");
                }
                if (command.Await)
                    log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Completed {command.Action} command on device {device.Name}");
            }
            else
            {
                log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Device {command.TargetDevice} not found in controller {command.TargetController}");
            }
        }

        public override List<BaseDevice> GetDevices()
        {
            return Devices.Values.Cast<BaseDevice>().ToList();
        }

        public override abstract Task UpdateStateAsync(ConcurrentQueue<string> log);

        protected abstract Task MoveAbsolute(Command command, BasePositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore);
        protected abstract Task UpdateMoveSettings(Command command, BasePositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore);
        protected abstract Task WaitUntilStop(Command command, BasePositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore);

        public override abstract BaseController GetCopy();
    }
}
