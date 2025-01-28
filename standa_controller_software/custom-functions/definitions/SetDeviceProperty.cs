using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library.Common;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.devices;
using System.ComponentModel;
using System.Reflection;
using text_parser_library;

namespace standa_controller_software.custom_functions.definitions
{
    public class SetDeviceProperty : CustomFunction
    {
        public string Message { get; set; } = "";
        private readonly ControllerManager _controllerManager;
        private readonly CommandManager _commandManager;

        public SetDeviceProperty(ControllerManager controllerManager, CommandManager commandManager)
        {
            _controllerManager = controllerManager;
            _commandManager = commandManager;
        }

        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out char[] deviceNames, out string propertyName, out object? propertyValue))
                throw new ArgumentException("Argument pasrsing was unsuccesfull. Wrong types.");

            if (propertyValue is null)
                throw new ArgumentNullException("Property value can not be null.");

            ExecuteCore(deviceNames, propertyName, propertyValue);

            return null;
        }

        public void ExecuteCore(char[] deviceNames, string propertyName, object propertyValue)
        {
            var CommandLine = new List<Command>();

            foreach (char deviceName in deviceNames)
            {
                // Find the device by name. This step depends on how your devices are stored and identified.
                if (!_controllerManager.TryGetDevice<BaseDevice>(deviceName, out BaseDevice device))
                        throw new Exception($"Failed to set property {propertyName} on device {deviceName}");

                // Get the type of the device
                Type deviceType = device.GetType();

                // Try to get the property by name
                var propertyInfo = device.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (propertyInfo == null)
                {
                    throw new Exception($"Property {propertyName} not found on device {device.GetType().Name}.");
                }

                try
                {
                    Type propertyType = propertyInfo.PropertyType;
                    object? convertedValue = null;

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
                        if (device is BasePositionerDevice positioner)
                            positioner.UpdatePending = true;

                        if(_controllerManager.TryGetDeviceController<BaseController>(device.Name, out BaseController controller))
                        {
                            var commandParameters = new UpdateDevicePropertyParameters
                            {
                                DeviceName = device.Name,
                                PropertyName = propertyName,
                                PropertyValue = convertedValue
                            };

                            var command = new Command
                            {
                                Action = CommandDefinitions.UpdateDeviceProperty,
                                Await = true,
                                Parameters = commandParameters,
                                TargetController = controller.Name,
                                EstimatedTime = 0,
                                TargetDevices = [device.Name],
                            };

                            CommandLine.Add(command);
                        }
                        else
                        {
                            throw new Exception($"Cannot Get controller instance for device {device.Name}, {propertyValue.GetType().Name} to {propertyType.Name} for property {propertyName}.");
                        }
                    }
                    else
                    {
                        throw new Exception($"Cannot convert type {propertyValue.GetType().Name} to {propertyType.Name} for property {propertyName}.");
                    }
                    
                }
                catch
                {
                    throw;
                }
            }

            if(CommandLine.Count > 0)
            {
                _commandManager.EnqueueCommandLine(CommandLine.ToArray());
                _commandManager.TryExecuteCommandLine(CommandLine.ToArray()).GetAwaiter().GetResult();
            }
        }



        public bool TryParseArguments(object?[] arguments, out char[] deviceNames, out string propertyName, out object? propertyValue)
        {
            deviceNames = Array.Empty<char>(); // Default value
            propertyName = string.Empty;
            propertyValue = null;

            if (arguments == null || arguments.Length == 0)
            {
                return false; // No arguments to parse
            }

            // Parse the first argument as string
            if (arguments[0] is string firstString)
            {
                deviceNames = firstString.ToArray();
            }
            else if (arguments[0] != null) // Check for non-string and non-null first argument
            {
                return false; // First argument is not a string or is null
            }

            if (arguments[1] is string secondString)
            {
                propertyName = secondString;
            }
            else if (arguments[1] != null) // Check for non-string and non-null first argument
            {
                return false; // First argument is not a string or is null
            }

            propertyValue = arguments[2];
            
            return true; // Successfully parsed all arguments
        }

    }
}
