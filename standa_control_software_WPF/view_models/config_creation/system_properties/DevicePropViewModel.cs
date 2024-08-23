
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.devices;
using System.Collections.ObjectModel;
using System.Reflection;

namespace standa_control_software_WPF.view_models.config_creation.system_properties
{
    public class DevicePropViewModel : ViewModelBase
    {
        private readonly ControllerManager _systemConfig;
        public readonly IDevice _device;

        public ObservableCollection<DevicePropertyDisplayItem> DeviceProperties { get; } = new ObservableCollection<DevicePropertyDisplayItem>();
        public string Name { get; set; }

        public string IsConnectedText
        {
            get
            {
                //if (_device.IsConnected)
                if (true)
                        return "Connected";
                return "Disconnected";
            }
        }


        public string DeviceType
        {
            get
            {
                var controller = _systemConfig.GetDeviceController<IController>(_device.Name);

                //var deviceType = DeviceDefinitions.AvailableControllers
                //    .FirstOrDefault(controllerDef => controllerDef.Type == controller.GetType())?.AllowedDevices?
                //    .FirstOrDefault(dev => dev.Type == _device.GetType())?.Name;

                var deviceType = DeviceDefinitionLibrary.ControllerDefinitions.GetAllControllerTypes()
                        .First(controllerInfo => controllerInfo.Type == controller.GetType())
                        .AllowedDevices.FirstOrDefault(deviceinfo => deviceinfo.Type == _device.GetType())
                        .Name;

                if (deviceType is null)
                    deviceType = string.Empty;

                return deviceType;
            }
        }

        public string DeviceControllerName
        {
            get
            {
                string controllerName = "";
                if (_device != null && _systemConfig != null)
                {
                    controllerName = _systemConfig.GetDeviceController<IController>(_device.Name).Name;
                }
                return controllerName;
            }
        }


        public DevicePropViewModel(ControllerManager systemConfig, IDevice device)
        {
            _systemConfig = systemConfig;
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

        internal void Connect()
        {
            var controller = _systemConfig.GetDeviceController<IController>(_device.Name);
            //controller.ConnectDevice(_device.Name).GetAwaiter().GetResult();
            OnPropertyChanged(nameof(IsConnectedText));
        }
    }
}
