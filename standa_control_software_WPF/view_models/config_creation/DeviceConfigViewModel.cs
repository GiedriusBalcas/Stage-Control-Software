
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.devices;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.config_creation
{
    public partial class DeviceConfigViewModel : ViewModelBase
    {
        private bool _isEnabled;
        public bool IsEnabled
        {
            get 
            {
                if (_controller.IsEnabled is true)
                    return _isEnabled;
                return false;
            }
            set
            {
                if(_controller.IsEnabled is true)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        private string _name;
        public string Name
        {
            get 
            {
                var nameProp = DeviceProperties.FirstOrDefault(prop => prop.PropertyName == "Name");
                if (nameProp != null && nameProp.PropertyValue is string stringVal)
                    if(stringVal != string.Empty)
                        return stringVal;
                return "u";
            }
            set {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public Type DeviceType { get; set; }

        public string _selectedDeviceType;

        private ControllerConfigViewModel _controller;

        public string SelectedDeviceType
        {
            get => _selectedDeviceType;
            set
            {
                _selectedDeviceType = value;
                if(_selectedDeviceType != null 
                    && DeviceDefinitionLibrary.ControllerDefinitions.GetAllControllerTypes().Any(controllerInfo => controllerInfo.Type == _controller.ControllerType)) 
                {
                    var foundType = DeviceDefinitionLibrary.ControllerDefinitions.GetAllControllerTypes()
                        .First(controllerInfo => controllerInfo.Type == _controller.ControllerType)
                        .AllowedDevices.FirstOrDefault(deviceinfo => deviceinfo.Name == _selectedDeviceType)
                        .Type;

                    if (foundType != null)
                    {
                        DeviceType = foundType;
                        GetProperties();
                    }
                }
                OnPropertyChanged(nameof(SelectedDeviceType));
            }
        }
        public ObservableCollection<PropertyDisplayItem> DeviceProperties { get; } = new ObservableCollection<PropertyDisplayItem>();

        public ObservableCollection<string> DeviceTypes => new ObservableCollection<string>( 
            DeviceDefinitionLibrary.ControllerDefinitions.GetAllControllerTypes()
            .First(controllerInfo => controllerInfo.Type == _controller.ControllerType)
            .AllowedDevices.Select(deviceInfo => deviceInfo.Name).ToList() 
            );

        public ICommand RemoveDeviceCommand {  get; set; }


        public DeviceConfigViewModel(ControllerConfigViewModel controller)
        {
            this._controller = controller;
            IsEnabled = controller.IsEnabled;

            RemoveDeviceCommand = new RelayCommand<DeviceConfigViewModel>(ExecuteRemoveDevice);
            DeviceProperties.Clear();
            GetProperties();
        }
        

        public void GetProperties()
        {
            
            if (DeviceType != null)
            {
                var propertiesAll = DeviceType.GetProperties();
                var properties = DeviceType.GetProperties()
                    .Where(prop => prop.GetCustomAttribute<DisplayPropertyAttribute>() != null);

                foreach (var property in properties)
                {
                    object? defaultValue = property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;

                    var propItem = new PropertyDisplayItem
                    {
                        OnNamePropertyChanged = () => OnPropertyChanged(nameof(Name)),
                        PropertyName = property.Name,
                        PropertyValue = defaultValue,
                        PropertyType = property.PropertyType,
                    };
                    
                    //TODO: IF property allready exists, skippidy skip. REMOVE WHATS NOT REQUIRED THOUGH

                    if (!DeviceProperties.Any(prop => prop.PropertyName == property.Name))
                    {
                        DeviceProperties.Add(propItem);
                    }
                }

                UpdatePropertyValue("Name", this.Name);
                OnPropertyChanged(nameof(Name));

            }
        }

        public bool UpdatePropertyValue(string propertyName, object newValue)
        {
            var propertyItem = DeviceProperties.FirstOrDefault(p => p.PropertyName == propertyName);
            if (propertyItem != null && propertyItem.PropertyType != null)
            {
                try
                {
                    // Convert newValue to the correct type
                    var convertedValue = Convert.ChangeType(newValue, propertyItem.PropertyType);
                    propertyItem.PropertyValue = convertedValue;
                    return true; // Update successful
                }
                catch (Exception)
                {
                    // Handle conversion error, e.g., log or notify the user
                    return false; // Update failed
                }
            }
            return false; // Property not found
        }

        public void ExecuteRemoveDevice(DeviceConfigViewModel device)
        {
            _controller.Devices.Remove(device);
        }

        public BaseDevice ExtractDevice(BaseController controller)
        {
            if (DeviceType == null || _controller == null)
            {
                throw new InvalidOperationException("DeviceType or Controller is not set.");
            }

            // Assuming Name and ID are always required and available
            var nameProp = DeviceProperties.FirstOrDefault(p => p.PropertyName == "Name")?.PropertyValue;
            var idProp = DeviceProperties.FirstOrDefault(p => p.PropertyName == "ID")?.PropertyValue;

            // Convert property values to expected types
            char name = nameProp != null ? Convert.ToChar(nameProp) : 'u'; // Default to 'u' if not found
            string id = idProp as string ?? string.Empty; // Default to empty string if not found

            // Fetch the controller - assuming _controller is an instance of IPositionerController or can be cast to it
            //var controller = _controller.ExtractController();

            // Create the device instance using reflection with parameters
            var constructorInfo = DeviceType.GetConstructor(new Type[] { typeof(char), typeof(string) });
            if (constructorInfo == null)
            {
                throw new InvalidOperationException("Suitable constructor not found.");
            }

            var deviceInstance = constructorInfo.Invoke(new object[] { name, id }) as BaseDevice;

            if (deviceInstance == null)
            {
                throw new InvalidOperationException($"Could not create an instance of {DeviceType}.");
            }

            foreach (var propItem in DeviceProperties)
            {
                var propInfo = DeviceType.GetProperty(propItem.PropertyName);
                if (propInfo != null && propInfo.CanWrite)
                {
                    try
                    {
                        // Convert the PropertyValue to the correct type and set it
                        var value = Convert.ChangeType(propItem.PropertyValue, propInfo.PropertyType);
                        propInfo.SetValue(deviceInstance, value);
                    }
                    catch (Exception ex)
                    {
                        // Handle or log the error
                        System.Diagnostics.Debug.WriteLine($"Failed to set property {propItem.PropertyName}: {ex.Message}");
                    }
                }
            }

            return deviceInstance;
        }

    }
}