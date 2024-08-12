using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.custom_functions.helpers;
using text_parser_library;
using standa_controller_software.device_manager.devices;

namespace standa_controller_software.custom_functions
{
    public class MoveAbsolutePositionFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private CommandManager _commandManager;
        private ControllerManager _controllerManager;

        public MoveAbsolutePositionFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
        }

        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out char[] devNames, out float[] positions))
                throw new ArgumentException("Argument pasrsing was unsuccesfull. Wrong types.");

            Command[] commandsMovementParameters = new Command[devNames.Length];
            Command[] commandsMovement = new Command[devNames.Length];
            Command[] commandsWaitForStop = new Command[devNames.Length];
            var trajectorySpeed = 100f;


            CustomFunctionHelper.GetLineKinParameters(devNames, positions, trajectorySpeed, _controllerManager, out float[] speedValuesOut, out float[] accelValuesOut, out float[] decelValuesOut, out float allocatedTime);

            for (int i = 0; i < devNames.Length; i++)
            {
                if (float.IsNaN(speedValuesOut[i]) || float.IsNaN(accelValuesOut[i]) || float.IsNaN(decelValuesOut[i]))
                    throw new ArgumentNullException("Failed to create kinematics parameters");
                var controller = _controllerManager.GetDeviceController<BasePositionerController>(devNames[i].ToString());

                commandsMovementParameters[i] =
                    new Command()
                    {
                        Action = "UpdateMoveSettings",
                        Await = true,
                        Parameters = [speedValuesOut[i], accelValuesOut[i], decelValuesOut[i]],
                        TargetController = controller.Name,
                        TargetDevice = devNames[i].ToString()
                    };
            }

            for (int i = 0; i< devNames.Length; i++) {

                var controller = _controllerManager.GetDeviceController<BasePositionerController>(devNames[i].ToString());

                commandsMovement[i] =
                    new Command()
                    {
                        Action = "MoveAbsolute",
                        //Await = i == devNames.Length - 1,
                        Await = true,
                        Parameters = [positions[i]],
                        TargetController = controller.Name,
                        TargetDevice = devNames[i].ToString()
                    };
            }


            for (int i = 0; i < devNames.Length; i++)
            {
                if (float.IsNaN(speedValuesOut[i]) || float.IsNaN(accelValuesOut[i]) || float.IsNaN(decelValuesOut[i]))
                    throw new ArgumentNullException("Failed to create kinematics parameters");
                var controller = _controllerManager.GetDeviceController<BasePositionerController>(devNames[i].ToString());

                commandsWaitForStop[i] =
                    new Command()
                    {
                        Action = "WaitUntilStop",
                        Await = true,
                        Parameters = [],
                        TargetController = controller.Name,
                        TargetDevice = devNames[i].ToString()
                    };
            }

            _commandManager.EnqueueCommandLine(commandsMovementParameters);
            _commandManager.EnqueueCommandLine(commandsMovement);
            _commandManager.EnqueueCommandLine(commandsWaitForStop);

            _commandManager.ExecuteCommandLine(commandsMovementParameters);
            _commandManager.ExecuteCommandLine(commandsMovement);
            _commandManager.ExecuteCommandLine(commandsWaitForStop);


            return null;
        }

        public bool TryParseArguments(object?[] arguments, out char[] devNames, out float[] positions)
        {
            var firstArg = string.Empty; // Default value
            devNames = Array.Empty<char>();
            positions = Array.Empty<float>(); // Default value

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

            positions = floatList.ToArray(); // Convert the list to an array
            devNames = firstArg.ToCharArray();

            return true; // Successfully parsed all arguments
        }

    }
}

//// With laser On or OFF
/// I could try to group execution into laser on groups. and work like that.
/// Move(xyz, 10, 10, 10) LASER ON;
/// Move(xyz, 20, 20, 20) 
/// Move(xyz, 10, 10, 10)
/// Move(xyz, 20, 20, 20) 
/// Move(xyz, 10, 10, 10)
/// Move(xyz, 20, 20, 20) 
/// Move(xyz, 10, 10, 10) LASER OFF;
/// then the laser on could be synced. 
/// I could count the move start. and try to time the laser off.
/// I could wait until finished and just turn off.
/// 


/// #define SYNCOUT STATE 0x02
///When output state is fixed by negative SYNCOUT ENABLED flag, the pin state is in accordance with this flag state
/// y.add_action(10,1);
/// x.add_action(10,1);
/// z.add_action(10,1);
/// x.add_action(0,1);
/// y.add_action(0,1);
/// z.add_action(0,1);
/// go.
/// z2.sync_out = on;
/// z2.sync_out = off;
