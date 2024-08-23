using standa_controller_software.command_manager;
using standa_controller_software.custom_functions.helpers;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_parser_library;
using standa_controller_software.device_manager.controller_interfaces.shutter;


namespace standa_controller_software.custom_functions.definitions
{
    public class ShutterOnIntervalFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private CommandManager _commandManager;
        private ControllerManager _controllerManager;

        public ShutterOnIntervalFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
        }

        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out char[] devNames, out float[] durations))
                throw new ArgumentException("Argument pasrsing was unsuccesfull. Wrong types.");

            Command[] commandsChangeStateOnInterval = new Command[devNames.Length];


            for (int i = 0; i < devNames.Length; i++)
            {
                var controller = _controllerManager.GetDeviceController<BaseShutterController>(devNames[i].ToString());

                commandsChangeStateOnInterval[i] =
                    new Command()
                    {
                        Action = CommandDefinitionsLibrary.ChangeShutterStateOnInterval.ToString(),
                        Await = false,
                        Parameters = [durations[i]],
                        TargetController = controller.Name,
                        TargetDevice = devNames[i].ToString()
                    };
            }

            _commandManager.EnqueueCommandLine(commandsChangeStateOnInterval);



            return null;
        }

        public bool TryParseArguments(object?[] arguments, out char[] devNames, out float[] durations)
        {
            var firstArg = string.Empty; // Default value
            devNames = Array.Empty<char>();
            durations = Array.Empty<float>(); // Default value

            if (arguments == null || arguments.Length == 0)
            {
                return false; // No arguments to parse
            }

            // Parse the first argument as string
            if (arguments[0] is string str)
            {
                firstArg = str;
            }
            else if (arguments[0] != null) // Check for non-string and non-null first argument
            {
                return false; // First argument is not a string or is null
            }

            // Initialize the rest of the arguments as a float array
            // Start with an empty list to collect the float values
            var floatList = new List<float>();

            // Start from index 1 since index 0 is the string argument
            for (int i = 1; i < arguments.Length; i++)
            {
                // Attempt to parse each object as float
                if (arguments[i] is float f)
                {
                    floatList.Add(f);
                }
                else if (arguments[i] is double d) // Handle double to float conversion
                {
                    floatList.Add((float)d);
                }
                else if (arguments[i] is int integer) // Handle int to float conversion
                {
                    floatList.Add(integer);
                }
                else
                {
                    // Try to parse as float from string or other convertible types
                    object? arg = arguments[i];
                    if (arg != null && float.TryParse(arg.ToString(), out float parsedFloat))
                    {
                        floatList.Add(parsedFloat);
                    }
                    else
                    {
                        // Could not parse argument as float
                        return false;
                    }
                }
            }

            durations = floatList.ToArray(); // Convert the list to an array
            devNames = firstArg.ToCharArray();

            return true; // Successfully parsed all arguments
        }

    }
}
