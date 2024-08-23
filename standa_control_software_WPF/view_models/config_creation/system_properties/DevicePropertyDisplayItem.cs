using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace standa_control_software_WPF.view_models.config_creation.system_properties
{
    public class DevicePropertyDisplayItem : ViewModelBase
    {
        private object _propertyValue;

        public bool isDynamic { get; set; }
        public PropertyInfo PropertyInformation { get; set; } // Represents the actual property info
        public BaseDevice DeviceReference { get; set; } // Reference to the _device object

        public string PropertyName { get; set; }
        public object PropertyValue
        {
            get 
            {
                if (PropertyInformation != null && DeviceReference != null)
                {
                    _propertyValue = PropertyInformation.GetValue(DeviceReference);
                }
                return _propertyValue;
            }
            set
            {
                _propertyValue = value;
                // Check if the new value is already of the correct type, including handling non-null values of the same type.
                if (PropertyType != null && value != null && PropertyType.IsAssignableFrom(value.GetType()))
                {
                    _propertyValue = value;
                    PropertyMessage = string.Empty; // Clear any previous error message
                }
                else if (PropertyType != null && value != null)
                {
                    var converter = TypeDescriptor.GetConverter(PropertyType);
                    if (converter != null && converter.CanConvertFrom(value.GetType()))
                    {
                        try
                        {
                            var convertedValue = converter.ConvertFrom(value);
                            _propertyValue = convertedValue;
                            if (PropertyInformation != null && DeviceReference != null)
                            {
                                // Use the convertedValue from your conversion logic above
                                PropertyInformation.SetValue(DeviceReference, _propertyValue);
                            }
                            PropertyMessage = string.Empty; // Clear the message on successful conversion
                        }
                        catch (Exception)
                        {
                            PropertyMessage = $"Expected input of type {PropertyType.Name}";
                            // Optionally, don't update _propertyValue if conversion fails to keep the last valid value
                        }
                    }
                    else
                    {
                        PropertyMessage = $"Cannot convert from {value.GetType().Name} to {PropertyType.Name}";
                        // Similarly, decide if you want to keep the last valid value or not
                    }
                }
                else
                {
                    // If there's no specific type information, just assign the value directly.
                    _propertyValue = value;
                    PropertyMessage = string.Empty;
                }

                OnPropertyChanged(nameof(PropertyValue));
                OnPropertyChanged(nameof(PropertyMessage));
            }
        }

        public Type PropertyType { get; set; }
        private string _propertyMessage = string.Empty;
        public string PropertyMessage
        {
            get => _propertyMessage;
            set
            {
                _propertyMessage = value;
                OnPropertyChanged(nameof(PropertyMessage));
            }
        }
    }
}
