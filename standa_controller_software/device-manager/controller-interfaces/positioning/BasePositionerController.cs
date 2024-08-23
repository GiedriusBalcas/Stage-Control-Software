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
    public abstract class BasePositionerController : IController
    {

        private ConcurrentDictionary<string, CancellationTokenSource> deviceCancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        private Dictionary<string, Func<Command, IPositionerDevice, CancellationToken, SemaphoreSlim, Task>> _methodMap = new Dictionary<string, Func<Command, IPositionerDevice, CancellationToken, SemaphoreSlim, Task>>();
        protected Dictionary<string, IPositionerDevice> Devices { get; }
        public string Name { get; private set; }

        public BasePositionerController(string name)
        {
            Name = name;
            _methodMap["MoveAbsolute"] = MoveAbsolute;
            _methodMap["UpdateMoveSettings"] = UpdateMoveSettings;
            _methodMap["WaitUntilStop"] = WaitUntilStop;
            Devices = new Dictionary<string, IPositionerDevice>();
            //methodMap["UpdateStates"] = UpdateStatesCall;
        }

        public virtual void AddDevice(IDevice device)
        {
            if (device is IPositionerDevice positioningDevice)
            {
                Devices.Add(positioningDevice.Name, positioningDevice);
            }
            else
                throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");
        }

        public async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            if (Devices.TryGetValue(command.TargetDevice, out IPositionerDevice device))
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

        public List<IDevice> GetDevices()
        {
            return Devices.Values.Cast<IDevice>().ToList();
        }

        public abstract Task UpdateStateAsync(ConcurrentQueue<string> log);

        protected virtual Task MoveAbsolute(Command command, IPositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore) 
        {
            device.CurrentPosition = (float)command.Parameters[0];
            semaphore.Release();
            return Task.CompletedTask;
        }
        protected abstract Task UpdateMoveSettings(Command command, IPositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore);
        protected abstract Task WaitUntilStop(Command command, IPositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore);

        public abstract IController GetCopy();
    }
}
