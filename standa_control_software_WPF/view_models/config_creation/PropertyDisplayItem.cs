using System.ComponentModel;
using System.Reflection;

namespace standa_control_software_WPF.view_models.config_creation
{
    public class PropertyDisplayItem : ViewModelBase
    {
        private object? _propertyValue;
        public required string PropertyName { get; set; }
        public object? PropertyValue
        {
            get => _propertyValue;
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

                if (PropertyName == "Name")
                {
                    OnNamePropertyChanged?.Invoke();
                }
            }
        }

        public Type? PropertyType { get; set; }
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
        public Action? OnNamePropertyChanged { get; set; }
    }

}