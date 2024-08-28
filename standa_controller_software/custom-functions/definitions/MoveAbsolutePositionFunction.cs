using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.custom_functions.helpers;
using text_parser_library;
using standa_controller_software.device_manager.devices;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using System.ComponentModel;
using System.Xml.Linq;

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
            this.SetProperty("Speed", 1000f);
            this.SetProperty("Shutter", false);
        }

        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out char[] deviceNames, out float[] positions, out float[] waitUntil))
                throw new ArgumentException("Argument pasrsing was unsuccesfull. Wrong types.");

            List<Command> commandsMovementParameters = new List<Command>();
            List<Command> commandsMovement = new List<Command>();
            List<Command> commandsWaitForStop = new List<Command>();

            //if (this.TryGetProperty("Shutter", out object isOn))
            //    commandsMovement = (bool)isOn ? new Command[deviceNames.Length + 1] : new Command[deviceNames.Length];
            //else
            //    commandsMovement = new Command[deviceNames.Length];


            this.TryGetProperty("Speed", out object trajSpeed);

            float trajectorySpeed = 1000f; // Default value

            if (trajSpeed != null)
            {
                if (trajSpeed is float)
                {
                    trajectorySpeed = (float)trajSpeed > 0 ? (float)trajSpeed : 1000f;
                }
                else if (trajSpeed is int)
                {
                    trajectorySpeed = (int)trajSpeed > 0 ? Convert.ToSingle(trajSpeed) : 1000f;
                }
            }

            var positionerMovementInformations = deviceNames.ToDictionary(name => name, name => new PositionerMovementInformation() { TargetPosition = positions[Array.IndexOf(deviceNames, name)] });

            if (!CustomFunctionHelper.TryGetLineKinParameters(positionerMovementInformations, trajectorySpeed, _controllerManager, out float allocatedTime))
                throw new Exception("Failed to create line kinematic parameters");

            var devices = deviceNames
                .Select(name =>
                {
                    _controllerManager.TryGetDevice(name, out BasePositionerDevice device);
                    return device;
                })
                .Where(device => device != null)
                .ToList();

            var controllers = devices
                .ToDictionary(device => device, device =>
                {
                    _controllerManager.TryGetDeviceController<BasePositionerController>(device.Name, out BasePositionerController controller);
                    return controller;
                });

            var groupedDevicesByController = devices
                .GroupBy(device => controllers[device])
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();
                var controlerName = controllerGroup.Key.Name;

                object[][] parameters = new object[groupedDeviceNames.Length][];

                for (int i = 0; i < groupedDeviceNames.Length; i++)
                {
                    var deviceName = groupedDeviceNames[i];
                    parameters[i] = new object[]
                    {
                        positionerMovementInformations[deviceName].TargetSpeed < 10 ? 10 : positionerMovementInformations[deviceName].TargetSpeed,
                        positionerMovementInformations[deviceName].TargetAcceleration,
                        positionerMovementInformations[deviceName].TargetDeceleration
                    };
                }

                commandsMovementParameters.Add(
                    new Command()
                    {
                        Action = "UpdateMoveSettings",
                        Await = true,
                        Parameters = parameters,
                        TargetController = controlerName,
                        TargetDevices = groupedDeviceNames
                    }
                );
            }

            _commandManager.EnqueueCommandLine(commandsMovementParameters.ToArray());

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();
                var controlerName = controllerGroup.Key.Name;

                object[][] parameters = new object[groupedDeviceNames.Length][];

                for (int i = 0; i < groupedDeviceNames.Length; i++)
                {
                    var deviceName = groupedDeviceNames[i];
                    parameters[i] =
                    [
                        positionerMovementInformations[deviceName].TargetPosition
                    ];
                }

                commandsMovement.Add(
                    new Command()
                    {
                        Action = CommandDefinitionsLibrary.MoveAbsolute.ToString(),
                        Await = false,
                        Parameters = parameters,
                        TargetController = controlerName,
                        TargetDevices = groupedDeviceNames
                    }
                );
            }

            _commandManager.EnqueueCommandLine(commandsMovement.ToArray());

            //if ((bool)isOn)
            //{
            //    var shutterDevice = _controllerManager.GetDevices<BaseShutterDevice>().FirstOrDefault();
            //    if (shutterDevice is null)
            //        throw new Exception("Trying to use non existing shutter device");

            //    var controller = _controllerManager.GetDeviceController<BaseShutterController>(shutterDevice.Name);

            //    commandsMovement[commandsMovement.Length - 1] =
            //        new Command()
            //        {
            //            Action = CommandDefinitionsLibrary.ChangeShutterStateOnInterval.ToString(),
            //            Await = false,
            //            Parameters = [allocatedTime],
            //            TargetController = controller.Name,
            //            TargetDevice = shutterDevice.Name
            //        };

            //}

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();
                var controlerName = controllerGroup.Key.Name;

                object[][] parameters = new object[groupedDeviceNames.Length][];
                if(waitUntil.Length == deviceNames.Length)
                    for (int i = 0; i < groupedDeviceNames.Length; i++)
                    {
                        var deviceName = groupedDeviceNames[i];
                        bool direction = positionerMovementInformations[deviceName].TargetPosition > positionerMovementInformations[deviceName].CurrentPosition;
                        float targetPosition = direction
                            ? positionerMovementInformations[deviceName].TargetPosition - waitUntil[i]
                            : positionerMovementInformations[deviceName].TargetPosition + waitUntil[i]
                            ;
                        parameters[i] =
                        [
                             targetPosition, direction
                        ];
                    }

                commandsWaitForStop.Add(
                    new Command()
                    {
                        Action = CommandDefinitionsLibrary.WaitUntilStop.ToString(),
                        Await = true,
                        Parameters = parameters,
                        TargetController = controlerName,
                        TargetDevices = groupedDeviceNames
                    }
                );
            }

            _commandManager.EnqueueCommandLine(commandsWaitForStop.ToArray());

            _commandManager.ExecuteCommandLine(commandsMovementParameters.ToArray()).GetAwaiter().GetResult();
            _commandManager.ExecuteCommandLine(commandsMovement.ToArray()).GetAwaiter().GetResult();
            _commandManager.ExecuteCommandLine(commandsWaitForStop.ToArray()).GetAwaiter().GetResult();


            return null;
        }

        public bool TryParseArguments(object?[] arguments, out char[] devNames, out float[] positions, out float[] waitUntil)
        {
            var firstArg = string.Empty; // Default value
            devNames = Array.Empty<char>();
            positions = Array.Empty<float>(); // Default value
            waitUntil = Array.Empty<float>(); // Default value

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
            
            devNames = firstArg.ToCharArray();

            // Start with an empty list to collect the float values
            var positionsList = new List<float>();

            // Start from index 1 since index 0 is the string argument
            for (int i = 1; i < 1 + devNames.Length; i++)
            {
                // Attempt to parse each object as float
                if (arguments[i] is float f)
                {
                    positionsList.Add(f);
                }
                else if (arguments[i] is double d) // Handle double to float conversion
                {
                    positionsList.Add((float)d);
                }
                else if (arguments[i] is int integer) // Handle int to float conversion
                {
                    positionsList.Add(integer);
                }
                else
                {
                    // Try to parse as float from string or other convertible types
                    object? arg = arguments[i];
                    if (arg != null && float.TryParse(arg.ToString(), out float parsedFloat))
                    {
                        positionsList.Add(parsedFloat);
                    }
                    else
                    {
                        // Could not parse argument as float
                        return false;
                    }
                }
            }

            positions = positionsList.ToArray(); // Convert the list to an array

            var waitUntilList = new List<float>();

            if(arguments.Length == 1 + positions.Length * 2)
                for (int i = 1 + positions.Length; i < arguments.Length; i++)
                {
                    // Attempt to parse each object as float
                    if (arguments[i] is float f)
                    {
                        waitUntilList.Add(f);
                    }
                    else if (arguments[i] is double d) // Handle double to float conversion
                    {
                        waitUntilList.Add((float)d);
                    }
                    else if (arguments[i] is int integer) // Handle int to float conversion
                    {
                        waitUntilList.Add(integer);
                    }
                    else
                    {
                        // Try to parse as float from string or other convertible types
                        object? arg = arguments[i];
                        if (arg != null && float.TryParse(arg.ToString(), out float parsedFloat))
                        {
                            waitUntilList.Add(parsedFloat);
                        }
                        else
                        {
                            // Could not parse argument as float
                            return false;
                        }
                    }
                }

            waitUntil = waitUntilList.ToArray(); // Convert the list to an array

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
