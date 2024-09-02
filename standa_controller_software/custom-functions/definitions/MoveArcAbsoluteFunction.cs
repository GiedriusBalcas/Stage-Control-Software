using standa_controller_software.command_manager;
using standa_controller_software.custom_functions.helpers;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_parser_library;

namespace standa_controller_software.custom_functions.definitions
{
    public class MoveArcAbsoluteFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private CommandManager _commandManager;
        private ControllerManager _controllerManager;

        public MoveArcAbsoluteFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            this.SetProperty("Speed", 1000f);
            this.SetProperty("Shutter", false);
        }

        

        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out char[] deviceNames, out float[] centerPositions, out float radius, out float[] waitUntil))
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

            float segmentLength = .1f * trajectorySpeed;
            float segmmentAngl = (float)Math.Atan2(segmentLength, radius);
            segmmentAngl = (float)(2 * Math.PI / Math.Floor(2 * Math.PI / segmmentAngl));

            var positionX = centerPositions[0] + (float)Math.Cos(0f) * radius;
            var positionY = centerPositions[1] + (float)Math.Sin(0f) * radius;

            var positionerMovementInformations = deviceNames.ToDictionary(name => name, name => new PositionerMovementInformation() { TargetPosition = name == deviceNames[0] ? positionX : positionY });

            if (!CustomFunctionHelper.TryGetLineKinParameters(positionerMovementInformations, trajectorySpeed, _controllerManager, out float allocatedTimeToStart))
                throw new Exception("Failed to create line kinematic parameters");
            var allocatedTime = allocatedTimeToStart;

            commandsMovementParameters = GetUpdateMovementParametersCommandLine(groupedDevicesByController, positionerMovementInformations);
            _commandManager.EnqueueCommandLine(commandsMovementParameters.ToArray());

            commandsMovement = GetMovementCommandLine(groupedDevicesByController, positionerMovementInformations);
            _commandManager.EnqueueCommandLine(commandsMovement.ToArray());

            float[] waitUntilPosition = Array.Empty<float>();
            commandsWaitForStop = GetWaitUntilCommandLine(waitUntilPosition, groupedDevicesByController, positionerMovementInformations);
            _commandManager.EnqueueCommandLine(commandsWaitForStop.ToArray());

            _commandManager.ExecuteCommandLine(commandsMovementParameters.ToArray()).GetAwaiter().GetResult();
            _commandManager.ExecuteCommandLine(commandsMovement.ToArray()).GetAwaiter().GetResult();
            
            float positionX_prev = positionX;
            float positionY_prev = positionY;

            for (int i = 1 ; i <= (2* Math.PI / segmmentAngl); i ++)
            {
                double angle = segmmentAngl * i;
                positionX = centerPositions[0] + (float)Math.Cos(angle) * radius;
                positionY = centerPositions[1] + (float)Math.Sin(angle) * radius;

                var kakaradius = Math.Sqrt(Math.Pow(positionX,2) + Math.Pow(positionY,2));

                positionerMovementInformations = deviceNames.ToDictionary(name => name, name => new PositionerMovementInformation() { TargetPosition = name == deviceNames[0] ? positionX : positionY });


                foreach (var device in devices)
                {
                    device.CurrentPosition = device.Name == deviceNames[0] ? positionX_prev : positionY_prev;
                }
                //positionerMovementInformations = deviceNames.ToDictionary(name => name, name => new PositionerMovementInformation() { CurrentSpeed = name == deviceNames[0] ? 0f : 0f });
                if (!CustomFunctionHelper.TryGetLineKinParameters(positionerMovementInformations, trajectorySpeed, _controllerManager, out float allocatedTimeSegment))
                    throw new Exception("Failed to create line kinematic parameters");
                allocatedTime += allocatedTimeSegment;


                commandsMovementParameters = GetUpdateMovementParametersCommandLine(groupedDevicesByController, positionerMovementInformations);
                _commandManager.EnqueueCommandLine(commandsMovementParameters.ToArray());


                float distanceToDecelerateX = (float)Math.Pow(positionerMovementInformations[deviceNames[0]].TargetSpeed, 2) / positionerMovementInformations[deviceNames[0]].MaxDeceleration;
                float distanceToDecelerateY = (float)Math.Pow(positionerMovementInformations[deviceNames[1]].TargetSpeed, 2) / positionerMovementInformations[deviceNames[1]].MaxDeceleration;

                var alpha = Math.Atan((positionY - positionY_prev) / (positionX - positionX_prev));
                var distx = distanceToDecelerateX / Math.Cos(alpha);
                var disty = distanceToDecelerateY / Math.Sin(alpha);


                float positionX_prolong;
                float positionY_prolong;

                if (Math.Abs(distx) > Math.Abs(disty))
                {
                    positionX_prolong = (float)(distx * Math.Cos(alpha) + positionX);
                    positionY_prolong = (float)(Math.Tan(alpha) * (positionX_prolong - positionX) + positionY);
                }
                else
                {
                    positionY_prolong = (float)(disty * Math.Sin(alpha) + positionY);
                    positionX_prolong = (float)((positionY_prolong - positionY) / Math.Tan(alpha) + positionX);
                }


                // Here for simplyfication distance is devided by length of trajectory.
                //var Dist_x = (float)( distanceToDecelerateX / (positionX - positionX_prev) );
                //var Dist_y = (float)( distanceToDecelerateY / (positionY - positionY_prev));

                //float Dist_max = Math.Max(Math.Abs(Dist_x), Math.Abs(Dist_x));

                //var positionX_prolong = positionX + Dist_max * (positionX - positionX_prev);
                //var positionY_prolong = positionY + Dist_max * (positionY - positionY_prev);

                positionerMovementInformations[deviceNames[0]].TargetPosition = positionX_prolong;
                positionerMovementInformations[deviceNames[1]].TargetPosition = positionY_prolong;
                commandsMovement = GetMovementCommandLine(groupedDevicesByController, positionerMovementInformations);
                _commandManager.EnqueueCommandLine(commandsMovement.ToArray());


                var waitUntilPolarCommandLine = GetWaitUntilPolarCommandLine(waitUntilPosition, centerPositions, true, groupedDevicesByController, positionerMovementInformations);

                //waitUntilPosition = [Math.Abs(positionX_prolong - positionX), Math.Abs(positionY_prolong - positionY)];
                //commandsWaitForStop = GetWaitUntilCommandLine(waitUntilPosition, groupedDevicesByController, positionerMovementInformations);
                //_commandManager.EnqueueCommandLine(commandsWaitForStop.ToArray());


                
                positionerMovementInformations[deviceNames[0]].TargetPosition = positionX;
                positionerMovementInformations[deviceNames[1]].TargetPosition = positionY;
                var commandsMovement_virtual = GetMovementCommandLine(groupedDevicesByController, positionerMovementInformations);

                //_commandManager.ExecuteCommandLine(commandsMovementParameters.ToArray()).GetAwaiter().GetResult();
                //_commandManager.ExecuteCommandLine(commandsMovement.ToArray()).GetAwaiter().GetResult();
                //_commandManager.ExecuteCommandLine(commandsMovement_virtual.ToArray()).GetAwaiter().GetResult();

                positionX_prev = positionX;
                positionY_prev = positionY;
            }


            return null;
        }

        private static List<Command> GetWaitUntilCommandLine(float[] waitUntil, Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController, Dictionary<char, PositionerMovementInformation> positionerMovementInformations)
        {
            List<Command> commandLine = new List<Command>();

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();
                var controlerName = controllerGroup.Key.Name;

                object[][] parameters = new object[groupedDeviceNames.Length][];
                if (waitUntil.Length > 0)
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

                commandLine.Add(
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

            return commandLine;
        }

        private static List<Command> GetWaitUntilPolarCommandLine(float[] waitUntil, float[] center, bool direction, Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController, Dictionary<char, PositionerMovementInformation> positionerMovementInformations)
        {
            List<Command> commandLine = new List<Command>();
            var deviceNamess = positionerMovementInformations.Keys.ToArray();
            var currentAngleRadians = Math.Atan2((positionerMovementInformations[deviceNamess[0]].CurrentPosition - center[0]), (positionerMovementInformations[deviceNamess[1]].CurrentPosition - center[1]));
            float targetAngleRadians = (float)Math.Atan2((positionerMovementInformations[deviceNamess[0]].TargetPosition - center[0]), (positionerMovementInformations[deviceNamess[1]].TargetPosition - center[1]));
            
            object[][] parameters = new object[deviceNamess.Length][];
            if (waitUntil.Length > 0)
            {
                parameters[0][0] = targetAngleRadians;
                parameters[1][0] = targetAngleRadians;
                parameters[0][1] = direction;
                parameters[1][1] = direction;
                parameters[0][2] = center[0];
                parameters[1][2] = center[1];
            }

            commandLine.Add(
                new Command()
                {
                    Action = CommandDefinitionsLibrary.WaitUntilStopPolar.ToString(),
                    Await = true,
                    Parameters = parameters,
                    TargetController = groupedDevicesByController.Keys.First().Name,
                    TargetDevices = groupedDevicesByController[groupedDevicesByController.Keys.First()].Select(device => device.Name).ToArray()
                }
                );

            return commandLine;
        }

        private static List<Command> GetMovementCommandLine(Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController, Dictionary<char, PositionerMovementInformation> positionerMovementInformations)
        {
            List<Command> commandLine = new List<Command>();
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

                commandLine.Add(
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
            return commandLine;
        }

        private static List<Command> GetUpdateMovementParametersCommandLine(Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController, Dictionary<char, PositionerMovementInformation> positionerMovementInformations)
        {
            var commandLine = new List<Command>();

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
                            positionerMovementInformations[deviceName].MaxAcceleration,
                            positionerMovementInformations[deviceName].MaxDeceleration
                    };
                }

                commandLine.Add
                (
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

            return commandLine;
        }

        // arcA("xy", 10,10,100);
        public bool TryParseArguments(object?[] arguments, out char[] devNames, out float[] centerPositions, out float radius, out float[] waitUntil)
        {
            var firstArg = string.Empty; // Default value
            devNames = Array.Empty<char>();
            centerPositions = Array.Empty<float>(); // Default value
            radius = 0f;
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
            var centerPositionsList = new float[2];

            // Start from index 1 since index 0 is the string argument
            for (int i = 1; i < 3; i++)
            {
                // Attempt to parse each object as float
                if (arguments[i] is float f)
                {
                    centerPositionsList[i - 1] = f;
                }
                else if (arguments[i] is double d) // Handle double to float conversion
                {
                    centerPositionsList[i - 1] = (float)d;
                }
                else if (arguments[i] is int integer) // Handle int to float conversion
                {
                    centerPositionsList[i - 1] = integer;
                }
                else
                {
                    // Try to parse as float from string or other convertible types
                    object? arg = arguments[i];
                    if (arg != null && float.TryParse(arg.ToString(), out float parsedFloat))
                    {
                        centerPositionsList[i - 1] = parsedFloat;
                    }
                    else
                    {
                        // Could not parse argument as float
                        return false;
                    }
                }
            }
            centerPositions = centerPositionsList.ToArray<float>();
            // Start with an empty list to collect the float values
            
            // Start from index 1 since index 0 is the string argument
            if (arguments.Length >= 4)
            {
                // Attempt to parse each object as float
                if (arguments[3] is float f)
                {
                    radius = f;
                }
                else if (arguments[3] is double d) // Handle double to float conversion
                {
                    radius = (float)d;
                }
                else if (arguments[3] is int integer) // Handle int to float conversion
                {
                    radius = integer;
                }
                else
                {
                    // Try to parse as float from string or other convertible types
                    object? arg = arguments[3];
                    if (arg != null && float.TryParse(arg.ToString(), out float parsedFloat))
                    {
                        radius = parsedFloat;
                    }
                    else
                    {
                        // Could not parse argument as float
                        return false;
                    }
                }
            }


            //var waitUntilList = new List<float>();

            //if (arguments.Length == 1 + positions.Length * 2)
            //    for (int i = 1 + positions.Length; i < arguments.Length; i++)
            //    {
            //        // Attempt to parse each object as float
            //        if (arguments[i] is float f)
            //        {
            //            waitUntilList.Add(f);
            //        }
            //        else if (arguments[i] is double d) // Handle double to float conversion
            //        {
            //            waitUntilList.Add((float)d);
            //        }
            //        else if (arguments[i] is int integer) // Handle int to float conversion
            //        {
            //            waitUntilList.Add(integer);
            //        }
            //        else
            //        {
            //            // Try to parse as float from string or other convertible types
            //            object? arg = arguments[i];
            //            if (arg != null && float.TryParse(arg.ToString(), out float parsedFloat))
            //            {
            //                waitUntilList.Add(parsedFloat);
            //            }
            //            else
            //            {
            //                // Could not parse argument as float
            //                return false;
            //            }
            //        }
            //    }

            //waitUntil = waitUntilList.ToArray(); // Convert the list to an array

            return true; // Successfully parsed all arguments
        }
    }
}
