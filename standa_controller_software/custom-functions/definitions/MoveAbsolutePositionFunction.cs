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


            //if (this.TryGetProperty("Shutter", out object isOn))
            //    commandsMovement = (bool)isOn ? new Command[deviceNames.Length + 1] : new Command[deviceNames.Length];
            //else
            //    commandsMovement = new Command[deviceNames.Length];

            // TODO: I should filter out devices, which dont change their position somewhere.


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


            if (!TryGetProperty("Line", out object IsLine))
                throw new Exception();
            if (!TryGetProperty("Shutter", out object IsShutterUsed))
                throw new Exception();
            if (!TryGetProperty("LeadIn", out object IsLeadInUsed))
                throw new Exception();
            if (!TryGetProperty("LeadOut", out object IsLeadOutUsed))
                throw new Exception();




            var positionerMovementInformations = new Dictionary<char, PositionerMovementInformation>();
            // Populate initial values
            foreach (char name in deviceNames)
            {
                if (_controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                {
                    positionerMovementInformations[name].TargetPosition = positions[Array.IndexOf(deviceNames, name)];
                    positionerMovementInformations[name].TargetDistance = Math.Abs(positionerMovementInformations[name].TargetPosition - positioner.CurrentPosition);
                    positionerMovementInformations[name].TargetDirection = positionerMovementInformations[name].TargetPosition > positioner.CurrentPosition;
                    positionerMovementInformations[name].StartingPosition = positioner.CurrentPosition;
                    positionerMovementInformations[name].StartingSpeed = positioner.CurrentSpeed;
                    positionerMovementInformations[name].StartingAcceleration = positioner.Acceleration;
                    positionerMovementInformations[name].StartingDeceleration = positioner.Deceleration;
                    positionerMovementInformations[name].MaxAcceleration = positioner.MaxAcceleration;
                    positionerMovementInformations[name].MaxDeceleration = positioner.MaxDeceleration;
                    positionerMovementInformations[name].MaxSpeed = positioner.MaxSpeed;
                }
                else
                    throw new Exception($"Unable retrieve positioner device {name}.");
            }
            float allocatedTime = 0f;
            if ((bool)IsLine || (bool)IsLeadInUsed || (bool)IsLeadOutUsed)
                if (CustomFunctionHelper.TryGetLineKinParameters(_controllerManager, trajectorySpeed, ref positionerMovementInformations, out float allocatedTimeCalc))
                    allocatedTime = allocatedTimeCalc;
                else
                    throw new Exception("Failed to create line kinematic parameters");
            else
            {
                if (CustomFunctionHelper.TryGetMaxKinParameters(_controllerManager, trajectorySpeed, ref positionerMovementInformations, out float allocatedTimeCalc))
                    allocatedTime = allocatedTimeCalc;
                else
                    throw new Exception("Failed to create line kinematic parameters");
            }

            /// Check if kinematic parameters dont need to be changed.
            /// If so, then our quable controllers will be forced to execute their buffers before this.

            if (positionerMovementInformations.Values.Any(deviceInfo =>
                (deviceInfo.TargetAcceleration != deviceInfo.StartingAcceleration
                || deviceInfo.TargetDeceleration != deviceInfo.StartingDeceleration
                || deviceInfo.TargetSpeed != deviceInfo.StartingSpeed))
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

                _commandManager.EnqueueCommandLine(UpdatePrametersCommandLine.ToArray());
                _commandManager.ExecuteCommandLine(UpdatePrametersCommandLine.ToArray()).GetAwaiter().GetResult();
            }



            if ((bool)IsLeadInUsed && (bool)IsLeadOutUsed)
            {
                // LEAD-IN PHASE

                var positionerMovementInformations_LeadIn = new Dictionary<char, PositionerMovementInformation>();
                // Populate initial values
                foreach (char name in deviceNames)
                {
                    if (_controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                    {
                        positionerMovementInformations_LeadIn[name].TargetPosition = positions[Array.IndexOf(deviceNames, name)];
                        positionerMovementInformations_LeadIn[name].TargetDistance = Math.Abs(positionerMovementInformations_LeadIn[name].TargetPosition - positioner.CurrentPosition);
                        positionerMovementInformations_LeadIn[name].TargetDirection = positionerMovementInformations_LeadIn[name].TargetPosition > positioner.CurrentPosition;
                        positionerMovementInformations_LeadIn[name].StartingPosition = positioner.CurrentPosition;
                        positionerMovementInformations_LeadIn[name].StartingSpeed = positioner.CurrentSpeed;
                        positionerMovementInformations_LeadIn[name].StartingAcceleration = positioner.Acceleration;
                        positionerMovementInformations_LeadIn[name].StartingDeceleration = positioner.Deceleration;
                        positionerMovementInformations_LeadIn[name].MaxAcceleration = positioner.MaxAcceleration;
                        positionerMovementInformations_LeadIn[name].MaxDeceleration = positioner.MaxDeceleration;
                        positionerMovementInformations_LeadIn[name].MaxSpeed = positioner.MaxSpeed;
                    }
                    else
                        throw new Exception($"Unable retrieve positioner device {name}.");
                }
                GetLeadInKinParameters(_controllerManager, trajectorySpeed, ref positionerMovementInformations_LeadIn, out float allocatedTimeLeadIn);


                // CONSTANCT SPEED PHASE

                var positionerMovementInformations_Constant = new Dictionary<char, PositionerMovementInformation>();

                foreach (char name in deviceNames)
                {
                    if (_controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                    {
                        positionerMovementInformations_Constant[name].TargetPosition = positions[Array.IndexOf(deviceNames, name)];
                        positionerMovementInformations_Constant[name].TargetDistance = Math.Abs(positionerMovementInformations_Constant[name].TargetPosition - positioner.CurrentPosition);
                        positionerMovementInformations_Constant[name].TargetDirection = positionerMovementInformations_Constant[name].TargetPosition > positioner.CurrentPosition;
                        positionerMovementInformations_Constant[name].StartingPosition = positioner.CurrentPosition;
                        positionerMovementInformations_Constant[name].StartingSpeed = positioner.CurrentSpeed;
                        positionerMovementInformations_Constant[name].StartingAcceleration = positioner.Acceleration;
                        positionerMovementInformations_Constant[name].StartingDeceleration = positioner.Deceleration;
                        positionerMovementInformations_Constant[name].MaxAcceleration = positioner.MaxAcceleration;
                        positionerMovementInformations_Constant[name].MaxDeceleration = positioner.MaxDeceleration;
                        positionerMovementInformations_Constant[name].MaxSpeed = positioner.MaxSpeed;
                    }
                    else
                        throw new Exception($"Unable retrieve positioner device {name}.");
                }
                if (!CustomFunctionHelper.TryGetMaxKinParameters(_controllerManager, trajectorySpeed, ref positionerMovementInformations_Constant, out float allocatedTime_Constant))
                    throw new Exception("Failed to create line kinematic parameters");

                // LEAD-OUT PHASE

                var positionerMovementInformations_LeadOut = new Dictionary<char, PositionerMovementInformation>();

                foreach (char name in deviceNames)
                {
                    if (_controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                    {
                        positionerMovementInformations_LeadOut[name].TargetPosition = positions[Array.IndexOf(deviceNames, name)];
                        positionerMovementInformations_LeadOut[name].TargetDistance = Math.Abs(positionerMovementInformations_LeadOut[name].TargetPosition - positioner.CurrentPosition);
                        positionerMovementInformations_LeadOut[name].TargetDirection = positionerMovementInformations_LeadOut[name].TargetPosition > positioner.CurrentPosition;
                        positionerMovementInformations_LeadOut[name].StartingPosition = positioner.CurrentPosition;
                        positionerMovementInformations_LeadOut[name].StartingSpeed = positioner.CurrentSpeed;
                        positionerMovementInformations_LeadOut[name].StartingAcceleration = positioner.Acceleration;
                        positionerMovementInformations_LeadOut[name].StartingDeceleration = positioner.Deceleration;
                        positionerMovementInformations_LeadOut[name].MaxAcceleration = positioner.MaxAcceleration;
                        positionerMovementInformations_LeadOut[name].MaxDeceleration = positioner.MaxDeceleration;
                        positionerMovementInformations_LeadOut[name].MaxSpeed = positioner.MaxSpeed;
                    }
                    else
                        throw new Exception($"Unable retrieve positioner device {name}.");
                }
                GetLeadOutKinParameters(_controllerManager, trajectorySpeed, ref positionerMovementInformations_LeadOut, out float allocatedTime_LeadOut);



                // GETTING TO LEAD-IN START PHASE

                var positionerMovementInformations_LeadInStart = new Dictionary<char, PositionerMovementInformation>();

                foreach (char name in deviceNames)
                {
                    if (_controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                    {
                        positionerMovementInformations_LeadInStart[name].StartingPosition = positioner.CurrentPosition;
                        positionerMovementInformations_LeadInStart[name].TargetPosition = positionerMovementInformations_LeadIn[name].StartingPosition;
                        positionerMovementInformations_LeadInStart[name].TargetDistance = Math.Abs(positionerMovementInformations_LeadInStart[name].TargetPosition - positionerMovementInformations_LeadInStart[name].StartingPosition);
                        positionerMovementInformations_LeadInStart[name].TargetDirection = positionerMovementInformations_LeadInStart[name].TargetPosition > positionerMovementInformations_LeadInStart[name].StartingPosition;
                        positionerMovementInformations_LeadInStart[name].StartingSpeed = positioner.CurrentSpeed;
                        positionerMovementInformations_LeadInStart[name].StartingAcceleration = positioner.Acceleration;
                        positionerMovementInformations_LeadInStart[name].StartingDeceleration = positioner.Deceleration;
                        positionerMovementInformations_LeadInStart[name].MaxAcceleration = positioner.MaxAcceleration;
                        positionerMovementInformations_LeadInStart[name].MaxDeceleration = positioner.MaxDeceleration;
                        positionerMovementInformations_LeadInStart[name].MaxSpeed = positioner.MaxSpeed;
                    }
                    else
                        throw new Exception($"Unable retrieve positioner device {name}.");
                }
                if (!CustomFunctionHelper.TryGetMaxKinParameters(_controllerManager, trajectorySpeed, ref positionerMovementInformations_Constant, out float allocatedTime_toLeadInStart))
                    throw new Exception("Failed to create line kinematic parameters");


                // MOVEA COMMAND TO LEAD-IN START

                List<Command> commandsMovement = new List<Command>();

                foreach (var controllerGroup in groupedDevicesByController)
                {
                    var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();
                    var controlerName = controllerGroup.Key.Name;

                    var moveAParameters = new MoveAbsoluteParameters();
                    moveAParameters.IsShutterUsed = false;
                    moveAParameters.IsLeadOutUsed = false;
                    moveAParameters.IsLeadInUsed = false;
                    moveAParameters.AllocatedTime = allocatedTime_toLeadInStart;

                    var PositionerInfoDictionary = new Dictionary<char, PositionerInfo>();

                    foreach (var deviceName in groupedDeviceNames)
                    {
                        LeadInfo? leadInfo = null;

                        float? waitUntilPos = null;

                        PositionerInfoDictionary[deviceName] = new PositionerInfo
                        {
                            LeadInformation = leadInfo,
                            WaitUntil = waitUntilPos,
                            TargetSpeed = positionerMovementInformations_LeadInStart[deviceName].TargetSpeed,
                            Direction = positionerMovementInformations_LeadInStart[deviceName].TargetDirection,
                            TargetPosition = positionerMovementInformations_LeadInStart[deviceName].TargetPosition,
                        };
                    }

                    moveAParameters.PositionerInfo = PositionerInfoDictionary;

                    /// This delay is not the same as the intrinsic Shutter delay, described in ShutterDevice
                    /// This delay can be used to perform timed movement where shutter state is in accordance to a lead in/out.
                    ShutterInfo? shutterInfo = null;
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

                // MOVEA COMMAND FOR ALL 3 PHASES.

                commandsMovement = new List<Command>();

                foreach (var controllerGroup in groupedDevicesByController)
                {
                    var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();
                    var controlerName = controllerGroup.Key.Name;

                    var moveAParameters = new MoveAbsoluteParameters();
                    moveAParameters.IsShutterUsed = (bool)IsShutterUsed;
                    moveAParameters.IsLeadOutUsed = true;
                    moveAParameters.IsLeadInUsed = true;
                    moveAParameters.AllocatedTime  = allocatedTimeLeadIn + allocatedTime_Constant + allocatedTime_LeadOut;

                    var PositionerInfoDictionary = new Dictionary<char, PositionerInfo>();

                    foreach (var deviceName in groupedDeviceNames)
                    {
                        LeadInfo leadInfo = new LeadInfo
                        {
                            LeadInAllocatedTime = allocatedTimeLeadIn,
                            LeadOutAllocatedTime = allocatedTime_LeadOut,
                            LeadInStartPos = positionerMovementInformations_LeadIn[deviceName].StartingPosition,
                            LeadInEndPos = positionerMovementInformations_LeadIn[deviceName].TargetPosition,
                            LeadOutEndPos = positionerMovementInformations_LeadOut[deviceName].TargetPosition
                        };

                        float? waitUntilPos = null;
                        if (waitUntil.Length == deviceNames.Length)
                        {
                            waitUntilPos = waitUntil[Array.IndexOf(deviceNames, deviceName)];
                        }

                        PositionerInfoDictionary[deviceName] = new PositionerInfo
                        {
                            LeadInformation = leadInfo,
                            WaitUntil = waitUntilPos,
                            TargetSpeed = positionerMovementInformations[deviceName].TargetSpeed,
                            Direction = positionerMovementInformations[deviceName].TargetPosition >= positionerMovementInformations[deviceName].StartingPosition,
                            TargetPosition = positionerMovementInformations[deviceName].TargetPosition,
                        };
                    }

                    moveAParameters.PositionerInfo = PositionerInfoDictionary;

                    /// This delay is not the same as the intrinsic Shutter delay, described in ShutterDevice
                    /// This delay can be used to perform timed movement where shutter state is in accordance to a lead in/out.
                    ShutterInfo? shutterInfo = null;
                    if ((bool)IsShutterUsed)
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

            }
            else
            {
                List<Command> commandsMovement = new List<Command>();

                foreach (var controllerGroup in groupedDevicesByController)
                {
                    var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();
                    var controlerName = controllerGroup.Key.Name;

                    var moveAParameters = new MoveAbsoluteParameters();
                    moveAParameters.IsShutterUsed = (bool)IsShutterUsed;
                    moveAParameters.IsLeadOutUsed = false;
                    moveAParameters.IsLeadInUsed = false;
                    moveAParameters.AllocatedTime = allocatedTime;

                    var PositionerInfoDictionary = new Dictionary<char, PositionerInfo>();

                    foreach (var deviceName in groupedDeviceNames)
                    {
                        LeadInfo? leadInfo = null;
                        if ((bool)IsLeadInUsed || (bool)IsLeadOutUsed)
                        {
                            // TODO: calculate lead params.
                        }

                        float? waitUntilPos = null;
                        if (waitUntil.Length == deviceNames.Length)
                        {
                            waitUntilPos = waitUntil[Array.IndexOf(deviceNames, deviceName)];
                        }

                        PositionerInfoDictionary[deviceName] = new PositionerInfo
                        {
                            LeadInformation = leadInfo,
                            WaitUntil = waitUntilPos,
                            TargetSpeed = positionerMovementInformations[deviceName].TargetSpeed,
                            Direction = positionerMovementInformations[deviceName].TargetPosition >= positionerMovementInformations[deviceName].StartingPosition,
                            TargetPosition = 0f,
                        };
                    }

                    moveAParameters.PositionerInfo = PositionerInfoDictionary;

                    /// This delay is not the same as the intrinsic Shutter delay, described in ShutterDevice
                    /// This delay can be used to perform timed movement where shutter state is in accordance to a lead in/out.
                    ShutterInfo? shutterInfo = null;
                    if ((bool)IsShutterUsed)
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
            }

            return null;
        }

        private void GetLeadInKinParameters(ControllerManager controllerManager, float? trajectorySpeed, ref Dictionary<char, PositionerMovementInformation> positionerMovementInformations, out float allocatedTime)
        {
            var positionerMovementInfo = positionerMovementInformations;

            char[] deviceNames = positionerMovementInfo.Keys.ToArray();

            //Calculate the initial and final tool positions
            var startToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.StartingPosition)
                );

            var endToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.TargetPosition)
                );

            if (startToolPoint == endToolPoint)
            {
                throw new Exception("Error encountered, when trying to get kinematic parameters. Starting point and end point are the same.");
            }

            // TODO: calculate the speed according to DefaultSpeed of positioners used.

            float trajectorySpeedCalculated = (float)((trajectorySpeed is null) ? 100f : trajectorySpeed);

            // Calculate trajectory length
            float trajectoryLength = (endToolPoint - startToolPoint).Length();


            // Calculate the target kinematic parameters
            var movementRatio = new Dictionary<char, float>();
            var projectedMaxAccelerations = new Dictionary<char, float>();
            var projectedMaxDecelerations = new Dictionary<char, float>();
            var projectedMaxSpeeds = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                movementRatio[name] = trajectoryLength / positionerMovementInfo[name].TargetDistance;
                projectedMaxAccelerations[name] = positionerMovementInfo[name].MaxAcceleration * movementRatio[name];
                projectedMaxDecelerations[name] = positionerMovementInfo[name].MaxDeceleration * movementRatio[name];
                projectedMaxSpeeds[name] = positionerMovementInfo[name].MaxSpeed * movementRatio[name];
            }
            var projectedMaxAcceleration = projectedMaxAccelerations.Min(kvp => kvp.Value);
            var projectedMaxDeceleration = projectedMaxDecelerations.Min(kvp => kvp.Value);
            var projectedMaxSpeed = Math.Min(trajectorySpeedCalculated, projectedMaxSpeeds.Min(kvp => kvp.Value));

            var timesToAccel = new Dictionary<char, float>();
            var timesToDecel = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                positionerMovementInfo[name].MaxAcceleration = projectedMaxAcceleration / movementRatio[name];
                positionerMovementInfo[name].MaxDeceleration = projectedMaxDeceleration / movementRatio[name];
                positionerMovementInfo[name].TargetSpeed = projectedMaxSpeed / movementRatio[name];

                // TODO: check if direction is needed here. Also include addiotional deceleration when changing directions.

                int direction = positionerMovementInfo[name].TargetDirection ? 1 : -1;
                timesToAccel[name] = Math.Abs(positionerMovementInfo[name].TargetSpeed * direction - positionerMovementInfo[name].StartingSpeed) / positionerMovementInfo[name].MaxAcceleration;
                timesToDecel[name] = Math.Abs(positionerMovementInfo[name].TargetSpeed * direction - 0) / positionerMovementInfo[name].MaxDeceleration;
            }

            var maxTimeToAccel = timesToAccel.Max(kvp => kvp.Value);
            var maxTimeToDecel = timesToAccel.Max(kvp => kvp.Value);

            foreach (char name in deviceNames)
            {
                positionerMovementInfo[name].TargetAcceleration = positionerMovementInfo[name].MaxAcceleration * (timesToAccel[name] / maxTimeToAccel);
                positionerMovementInfo[name].TargetDeceleration = positionerMovementInfo[name].MaxDeceleration * (timesToDecel[name] / maxTimeToDecel);
            }

            var projectedDistanceToAccelerate = 1 / 2 * maxTimeToAccel * maxTimeToAccel * projectedMaxAcceleration;
            var distancesToAccelerate = new Dictionary<char, float>();
            var leadPositions = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                distancesToAccelerate[name] = projectedDistanceToAccelerate / movementRatio[name];
                int direction = positionerMovementInfo[name].TargetDirection ? 1 : -1;
                positionerMovementInfo[name].TargetPosition = positionerMovementInfo[name].StartingPosition;
                positionerMovementInfo[name].StartingPosition = positionerMovementInfo[name].StartingPosition - distancesToAccelerate[name] * direction;
            }

            allocatedTime = maxTimeToAccel;
        }

        private void GetLeadOutKinParameters(ControllerManager controllerManager, float? trajectorySpeed, ref Dictionary<char, PositionerMovementInformation> positionerMovementInformations, out float allocatedTime)
        {
            var positionerMovementInfo = positionerMovementInformations;

            char[] deviceNames = positionerMovementInfo.Keys.ToArray();

            //Calculate the initial and final tool positions
            var startToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.StartingPosition)
                );

            var endToolPoint = controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfo.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.TargetPosition)
                );

            if (startToolPoint == endToolPoint)
            {
                throw new Exception("Error encountered, when trying to get kinematic parameters. Starting point and end point are the same.");
            }

            // TODO: calculate the speed according to DefaultSpeed of positioners used.

            float trajectorySpeedCalculated = (float)((trajectorySpeed is null) ? 100f : trajectorySpeed);

            // Calculate trajectory length
            float trajectoryLength = (endToolPoint - startToolPoint).Length();


            // Calculate the target kinematic parameters
            var movementRatio = new Dictionary<char, float>();
            var projectedMaxAccelerations = new Dictionary<char, float>();
            var projectedMaxDecelerations = new Dictionary<char, float>();
            var projectedMaxSpeeds = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                movementRatio[name] = trajectoryLength / positionerMovementInfo[name].TargetDistance;
                projectedMaxAccelerations[name] = positionerMovementInfo[name].MaxAcceleration * movementRatio[name];
                projectedMaxDecelerations[name] = positionerMovementInfo[name].MaxDeceleration * movementRatio[name];
                projectedMaxSpeeds[name] = positionerMovementInfo[name].MaxSpeed * movementRatio[name];
            }
            var projectedMaxAcceleration = projectedMaxAccelerations.Min(kvp => kvp.Value);
            var projectedMaxDeceleration = projectedMaxDecelerations.Min(kvp => kvp.Value);
            var projectedMaxSpeed = Math.Min(trajectorySpeedCalculated, projectedMaxSpeeds.Min(kvp => kvp.Value));

            var timesToAccel = new Dictionary<char, float>();
            var timesToDecel = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                positionerMovementInfo[name].MaxAcceleration = projectedMaxAcceleration / movementRatio[name];
                positionerMovementInfo[name].MaxDeceleration = projectedMaxDeceleration / movementRatio[name];
                positionerMovementInfo[name].TargetSpeed = projectedMaxSpeed / movementRatio[name];

                // TODO: check if direction is needed here. Also include addiotional deceleration when changing directions.

                int direction = positionerMovementInfo[name].TargetDirection ? 1 : -1;
                timesToAccel[name] = Math.Abs(positionerMovementInfo[name].TargetSpeed * direction - positionerMovementInfo[name].StartingSpeed) / positionerMovementInfo[name].MaxAcceleration;
                timesToDecel[name] = Math.Abs(positionerMovementInfo[name].TargetSpeed * direction - 0) / positionerMovementInfo[name].MaxDeceleration;
            }

            var maxTimeToAccel = timesToAccel.Max(kvp => kvp.Value);
            var maxTimeToDecel = timesToAccel.Max(kvp => kvp.Value);

            foreach (char name in deviceNames)
            {
                positionerMovementInfo[name].TargetAcceleration = positionerMovementInfo[name].MaxAcceleration * (timesToAccel[name] / maxTimeToAccel);
                positionerMovementInfo[name].TargetDeceleration = positionerMovementInfo[name].MaxDeceleration * (timesToDecel[name] / maxTimeToDecel);
            }

            var projectedDistanceToDecelerate = 1 / 2 * maxTimeToDecel * maxTimeToDecel * projectedMaxDeceleration;
            var distancesToDecelerate = new Dictionary<char, float>();
            var leadPositions = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                distancesToDecelerate[name] = projectedDistanceToDecelerate / movementRatio[name];
                int direction = positionerMovementInfo[name].TargetDirection ? 1 : -1;
                positionerMovementInfo[name].StartingPosition = positionerMovementInfo[name].TargetPosition;
                positionerMovementInfo[name].TargetPosition = positionerMovementInfo[name].TargetPosition + distancesToDecelerate[name] * direction;
            }

            allocatedTime = maxTimeToDecel;
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
