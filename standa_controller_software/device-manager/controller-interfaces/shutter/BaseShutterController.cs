using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.command_manager.command_parameter_library.Common;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.shutter
{
    public abstract class BaseShutterController : BaseController
    {
        private ConcurrentDictionary<char, CancellationTokenSource> _deviceCancellationTokens = new ConcurrentDictionary<char, CancellationTokenSource>();
        protected Dictionary<char, BaseShutterDevice> Devices { get; }

        protected BaseShutterController(string name, ILoggerFactory loggerFactory) : base(name, loggerFactory)
        {
            _logger = _loggerFactory.CreateLogger<BaseShutterController>();

            _methodMap[CommandDefinitions.ChangeShutterState] = new MethodInformation
            {
                MethodHandle = ChangeState,
            };
            _methodMap[CommandDefinitions.ChangeShutterStateOnInterval] = new MethodInformation
            {
                MethodHandle = ChangeStateOnInterval,
            };

            Devices = new Dictionary<char, BaseShutterDevice>();
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
        public override BaseController GetVirtualCopy()
        {
            var virtualCopy = new ShutterController_Virtual(Name, _loggerFactory) 
            {
                MasterController = this.MasterController,
            };
            foreach (var (deviceName, device) in Devices)
            {
                virtualCopy.AddDevice(device.GetCopy());
            }
            return virtualCopy;
        }
        public override List<BaseDevice> GetDevices()
        {
            return Devices.Values.Cast<BaseDevice>().ToList();
        }

        protected override Task UpdateDeviceProperty(Command command, SemaphoreSlim slim)
        {
            if (command.Parameters is UpdateDevicePropertyParameters parameters && Devices.TryGetValue(parameters.DeviceName, out BaseShutterDevice device))
            {
                PropertyInfo propertyInfo = device.GetType().GetProperty(parameters.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (propertyInfo == null)
                {
                    throw new Exception($"Property {parameters.PropertyName} not found on device {device.GetType().Name}.");
                }

                Type propertyType = propertyInfo.PropertyType;
                object convertedValue = null;
                var propertyValue = parameters.PropertyValue;

                // Handle known type conversions manually
                if (propertyType == typeof(float) && propertyValue.GetType() == typeof(int))
                {
                    convertedValue = Convert.ToSingle(propertyValue);
                }
                else if (propertyType.IsAssignableFrom(propertyValue.GetType()))
                {
                    // Direct assignment
                    convertedValue = propertyValue;
                }
                else
                {
                    // Use TypeDescriptor for other conversions
                    TypeConverter typeConverter = TypeDescriptor.GetConverter(propertyType);
                    if (typeConverter != null && typeConverter.CanConvertFrom(propertyValue.GetType()))
                    {
                        convertedValue = typeConverter.ConvertFrom(propertyValue);
                    }
                }

                // Check if conversion was successful
                if (convertedValue != null)
                {
                    propertyInfo.SetValue(device, convertedValue);
                }
            }
            else
            {
                _logger.LogError($"Unable to perform device property update.");
                throw new Exception($"Unable to perform device property update.");
            }

            return Task.CompletedTask;
        }
        protected override Task ConnectDevice(Command command, SemaphoreSlim semaphore)
        {
            if (command.Parameters is ConnectDevicesParameters connectDevicesParameters)
            {
                var deviceNames = connectDevicesParameters.Devices;
                foreach (var deviceName in deviceNames)
                {

                    var device = Devices[deviceName];
                    ConnectDevice_implementation(device);
                    device.IsConnected = true;
                }
            }

            return Task.CompletedTask;
        }
        protected virtual async Task ChangeState(Command command, SemaphoreSlim semaphore)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            if (command.Parameters is ChangeShutterStateParameters parameters)
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    var device = devices[i];
                    var state = parameters.State;

                    await ChangeState_implementation(device, state);
                }
            }
        }
        protected virtual async Task ChangeStateOnInterval(Command command, SemaphoreSlim semaphore)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            if (command.Parameters is ChangeShutterStateForIntervalParameters parameters)
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    var device = devices[i];
                    var duration = parameters.Duration;

                    await ChangeStateOnInterval_implementation(device, duration);
                }
            }
        }
        protected override async Task Stop(Command command, SemaphoreSlim semaphore)
        {
            foreach(var (deviceName, device) in Devices)
            {
                await ChangeState_implementation(device, false);
            }
        }
        protected override abstract Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore);

        protected abstract Task ConnectDevice_implementation(BaseDevice device);
        protected abstract Task ChangeState_implementation(BaseShutterDevice device, bool wantedState);
        protected abstract Task ChangeStateOnInterval_implementation(BaseShutterDevice device, float duration);





    }
}
