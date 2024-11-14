using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
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
            _methodMap[CommandDefinitions.ChangeShutterStateOnInterval] = new MethodInformation
            {
                MethodHandle = ChangeStateOnInterval,
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
            try
            {
                if (device is BaseShutterDevice shutterDevice && Devices.ContainsValue(shutterDevice))
                {
                    shutterDevice.IsConnected = true;
                }
                else
                    throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");

            }
            catch { }

            return Task.CompletedTask;
        }

        protected virtual async Task ChangeStateOnInterval(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            if (command.Parameters is ChangeShutterStateForIntervalParameters parameters)
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    var device = devices[i];
                    var duration = parameters.Duration;

                    await ChangeStateOnIntervalImplementation(device, duration);
                }
            }
        }

        protected virtual async Task ChangeState(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            if (command.Parameters is ChangeShutterStateParameters parameters)
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    var device = devices[i];
                    var state = parameters.State;

                    await ChangeStateImplementation(device, state);
                }
            }
        }
        protected abstract Task ChangeStateImplementation(BaseShutterDevice device, bool wantedState);
        protected abstract Task ChangeStateOnIntervalImplementation(BaseShutterDevice device, float duration);


        public override abstract BaseController GetVirtualCopy();

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
