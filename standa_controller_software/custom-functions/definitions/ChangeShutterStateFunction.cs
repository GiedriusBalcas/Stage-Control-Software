using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_parser_library;

namespace standa_controller_software.custom_functions.definitions
{
    public class ChangeShutterStateFunction : CustomFunction
    {

        private CommandManager _commandManager;
        private ControllerManager _controllerManager;

        public ChangeShutterStateFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
        }

        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out char devName, out bool turnOn))
                throw new ArgumentException("Argument pasrsing was unsuccesfull. Wrong types.");

            if (!_controllerManager.TryGetDevice<BaseShutterDevice>(devName, out var shutterDevice))
                throw new Exception($"Unable to retrieve shutter device named {devName}.");
            
            if (!_controllerManager.TryGetDeviceController<BaseShutterController>(devName, out var shutterController))
                throw new Exception($"Unable to retrieve shutter controller of device named {devName}.");


            Command[] commandLine=
            [
                new Command()
                {
                    Action = CommandDefinitionsLibrary.ChangeShutterState.ToString(),
                    Await = true,
                    Parameters = [[turnOn]],
                    TargetController = shutterController.Name,
                    TargetDevices = [devName]
                },
            ];

            _commandManager.EnqueueCommandLine(commandLine);

            return null;
        }

        public bool TryParseArguments(object?[] arguments, out char deviceName, out bool isEngaged)
        {
            isEngaged = false;
            deviceName = '\0';

            if (arguments == null || arguments.Length != 2)
            {
                return false; // No arguments to parse
            }

            if (arguments[0] is string str)
            {
                deviceName = str[0];
            }
            else if (arguments[0] is char chr)
            {
                deviceName = chr;
            }
            else if (arguments[0] != null) // Check for non-string and non-null first argument
            {
                return false; // First argument is not a string or is null
            }

            // Attempt to parse each object as float
            if (arguments[1] is float f)
            {
                isEngaged = f == 0 ? false : true;
            }
            else if (arguments[1] is double d) // Handle double to float conversion
            {
                isEngaged = d == 0 ? false : true;
            }
            else if (arguments[1] is int integer) // Handle int to float conversion
            {
                isEngaged = integer == 1 ? false : true;
            }
            else if (arguments[1] is bool boolien) // Handle int to float conversion
            {
                isEngaged = boolien;
            }
            else
            {
                // Try to parse as float from string or other convertible types
                object? arg = arguments[1];
                if (arg != null && float.TryParse(arg.ToString(), out float parsedFloat))
                {
                    isEngaged = parsedFloat == 0 ? false : true;
                }
                else if (arg.ToString().ToLower() == "on" || arg.ToString().ToLower() == "off")
                {
                    if (arg.ToString().ToLower() == "on")
                        isEngaged = true;
                    else
                        isEngaged = false;
                }
                else
                    return false;
            }
            return true; // Successfully parsed all arguments
        }
    }
}
