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

        protected ConcurrentDictionary<char, CancellationTokenSource> deviceCancellationTokens = new ConcurrentDictionary<char, CancellationTokenSource>();
        protected Dictionary<char, BasePositionerDevice> Devices { get; }

        public BasePositionerController(string name) : base(name)
        {
            _methodMap[CommandDefinitions.MoveAbsolute] = new MethodInformation()
            {
                MethodHandle = MoveAbsolute,
                Quable = false,
                State = MethodState.Free,
            };
            _methodMap[CommandDefinitions.UpdateMoveSettings] = new MethodInformation()
            {
                MethodHandle = UpdateMoveSettings,
                Quable = false,
                State = MethodState.Free,
            };
            _methodMap[CommandDefinitions.WaitUntilStop] = new MethodInformation()
            {
                MethodHandle = WaitUntilStop,
                Quable = false,
                State = MethodState.Free,
            };
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
        

        public override List<BaseDevice> GetDevices()
        {
            return Devices.Values.Cast<BaseDevice>().ToList();
        }

        public override abstract Task UpdateStatesAsync(ConcurrentQueue<string> log);
        protected abstract Task MoveAbsolute(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        protected abstract Task UpdateMoveSettings(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        protected abstract Task WaitUntilStop(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        protected abstract Task WaitUntilStopPolar(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log);

        public override abstract BaseController GetCopy();
    }
}
