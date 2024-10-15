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
using standa_controller_software.command_manager.command_parameter_library;
using System.Reflection;
using standa_controller_software.device_manager.devices.shutter;

namespace standa_controller_software.custom_functions.definitions
{
    public class JumpAbsoluteFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private CommandManager _commandManager;
        private ControllerManager _controllerManager;

        /// move command will bring:
        ///      traj:
        ///         isLine
        ///      shutter:
        ///          target state
        ///          delay on (not the intrinsic one)
        ///          delay off
        ///      positioner:
        ///          target position
        ///          target speed
        ///          allocated time
        ///          acceleration
        ///          deceleration
        
        public JumpAbsoluteFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            this.SetProperty("Shutter", false);
            this.SetProperty("Accuracy", 0.1f);
        }

        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out char[] parsedDeviceNames, out float[] parsedPositions, out float[] parsedWaitUntil))
                throw new ArgumentException("Argument pasrsing was unsuccesfull. Wrong types.");

            if (!TryGetProperty("Shutter", out object isShutterUsed_o))
                throw new Exception();
            var isShutterUsed = (bool)isShutterUsed_o;
            
            if (!TryGetProperty("Accuracy", out object accuracy_o))
                throw new Exception();
            var accuracy = (float)accuracy_o;

            List<char> deviceNameList = new List<char>();
            List<float> positionsList = new List<float>();
            List<float> waitUntilList = new List<float>();
            for (int i = 0; i < parsedDeviceNames.Length; i++)
            {
                if (_controllerManager.TryGetDevice<BasePositionerDevice>(parsedDeviceNames[i], out var positioner))
                {
                    // Accuracy of the movement?
                    // it should also check if its not moving. Cause if its gonna go out of the position.
                    if ( !( Math.Abs(positioner.CurrentPosition - parsedPositions[i]) < accuracy && positioner.CurrentSpeed > 0 ) )
                    {
                        deviceNameList.Add(parsedDeviceNames[i]);
                        positionsList.Add(parsedPositions[i]);
                        if(parsedWaitUntil.Length > i)
                            waitUntilList.Add(parsedWaitUntil[i]);
                    }
                }
            }
            char[] deviceNames = deviceNameList.ToArray();
            float[] positions = positionsList.ToArray();
            float[] waitUntil = waitUntilList.ToArray();

            if (deviceNames.Length == 0)
                return null;

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


            float allocatedTime = 0f;
            var positionerMovementInformations = new Dictionary<char, PositionerMovementInformation>();

            foreach (char name in deviceNames)
            {
                if (_controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                {
                    positionerMovementInformations[name] = new PositionerMovementInformation
                    {
                        TargetPosition = positions[Array.IndexOf(deviceNames, name)],
                        TargetDistance = Math.Abs(positions[Array.IndexOf(deviceNames, name)] - positioner.CurrentPosition),
                        TargetDirection = positions[Array.IndexOf(deviceNames, name)] > positioner.CurrentPosition,
                        StartingPosition = positioner.CurrentPosition,
                        StartingSpeed = positioner.CurrentSpeed,
                        CurrentTargetSpeed = positioner.Speed,
                        StartingAcceleration = positioner.Acceleration,
                        StartingDeceleration = positioner.Deceleration,
                        MaxAcceleration = positioner.MaxAcceleration,
                        MaxDeceleration = positioner.MaxDeceleration,
                        MaxSpeed = positioner.MaxSpeed,
                        TargetAcceleration = positioner.MaxAcceleration,
                        TargetDeceleration = positioner.MaxDeceleration,
                        TargetSpeed = positioner.DefaultSpeed,
                    };

                    allocatedTime = (float)Math.Max(
                        allocatedTime, CustomFunctionHelper.CalculateTotalTime(
                            positionerMovementInformations[name].TargetDistance,
                            positionerMovementInformations[name].TargetDirection ? positionerMovementInformations[name].TargetSpeed :   positionerMovementInformations[name].TargetSpeed,
                            positionerMovementInformations[name].TargetAcceleration,
                            positionerMovementInformations[name].TargetDeceleration,
                            positionerMovementInformations[name].StartingSpeed
                            )
                        );

                }
                else
                    throw new Exception($"Unable retrieve positioner device {name}.");
            }



            /// Check if kinematic parameters dont need to be changed.
            /// If so, then our quable controllers will be forced to execute their buffers before this.

            if (positionerMovementInformations.Values.Any(deviceInfo =>
                (deviceInfo.TargetAcceleration != deviceInfo.StartingAcceleration
                || deviceInfo.TargetDeceleration != deviceInfo.StartingDeceleration
                || deviceInfo.TargetSpeed != deviceInfo.CurrentTargetSpeed))
                )
            {
                List<Command> UpdatePrametersCommandLine = new List<Command>();

                foreach (var controllerGroup in groupedDevicesByController)
                {
                    var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();
                    var controlerName = controllerGroup.Key.Name;

                    var movementSettings = new Dictionary<char, MovementSettingsInfo>();
                    foreach (var deviceName in groupedDeviceNames)
                    {
                        var deviceInfo = positionerMovementInformations[deviceName];
                        movementSettings[deviceName] = new MovementSettingsInfo
                        {
                            TargetAcceleration = deviceInfo.TargetAcceleration,
                            TargetDeceleration = deviceInfo.TargetDeceleration,
                            TargetSpeed = deviceInfo.TargetSpeed,

                        };
                    }

                    var commandParameters = new UpdateMovementSettingsParameters
                    {
                        MovementSettingsInformation = movementSettings
                    };

                    UpdatePrametersCommandLine.Add(
                        new Command()
                        {
                            Action = CommandDefinitions.UpdateMoveSettings,
                            Await = true,
                            Parameters = commandParameters,
                            TargetController = controlerName,
                            TargetDevices = groupedDeviceNames
                        }
                    );
                }

                _commandManager.EnqueueCommandLine(UpdatePrametersCommandLine.ToArray());
                _commandManager.ExecuteCommandLine(UpdatePrametersCommandLine.ToArray()).GetAwaiter().GetResult();
            }


            
            List<Command> commandsMovement = new List<Command>();

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();
                var controlerName = controllerGroup.Key.Name;

                var moveAParameters = new MoveAbsoluteParameters();
                moveAParameters.IsShutterUsed = (bool)isShutterUsed;
                moveAParameters.IsLeadOutUsed = false;
                moveAParameters.IsLeadInUsed = false;
                moveAParameters.AllocatedTime = allocatedTime;

                var PositionerInfoDictionary = new Dictionary<char, PositionerInfo>();

                foreach (var deviceName in groupedDeviceNames)
                {
                    var deviceInfo = positionerMovementInformations[deviceName];

                    PositionerInfoDictionary[deviceName] = new PositionerInfo
                    {
                        WaitUntil = null,
                        TargetSpeed = positionerMovementInformations[deviceName].TargetSpeed,
                        Direction = deviceInfo.TargetDirection,
                        TargetPosition = deviceInfo.TargetPosition,
                    };
                }

                moveAParameters.PositionerInfo = PositionerInfoDictionary;

                /// This delay is not the same as the intrinsic Shutter delay, described in ShutterDevice
                /// This delay can be used to perform timed movement where shutter state is in accordance to a lead in/out.
                ShutterInfo? shutterInfo = null;
                if (isShutterUsed)
                {
                    // TODO: shutter delays according to the lead information.
                    shutterInfo = new ShutterInfo
                    {
                        DelayOn = 0f,
                        DelayOff = 0f,
                    };
                }
                moveAParameters.ShutterInfo = shutterInfo;

                commandsMovement.Add(
                    new Command()
                    {
                        Action = CommandDefinitions.MoveAbsolute,
                        Await = true,
                        Parameters = moveAParameters,
                        TargetController = controlerName,
                        TargetDevices = groupedDeviceNames
                    }
                );
            }

            _commandManager.EnqueueCommandLine(commandsMovement.ToArray());
            _commandManager.ExecuteCommandLine(commandsMovement.ToArray()).GetAwaiter().GetResult();
            

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

            if (arguments.Length == 1 + positions.Length * 2)
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
