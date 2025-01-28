using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library.Common;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.positioning
{
    public abstract class BasePositionerController : BaseController
    {

        protected ConcurrentDictionary<char, CancellationTokenSource> deviceCancellationTokens = new();
        protected Dictionary<char, BasePositionerDevice> Devices { get; }


        public BasePositionerController(string name, ILoggerFactory loggerFactory) : base(name, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<BasePositionerController>();

            _methodMap[CommandDefinitions.MoveAbsolute] = new MethodInformation()
            {
                MethodHandle = MoveAbsolute,
            };
            _methodMap[CommandDefinitions.UpdateMoveSettings] = new MethodInformation()
            {
                MethodHandle = UpdateMoveSettings,
            };
            _methodMap[CommandDefinitions.WaitForStop] = new MethodInformation()
            {
                MethodHandle = WaitForStop,
            };
            _methodMap[CommandDefinitions.Home] = new MethodInformation()
            {
                MethodHandle = Home,
            };

            _methodMap[CommandDefinitions.AddSyncInAction] = new MethodInformation()
            {
                MethodHandle = AddSyncInAction,
            };
            _methodMap[CommandDefinitions.GetBufferCount] = new MethodInformation<int>()
            {
                MethodHandle = GetBufferFreeSpace,
            };

            Devices = [];
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
        public override List<BaseDevice> GetDevices()
        {
            return Devices.Values.Cast<BaseDevice>().ToList();
        }
        public override BaseController GetVirtualCopy()
        {
            var virtualController = new PositionerController_Virtual(Name, _loggerFactory)
            {
                MasterController = this.MasterController,
            };
            foreach (var (deviceName,device) in Devices)
            {
                virtualController.AddDevice(device.GetCopy());
            }

            return virtualController;
        }

        protected override Task UpdateDeviceProperty(Command command, SemaphoreSlim slim) 
        {
            if (command.Parameters is UpdateDevicePropertyParameters parameters && Devices.TryGetValue(parameters.DeviceName, out var device))
            {
                PropertyInfo? propertyInfo = device.GetType().GetProperty(parameters.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (propertyInfo == null)
                {
                    throw new Exception($"Property {parameters.PropertyName} not found on device {device.GetType().Name}.");
                }

                Type propertyType = propertyInfo.PropertyType;
                object? convertedValue = null;
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
                    try
                    {
                        ConnectDevice_implementation(device);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error encountered when trying to connect {device.Name} device. \n{ex.Message}");
                    }
                    device.IsConnected = true;
                }
            }

            return Task.CompletedTask;
        }
        protected override abstract Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore);
        protected abstract Task MoveAbsolute(Command command, SemaphoreSlim semaphore);
        protected abstract Task Home(Command command, SemaphoreSlim semaphore);
        protected async Task WaitForStop(Command command, SemaphoreSlim semaphore)
        {

            var devicesToAwait = command.TargetDevices.ToList();
            while (devicesToAwait.Count > 0)
            {
                List<char> devicesToRemove = [];
                foreach (var deviceName in devicesToAwait)
                {
                    var device = Devices[deviceName];
                    bool isStationary = await IsDeviceStationary(device);
                    if (isStationary)
                        devicesToRemove.Add(deviceName);
                }
                foreach (var item in devicesToRemove)
                {
                    devicesToAwait.Remove(item);
                }
            }
        }
        protected abstract Task<bool> IsDeviceStationary(BasePositionerDevice device);
        protected abstract Task UpdateMoveSettings(Command command, SemaphoreSlim semaphore);
        protected abstract void ConnectDevice_implementation(BaseDevice device);

        protected abstract Task AddSyncInAction(Command command, SemaphoreSlim semaphore);
        protected abstract Task<int> GetBufferFreeSpace(Command command, SemaphoreSlim semaphore);
    }
}
