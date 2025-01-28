
using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library.Common;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.devices;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;

namespace standa_control_software_WPF.view_models.config_creation.system_properties
{
    public class DevicePropViewModel : ViewModelBase
    {
        private readonly ILogger<DevicePropViewModel> _logger;
        private readonly ControllerManager _controllerManager;
        private readonly standa_controller_software.command_manager.CommandManager _commandManager;
        public readonly BaseDevice _device;

        public ObservableCollection<DevicePropertyDisplayItem> DeviceProperties { get; } = [];
        public char Name { get; set; }

        public string IsConnectedText
        {
            get
            {
                if (_device.IsConnected)
                        return "Connected";
                return "Disconnected";
            }
        }


        public string DeviceType
        {
            get
            {
                var controller = _controllerManager.GetDeviceController<BaseController>(_device.Name);

                //var deviceType = DeviceDefinitions.AvailableControllers
                //    .FirstOrDefault(controllerDef => controllerDef.Type == controller.GetType())?.AllowedDevices?
                //    .FirstOrDefault(dev => dev.Type == _device.GetType())?.Name;

                var deviceType = DeviceDefinitionLibrary.ControllerDefinitions.GetAllControllerTypes()
                        .First(controllerInfo => controllerInfo.Type == controller.GetType())
                        .AllowedDevices.FirstOrDefault(deviceinfo => deviceinfo.Type == _device.GetType())
                        .Name;

                deviceType ??= string.Empty;

                return deviceType;
            }
        }

        public string DeviceControllerName
        {
            get
            {
                string controllerName = "";
                if (_device != null && _controllerManager != null)
                {
                    controllerName = _controllerManager.GetDeviceController<BaseController>(_device.Name).Name;
                }
                return controllerName;
            }
        }


        public DevicePropViewModel(ControllerManager controllerManager, standa_controller_software.command_manager.CommandManager commandManager, BaseDevice device, ILogger<DevicePropViewModel> logger)
        {
            _logger = logger;
            _controllerManager = controllerManager;
            _commandManager = commandManager;
            _device = device;
            Name = _device.Name;
            GetProperties();
        }

        public void GetProperties()
        {
            var deviceType = _device.GetType();

            var properties = deviceType.GetProperties()
                .Where(prop => prop.GetCustomAttribute<DisplayPropertyAttribute>() != null);

            foreach (var property in properties)
            {
                var actualValue = property.GetValue(_device);
                var isDynamic = property.GetCustomAttribute<DynamicPropertyAttribute>() != null; //check for Dynamic attribute

                var propItem = new DevicePropertyDisplayItem
                {
                    DeviceReference = _device,
                    isDynamic = isDynamic,
                    PropertyInformation = property,
                    PropertyName = property.Name,
                    PropertyType = property.PropertyType,
                };
                DeviceProperties.Add(propItem);
            }
            OnPropertyChanged(nameof(Name));
        }

        internal async Task ConnectAsync()
        {
            if (!_device.IsConnected)
            {
                var controller = _controllerManager.GetDeviceController<BaseController>(_device.Name);
                //await controller.ConnectDevice(_device, semaphore);
                var connectCommand = new Command
                {
                    Action = CommandDefinitions.ConnectDevice,
                    TargetController = controller.Name,
                    TargetDevices = [_device.Name],
                    Parameters = new ConnectDevicesParameters
                    {
                        Devices = [_device.Name],
                    }
                };
                try
                {
                    await _commandManager.TryExecuteCommand(connectCommand);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error encountered when trying to connect {_device.Name} device. \n{ex.Message}");
                    MessageBox.Show(ex.Message);

                }

                OnPropertyChanged(nameof(IsConnectedText));
            }
        }
    }
}
