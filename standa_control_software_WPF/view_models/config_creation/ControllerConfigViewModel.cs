
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.controller_interfaces;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.config_creation
{
    public class ControllerConfigViewModel : ViewModelBase
    {
        private readonly ConfigurationViewModel _config;
        private string _selectedControllerType;

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
                foreach (var device in Devices)
                    device.IsEnabled = _isEnabled;
            }
        }

        private string _name;
        public string Name
        {
            get
            {
                var nameProp = ControllerProperties.FirstOrDefault(prop => prop.PropertyName == "Name");
                if (nameProp != null && nameProp.PropertyValue is string stringVal)
                    if (stringVal != "")
                    {
                        _name = stringVal;
                        return _name;
                    }

                _name = "Undefined Controller";
                return _name;
            }
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public Type ControllerType { get; private set; }
        public ObservableCollection<DeviceConfigViewModel> Devices { get; set; } = new ObservableCollection<DeviceConfigViewModel>();
        public ObservableCollection<PropertyDisplayItem> ControllerProperties { get; } = new ObservableCollection<PropertyDisplayItem>();

        public string SelectedControllerType { 
            get => _selectedControllerType; 
            set 
            {
                _selectedControllerType = value;
                if(_selectedControllerType != null)
                    ControllerType = DeviceDefinitionLibrary.ControllerDefinitions.GetAllControllerTypes().First(controllerInfo => controllerInfo.Name == _selectedControllerType).Type;

                OnPropertyChanged(nameof(SelectedControllerType));
                GetProperties();
            } 
        }
        // List of available controller types are held in Model/definitions-library
        public ObservableCollection<string> ControllerTypes { get; } = new ObservableCollection<string>(
            DeviceDefinitionLibrary.ControllerDefinitions.GetAllControllerTypes()
            .Select(controllerInfo => controllerInfo.Name)
            .ToList()
        );

        public ICommand AddDeviceCommand { get; private set; }
        public ICommand RemoveControllerCommand { get; private set; }


        public ControllerConfigViewModel(ConfigurationViewModel config)
        {
            _config = config;

            ControllerProperties.Clear();

            AddDeviceCommand = new RelayCommand<ControllerConfigViewModel>(ExecuteAddDevice, CanExecuteAddDevice);
            RemoveControllerCommand = new RelayCommand<ControllerConfigViewModel>(ExecuteRemoveController);

            GetProperties();
        }

        private bool CanExecuteAddDevice(ControllerConfigViewModel obj)
        {
            if(this.ControllerType is null)
                return false;
            return true;
        }

        public void GetProperties()
        {
            if (ControllerType != null)
            {
                var ControllerPropertiesNew = new List<PropertyDisplayItem>();

                var propertiesAll = ControllerType.GetProperties();
                var properties = ControllerType.GetProperties()
                    .Where(prop => prop.GetCustomAttribute<DisplayPropertyAttribute>() != null);

                foreach (var property in properties)
                {
                    var propValue = property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;

                    var existingProp = ControllerProperties.FirstOrDefault(prop => prop.PropertyName == property.Name && prop.PropertyType == property.PropertyType && prop.PropertyValue != null);
                    
                    if (existingProp != null)
                    {
                        propValue = existingProp.PropertyValue;
                    }
                    
                    var propItem = new PropertyDisplayItem
                    {
                        OnNamePropertyChanged = () => OnPropertyChanged(nameof(Name)),
                        PropertyName = property.Name,
                        PropertyValue = propValue,
                        PropertyType = property.PropertyType,
                    };
                    ControllerPropertiesNew.Add(propItem);
                }

                ControllerProperties.Clear();
                ControllerPropertiesNew.ForEach(propItem => ControllerProperties.Add(propItem));

                UpdatePropertyValue("Name", this.Name);
                OnPropertyChanged(nameof(Name));
            }

        }


        public bool UpdatePropertyValue(string propertyName, object newValue)
        {
            var propertyItem = ControllerProperties.FirstOrDefault(p => p.PropertyName == propertyName);
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


        private void ExecuteAddDevice(ControllerConfigViewModel controllerVM)
        {
            Devices.Add(new DeviceConfigViewModel(this));
        }

        private void ExecuteRemoveController(ControllerConfigViewModel controllerVM)
        {
            _config.Controllers.Remove(this);
        }



        public IController ExtractController()
        {
            if (ControllerType == null)
            {
                throw new InvalidOperationException("Controller type is not set.");
            }

            // Assuming Name and ID are always required and available
            var nameProp = ControllerProperties.FirstOrDefault(p => p.PropertyName == "Name")?.PropertyValue;
            
            // Convert property values to expected types
            string name = nameProp != null ? Convert.ToString(nameProp) : "undefined"; // Default to 'u' if not found


            // Create the controller instance using reflection with parameters
            //var constructorInfo = ControllerType.GetConstructor(new Type[] { });
            //if (constructorInfo == null)
            //{
            //    throw new InvalidOperationException("Suitable constructor not found.");
            //}

            //var controllerInstance = constructorInfo.Invoke(new object[] { }) as IController;

            var controllerInstance = Activator.CreateInstance(ControllerType) as IController;


            if (controllerInstance == null)
            {
                throw new InvalidOperationException($"Could not create an instance of {ControllerType}.");
            }

            foreach (var propItem in ControllerProperties)
            {
                var propInfo = ControllerType.GetProperty(propItem.PropertyName);
                if (propInfo != null && propInfo.CanWrite)
                {
                    try
                    {
                        // Convert the PropertyValue to the correct type and set it
                        var value = Convert.ChangeType(propItem.PropertyValue, propInfo.PropertyType);
                        propInfo.SetValue(controllerInstance, value);
                    }
                    catch (Exception ex)
                    {
                        // Handle or log the error
                        System.Diagnostics.Debug.WriteLine($"Failed to set property {propItem.PropertyName}: {ex.Message}");
                    }
                }
            }


            // Additional property setting can be performed here if necessary

            return controllerInstance;
        }



    }
}
