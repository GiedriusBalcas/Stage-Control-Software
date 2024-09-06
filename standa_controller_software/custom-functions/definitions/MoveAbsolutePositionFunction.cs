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

namespace standa_controller_software.custom_functions.definitions
{
    public class MoveAbsolutePositionFunction : CustomFunction
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
        
        public MoveAbsolutePositionFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            this.SetProperty("Speed", null);
            this.SetProperty("Shutter", false);
            this.SetProperty("LeadIn", false);
            this.SetProperty("LeadOut", false);
            this.SetProperty("Line", true);
        }

        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out char[] deviceNames, out float[] positions, out float[] waitUntil))
                throw new ArgumentException("Argument pasrsing was unsuccesfull. Wrong types.");

            List<Command> commandsMovement = new List<Command>();
            List<Command> commandsWaitForStop = new List<Command>();

            //if (this.TryGetProperty("Shutter", out object isOn))
            //    commandsMovement = (bool)isOn ? new Command[deviceNames.Length + 1] : new Command[deviceNames.Length];
            //else
            //    commandsMovement = new Command[deviceNames.Length];


            this.TryGetProperty("Speed", out object trajSpeed);

            float? trajectorySpeed = null; // Default value

            if (trajSpeed != null && trajSpeed is float trajSpeedFloat)
            {
                trajectorySpeed = trajSpeedFloat;
            }

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

            var positionerMovementInformations = deviceNames.ToDictionary(name => name, name => new PositionerMovementInformation() { TargetPosition = positions[Array.IndexOf(deviceNames, name)] });

            if (!CustomFunctionHelper.TryGetLineKinParameters(positionerMovementInformations, trajectorySpeed, _controllerManager, out float allocatedTime))
                throw new Exception("Failed to create line kinematic parameters");


            if (!TryGetProperty("Line", out object IsLine))
                throw new Exception();
            if (!TryGetProperty("Shutter", out object IsShutterUsed))
                throw new Exception();
            if (!TryGetProperty("LeadIn", out object IsLeadInUsed))
                throw new Exception();
            if (!TryGetProperty("LeadOut", out object IsLeadOutUsed))
                throw new Exception();

            var moveAbsoluteParameters = new MoveAbsoluteParameters
            {
                AllocatedTime = allocatedTime,
                Devices = deviceNames,
                IsLine = (bool)IsLine,
                IsShutterUsed = (bool)IsShutterUsed,
                IsLeadInUsed = (bool)IsLeadInUsed,
                IsLeadOutUsed = (bool)IsLeadOutUsed,
                ShutterInfo = (bool)IsShutterUsed
                    ? new ShutterInfo
                    {
                        DelayOff = 0f,
                        DelayOn = 0f,
                    }
                    : null,
            };


            /// Check if kinematic parameters dont need to be changed.
            /// If so, then our quable controllers will be forced to execute their buffers before this.

            bool UpdateSeetingsNeeded = false;
            foreach(var deviceName in deviceNames)
            {
                var deviceInfo = positionerMovementInformations[deviceName];

                if (deviceInfo.TargetAcceleration != deviceInfo.CurrentAcceleration
                    || deviceInfo.TargetDeceleration != deviceInfo.CurrentDeceleration
                    || deviceInfo.TargetSpeed != deviceInfo.CurrentSpeed)
                    UpdateSeetingsNeeded = true;
            }

            if (UpdateSeetingsNeeded)
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

                        movementSettings[deviceName].TargetAcceleration = deviceInfo.TargetAcceleration;
                        movementSettings[deviceName].TargetDeceleration = deviceInfo.TargetDeceleration;
                        movementSettings[deviceName].TargetSpeed = deviceInfo.TargetSpeed;
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
            }


            var kakaCommand = new Command()
            {
                Action = CommandDefinitions.UpdateMoveSettings,
                Await = true,
                Parameters = moveAbsoluteParameters,
                TargetController = "controller_name",
                TargetDevices = ['x', 'y']
            };

            var parameters = kakaCommand.Parameters as MoveAbsoluteParameters;


            //_commandManager.EnqueueCommandLine(commandsMovementParameters.ToArray());

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
                        Action = CommandDefinitions.MoveAbsolute,
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
                        Action = CommandDefinitions.WaitUntilStop,
                        Await = true,
                        Parameters = parameters,
                        TargetController = controlerName,
                        TargetDevices = groupedDeviceNames
                    }
                );
            }

            _commandManager.EnqueueCommandLine(commandsWaitForStop.ToArray());

            //_commandManager.ExecuteCommandLine(commandsMovementParameters.ToArray()).GetAwaiter().GetResult();
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
