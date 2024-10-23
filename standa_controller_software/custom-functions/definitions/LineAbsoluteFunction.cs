using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.devices;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using System;
using System.Collections.Generic;
using System.Linq;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.custom_functions.helpers;
using text_parser_library;
using System.Xml.Linq;

namespace standa_controller_software.custom_functions.definitions
{
    public class LineAbsoluteFunction : CustomFunction
    {
        public enum WaitUntilCondition
        {
            LeadInEnd,
            LeadOutStart
        }


        private readonly float _jerkValue = 20000f;

        public string Message { get; set; } = "";
        private readonly CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;
        private readonly JumpAbsoluteFunction _jumpAbsoluteFunction;
        private float JerkTime = 0.0003f;

        public LineAbsoluteFunction(CommandManager commandManager, ControllerManager controllerManager, JumpAbsoluteFunction jumpFunction)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            _jumpAbsoluteFunction = jumpFunction;
            SetProperty("Shutter", false);
            SetProperty("Accuracy", 0.05f);
            SetProperty("LeadIn", false);
            SetProperty("LeadOut", false);
            SetProperty("WaitUntilCondition", null, true);
            SetProperty("Speed", 100f);

        }

        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out var parsedDeviceNames, out var parsedStartPositions, out var parsedEndPositions))
                throw new ArgumentException("Argument parsing was unsuccessful. Wrong types.");

            if (!TryGetProperty("Shutter", out var isShutterUsedObj))
                throw new Exception("Failed to get 'Shutter' property.");
            var isShutterUsed = (bool)isShutterUsedObj;

            if (!TryGetProperty("Accuracy", out var accuracyObj))
                throw new Exception("Failed to get 'Accuracy' property.");
            var accuracy = (float)accuracyObj;

            if (!TryGetProperty("Speed", out var trajSpeedObj))
                throw new Exception("Failed to get 'Speed' property.");
            var trajectorySpeed = (float)trajSpeedObj;

            if (!TryGetProperty("LeadIn", out var leadInObj))
                throw new Exception("Failed to get 'LeadIn' property.");
            var leadIn = (bool)leadInObj;

            if (!TryGetProperty("LeadOut", out var leadOutObj))
                throw new Exception("Failed to get 'LeadOut' property.");
            var leadOut = (bool)leadOutObj;

            WaitUntilCondition? waitUntilCondition = null;
            if (!TryGetProperty("WaitUntilCondition", out var waitUntilConditionObj))
                throw new Exception("Failed to get 'WaitUntilCondition' property.");
            if (waitUntilConditionObj is not null)
            {
                if (Enum.TryParse(waitUntilConditionObj.ToString(), ignoreCase: true, out WaitUntilCondition parsedWaitUntilCondition))
                {
                    waitUntilCondition = parsedWaitUntilCondition;
                }
                else
                {
                    throw new ArgumentException($"Invalid 'WaitUntilCondition' property value: '{waitUntilConditionObj}'. Supported values are: {string.Join(", ", Enum.GetNames(typeof(WaitUntilCondition)))}");
                }
            }

            ExecutionCore(parsedDeviceNames, parsedStartPositions, parsedEndPositions, trajectorySpeed, isShutterUsed, accuracy, leadIn, leadOut, waitUntilCondition);

            return null;
        }

        public void ExecutionCore(
    char[] deviceNames,
    float[] startPositions,
    float[] endPositions,
    float trajectorySpeed,
    bool isShutterUsed,
    float accuracy,
    bool leadIn,
    bool leadOut,
    WaitUntilCondition? waitUntilCondition)
        {
            // STEP 1: Filter out devices that need to move
            var devicesToMove = new List<char>();
            var devicesStartPositions = new List<float>();
            var devicesEndPositions = new List<float>();

            for (int i = 0; i < deviceNames.Length; i++)
            {
                if (_controllerManager.TryGetDevice<BasePositionerDevice>(deviceNames[i], out var positioner))
                {
                    var startPos = startPositions[i];
                    var endPos = endPositions[i];

                    // Check if movement is needed
                    if (Math.Abs(startPos - endPos) > accuracy)
                    {
                        devicesToMove.Add(deviceNames[i]);
                        devicesStartPositions.Add(startPos);
                        devicesEndPositions.Add(endPos);
                    }
                }
                else
                {
                    throw new Exception($"Unable to retrieve positioner device {deviceNames[i]}.");
                }
            }

            if (devicesToMove.Count == 0)
                return;

            var deviceNamesFiltered = devicesToMove.ToArray();
            var startPositionsFiltered = devicesStartPositions.ToArray();
            var endPositionsFiltered = devicesEndPositions.ToArray();

            Dictionary<char, LeadInfo>? leadInfo = null;
            if (leadIn || leadOut)
            {
                leadInfo = new Dictionary<char, LeadInfo>();
                foreach (char name in deviceNamesFiltered)
                {
                    leadInfo[name] = new LeadInfo();
                }
            }


            // STEP 2: Map devices and controllers
            var devices = deviceNamesFiltered
                .Select(name => (success: _controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner), name, positioner))
                .Where(t => t.success)
                .ToDictionary(t => t.name, t => t.positioner);

            var controllers = devices.Values
                .ToDictionary(device => device, device =>
                {
                    if (_controllerManager.TryGetDeviceController<BasePositionerController>(device.Name, out BasePositionerController controller))
                        return controller;
                    else
                        throw new Exception($"Unable to find controller for device: {device.Name}.");
                });

            var groupedDevicesByController = devices.Values
                .GroupBy(device => controllers[device])
                .ToDictionary(group => group.Key, group => group.ToList());

            // STEP 3: Prepare main movement
            var positionerMovementInfos = new Dictionary<char, PositionerMovementInformation>();

            foreach (var name in deviceNamesFiltered)
            {
                var positioner = devices[name];
                var startPos = startPositionsFiltered[Array.IndexOf(deviceNamesFiltered, name)];
                var endPos = endPositionsFiltered[Array.IndexOf(deviceNamesFiltered, name)];
                var targetDistance = Math.Abs(endPos - startPos);
                var targetDirection = endPos > startPos;

                positionerMovementInfos[name] = new PositionerMovementInformation
                {
                    StartingPosition = startPos,
                    StartingSpeed = 0f, // Starting from rest
                    MaxAcceleration = positioner.MaxAcceleration,
                    MaxDeceleration = positioner.MaxDeceleration,
                    MaxSpeed = positioner.MaxSpeed,
                    TargetPosition = endPos,
                    TargetDirection = targetDirection,
                    TargetDistance = targetDistance,
                };
            }

            // STEP 4: Calculate line kinematic parameters
            if (!TryGetLineKinParameters(trajectorySpeed, ref positionerMovementInfos, out float allocatedTimeLine))
                throw new Exception("Failed to calculate kinematic parameters for line movement.");

            // Now that we have TargetAcceleration and TargetDeceleration, we can calculate lead-in and lead-out offsets

            // STEP 5: Adjust positions based on lead-in and lead-out
            var initialPositions = new Dictionary<char, float>();
            var needToMoveToInitialPositions = false;

            foreach (var name in deviceNamesFiltered)
            {
                var positioner = devices[name];
                var info = positionerMovementInfos[name];
                float initialPos = info.StartingPosition;

                // Adjust starting position for lead-in
                if (leadIn)
                {
                    // Calculate LeadIn offset using TargetAcceleration and TargetSpeed
                    var leadInOffset = CalculateLeadInOffset(info, out float allocatedTime_LeadIn);
                    initialPos -= leadInOffset;

                    leadInfo[name].LeadInStartPos = initialPos;
                    leadInfo[name].LeadInEndPos = info.StartingPosition;
                    leadInfo[name].LeadInAllocatedTime = allocatedTime_LeadIn;
                }

                initialPositions[name] = initialPos;
                info.StartingPosition = initialPos;
                // Adjust target position for lead-out
                if (leadOut)
                {
                    var leadOutOffset = CalculateLeadOutOffset(info, out float allocatedTime_leadOut);
                    var leadOutEndPosition = info.TargetPosition + leadOutOffset;
                    leadInfo[name].LeadOutStartPos = info.TargetPosition;
                    leadInfo[name].LeadOutEndPos = leadOutEndPosition;
                    leadInfo[name].LeadOutAllocatedTime = allocatedTime_leadOut;

                    info.TargetPosition = leadOutEndPosition;
                    // Update TargetDistance after adjusting TargetPosition
                    info.TargetDistance = Math.Abs(info.TargetPosition - info.StartingPosition);
                    positionerMovementInfos[name] = info;


                }
            }

            // Create arrays to jump to start.
            var deviceNamesToInitialize = new List<char>();
            var deviceInitialPositions = new List<float>();
            foreach (var name in deviceNamesFiltered)
            {
                var positioner = devices[name];
                //var info = positionerMovementInfos[name];
                var currentPos = positioner.CurrentPosition;
                var initialPos = initialPositions[name];

                // Check if movement to initial position is needed
                if (Math.Abs(currentPos - initialPos) > accuracy)
                {
                    needToMoveToInitialPositions = true;
                    deviceNamesToInitialize.Add(name);
                    deviceInitialPositions.Add(initialPos);
                }

            }
            // let's check for non moving devices, that need to relocate
            for (int i = 0; i < deviceNames.Length; i++)
            {
                var name = deviceNames[i];
                if (!deviceNamesFiltered.Contains(name))
                {
                    if (_controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                    {
                        var startPos = startPositions[i];
                        // check if movement to start pos is needed
                        if (Math.Abs(startPos - positioner.CurrentPosition) > accuracy)
                        {
                            needToMoveToInitialPositions = true;
                            deviceNamesToInitialize.Add(name);
                            deviceInitialPositions.Add(startPos);
                        }
                    }
                }
            }

            // STEP 6: Move to initial positions if needed
            if (needToMoveToInitialPositions)
            {

                //// Use JumpAbsoluteFunction to move devices to initial positions
                var jumpDeviceNames = deviceNamesToInitialize.ToArray();
                var jumpPositions = deviceInitialPositions.ToArray();

                // Call the ExecutionCore method of JumpAbsoluteFunction
                _jumpAbsoluteFunction.ExecutionCore(jumpDeviceNames, jumpPositions, false, accuracy, null, null, false);
            }

            // STEP 7: Recalculate allocated times for LeadIn and LeadOut
            float allocatedTimeLeadIn = 0f;
            float allocatedTimeLeadOut = 0f;

            if (leadIn)
            {
                foreach(var (name, leadInformation) in leadInfo)
                {
                    float timeToReachSpeed = leadInformation.LeadInAllocatedTime;
                    allocatedTimeLeadIn = Math.Max(allocatedTimeLeadIn, timeToReachSpeed);
                }
            }

            if (leadOut)
            {
                foreach (var (name, leadInformation) in leadInfo)
                {
                    float timeToStop = leadInformation.LeadOutAllocatedTime;
                    allocatedTimeLeadOut = Math.Max(allocatedTimeLeadOut, timeToStop);
                }
            }

            // STEP 8: Total allocated time
            float totalAllocatedTime = allocatedTimeLeadIn + allocatedTimeLine + allocatedTimeLeadOut;

            // let's update the line kin parameters
            if (!TryGetLineKinParameters(trajectorySpeed, ref positionerMovementInfos, out float totalAllocatedTime_calculated))
                throw new Exception("Failed to calculate kinematic parameters for line movement.");

            // STEP 9: Update movement settings if necessary
            List<Command> updateParametersCommandLine = CreateUpdateCommands(positionerMovementInfos, groupedDevicesByController);

            _commandManager.EnqueueCommandLine(updateParametersCommandLine.ToArray());
            _commandManager.ExecuteCommandLine(updateParametersCommandLine.ToArray()).GetAwaiter().GetResult();

            // STEP 10: Create and execute movement commands

            Dictionary<char, float>? waitUntilPosDict = null;
            float? waitUntilTIme = null;

            var movementCommandsLine = CreateMovementCommands(
                isShutterUsed,
                groupedDevicesByController,
                positionerMovementInfos,
                leadInfo,
                totalAllocatedTime_calculated - (totalAllocatedTime_calculated - allocatedTimeLine)/2, // why STANDA WHYYYYY
                waitUntilTIme,
                waitUntilPosDict,
                leadIn,
                leadOut);

            _commandManager.EnqueueCommandLine(movementCommandsLine.ToArray());
            _commandManager.ExecuteCommandLine(movementCommandsLine.ToArray()).GetAwaiter().GetResult();
        }


        // Helper Methods

        private List<Command> CreateUpdateCommands(Dictionary<char, PositionerMovementInformation> positionerMovementInfos, Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController)
        {
            var updateParametersCommandLine = new List<Command>();

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var controllerName = controllerGroup.Key.Name;
                var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();
                bool isAccelChangeNeeded = groupedDeviceNames.Any(deviceName =>
                {
                    if (_controllerManager.TryGetDevice<BasePositionerDevice>(deviceName, out BasePositionerDevice positionerDevice))
                    {
                        if (positionerDevice.Acceleration != positionerMovementInfos[deviceName].TargetAcceleration || positionerDevice.Deceleration != positionerMovementInfos[deviceName].TargetDeceleration)
                            return true;
                        else
                            return false;
                    }
                    else
                        return true;
                });

                var movementSettings = groupedDeviceNames.ToDictionary(
                    deviceName => deviceName,
                    deviceName => new MovementSettingsInfo
                    {
                        TargetAcceleration = positionerMovementInfos[deviceName].TargetAcceleration,
                        TargetDeceleration = positionerMovementInfos[deviceName].TargetDeceleration,
                        TargetSpeed = positionerMovementInfos[deviceName].TargetSpeed,
                    });

                var commandParameters = new UpdateMovementSettingsParameters
                {
                    MovementSettingsInformation = movementSettings,
                    AccelChangePending = isAccelChangeNeeded,
                };

                updateParametersCommandLine.Add(new Command
                {
                    Action = CommandDefinitions.UpdateMoveSettings,
                    Await = true,
                    Parameters = commandParameters,
                    TargetController = controllerName,
                    TargetDevices = groupedDeviceNames
                });
            }

            return updateParametersCommandLine;
        }


        private float CalculateLeadInOffset(PositionerMovementInformation info, out float totalTime)
        {
            float acceleration = info.TargetAcceleration;
            float targetSpeed = info.TargetSpeed;
            float directionMultiplier = info.TargetDirection ? 1 : -1;

            // Acceleration ramp-up time
            float t_jerk = JerkTime;

            // Velocity at the end of jerk phase
            float v_jerk = (acceleration * t_jerk) / 2;

            // Time to reach target speed after jerk phase
            float t_const_accel = (targetSpeed - v_jerk) / acceleration;

            // Total time
            totalTime = t_jerk + t_const_accel;

            // Distance during jerk phase
            float s_jerk = (acceleration * t_jerk * t_jerk) / 6;

            // Distance during constant acceleration
            float s_const_accel = v_jerk * t_const_accel + 0.5f * acceleration * t_const_accel * t_const_accel;

            float offset = s_jerk + s_const_accel;

            // Adjust offset based on direction
            offset *= directionMultiplier;

            return offset;
        }

        private float CalculateLeadOutOffset(PositionerMovementInformation info, out float totalTime)
        {
            float deceleration = info.TargetDeceleration;
            float targetSpeed = info.TargetSpeed;
            float directionMultiplier = info.TargetDirection ? 1 : -1;

            // Deceleration ramp-up time
            float t_jerk = JerkTime;

            // Velocity at the start of deceleration jerk phase
            float v_jerk = (deceleration * t_jerk) / 2;

            // Time to decelerate from target speed to v_jerk
            float t_const_decel = (targetSpeed - v_jerk) / deceleration;

            // Total time
            totalTime = t_jerk + t_const_decel;

            // Distance during jerk phase
            float s_jerk = (deceleration * t_jerk * t_jerk) / 6;

            // Distance during constant deceleration
            float s_const_decel = v_jerk * t_const_decel + 0.5f * deceleration * t_const_decel * t_const_decel;

            float offset = s_jerk + s_const_decel;

            // Adjust offset based on direction
            offset *= directionMultiplier;

            return offset;
        }


        //private float CalculateLeadInOffset(PositionerMovementInformation info, out float timeToReachSpeed)
        //{
        //    float acceleration = info.TargetAcceleration;
        //    timeToReachSpeed = info.TargetSpeed / acceleration;
        //    float offset = 0.5f * acceleration * timeToReachSpeed * timeToReachSpeed;
        //    // Adjust offset based on direction
        //    if (!info.TargetDirection)
        //        offset = -offset;
        //    return offset;
        //}

        //private float CalculateLeadOutOffset(PositionerMovementInformation info, out float timeToStop)
        //{
        //    float deceleration = info.TargetDeceleration;
        //    timeToStop = info.TargetSpeed / deceleration;
        //    float offset = 0.5f * deceleration * timeToStop * timeToStop;
        //    // Adjust offset based on direction
        //    if (!info.TargetDirection)
        //        offset = -offset;
        //    return offset;
        //}


        private bool TryGetLineKinParameters(
    float trajectorySpeed,
    ref Dictionary<char, PositionerMovementInformation> positionerMovementInfos,
    out float allocatedTime)
        {
            char[] deviceNames = positionerMovementInfos.Keys.ToArray();
            Dictionary<char, float> movementRatio = new Dictionary<char, float>();

            //Calculate the initial and final tool positions
            var startToolPoint = _controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfos.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.StartingPosition)
                );

            var endToolPoint = _controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfos.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.TargetPosition)
                );

            if (startToolPoint == endToolPoint)
            {
                throw new Exception("Error encountered, when trying to get kinematic parameters. Starting point and end point are the same.");
            }

            // TODO: calculate the speed according to DefaultSpeed of positioners used.

            float trajectorySpeedCalculated = trajectorySpeed;

            // Calculate trajectory length
            float trajectoryLength = (endToolPoint - startToolPoint).Length();


            // Calculate the target kinematic parameters
            var projectedMaxAccelerations = new Dictionary<char, float>();
            var projectedMaxDecelerations = new Dictionary<char, float>();
            var projectedMaxSpeeds = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                movementRatio[name] = trajectoryLength / positionerMovementInfos[name].TargetDistance;
                projectedMaxAccelerations[name] = positionerMovementInfos[name].MaxAcceleration * movementRatio[name];
                projectedMaxDecelerations[name] = positionerMovementInfos[name].MaxDeceleration * movementRatio[name];
                projectedMaxSpeeds[name] = positionerMovementInfos[name].MaxSpeed * movementRatio[name];
            }
            var projectedMaxAcceleration = projectedMaxAccelerations.Min(kvp => kvp.Value);
            var projectedMaxDeceleration = projectedMaxDecelerations.Min(kvp => kvp.Value);
            var projectedMaxSpeed = Math.Min(trajectorySpeedCalculated, projectedMaxSpeeds.Min(kvp => kvp.Value));

            var timesToAccel = new Dictionary<char, float>();
            var timesToDecel = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                positionerMovementInfos[name].MaxAcceleration = projectedMaxAcceleration / movementRatio[name];
                positionerMovementInfos[name].MaxDeceleration = projectedMaxDeceleration / movementRatio[name];
                positionerMovementInfos[name].TargetSpeed = projectedMaxSpeed / movementRatio[name];

                // TODO: check if direction is needed here. Also include addiotional deceleration when changing directions.

                int direction = positionerMovementInfos[name].TargetDirection ? 1 : -1;
                timesToAccel[name] = Math.Abs(positionerMovementInfos[name].TargetSpeed * direction - positionerMovementInfos[name].StartingSpeed) / positionerMovementInfos[name].MaxAcceleration;
                timesToDecel[name] = Math.Abs(positionerMovementInfos[name].TargetSpeed * direction - 0) / positionerMovementInfos[name].MaxDeceleration;
            }

            var maxTimeToAccel = timesToAccel.Max(kvp => kvp.Value);
            var maxTimeToDecel = timesToAccel.Max(kvp => kvp.Value);

            foreach (char name in deviceNames)
            {
                positionerMovementInfos[name].TargetAcceleration = positionerMovementInfos[name].MaxAcceleration * (timesToAccel[name] / maxTimeToAccel);
                positionerMovementInfos[name].TargetDeceleration = positionerMovementInfos[name].MaxDeceleration * (timesToDecel[name] / maxTimeToDecel);
            }

            var selectedPosInfo = positionerMovementInfos.Where(kvp => kvp.Value.TargetAcceleration > 0).First();
            var projectedAccel = positionerMovementInfos[selectedPosInfo.Key].TargetAcceleration * movementRatio[selectedPosInfo.Key];
            var projectedDecel = positionerMovementInfos[selectedPosInfo.Key].TargetDeceleration * movementRatio[selectedPosInfo.Key];
            var projectedTargetSpeed = positionerMovementInfos[selectedPosInfo.Key].TargetSpeed * movementRatio[selectedPosInfo.Key];

            allocatedTime = CalculateTotalTimeForMovementInfo(selectedPosInfo.Value);

            return true;
        }


        private float CalculateTotalTimeForMovementInfo(PositionerMovementInformation info)
        {
            float x0 = info.StartingPosition;
            float v0 = info.StartingSpeed;
            float vt = info.TargetSpeed;
            float a = info.TargetAcceleration;
            float d = info.TargetDeceleration;
            float x_target = info.TargetPosition;

            // Calculate total movement direction
            float deltaX_total = x_target - x0;
            float direction = Math.Sign(deltaX_total); // +1 for positive, -1 for negative

            // Adjust initial speed to movement direction
            float v0_dir = v0 * direction;

            // Keep accelerations and speeds positive
            a = Math.Abs(a);
            d = Math.Abs(d);
            vt = Math.Abs(vt);

            float totalTime = 0f;

            // If initial speed is in the opposite direction, decelerate to zero first
            if (v0_dir < 0)
            {
                // Time to decelerate to zero speed
                float t_stop = -v0_dir / d;
                totalTime += t_stop;
                v0_dir = 0; // Reset initial speed after stopping
            }

            // Remaining distance after any initial deceleration
            float deltaX_remaining = Math.Abs(deltaX_total);

            // Calculate velocities at the end of acceleration and deceleration jerk phases
            float v_accel_jerk = (a * JerkTime) / 2;
            float v_decel_jerk = (d * JerkTime) / 2;

            // Time after jerk phases to reach target speed
            float t_accel_const = (vt - v_accel_jerk) / a;
            float t_decel_const = (vt - v_decel_jerk) / d;

            // Total acceleration and deceleration times
            float t1 = JerkTime + t_accel_const;
            float t3 = JerkTime + t_decel_const;

            // Distances during acceleration and deceleration
            float s_accel = (a * JerkTime * JerkTime) / 6 + v_accel_jerk * t_accel_const + 0.5f * a * t_accel_const * t_accel_const;
            float s_decel = (d * JerkTime * JerkTime) / 6 + v_decel_jerk * t_decel_const + 0.5f * d * t_decel_const * t_decel_const;

            // Total distance required for acceleration and deceleration
            float s_total_required = s_accel + s_decel;

            if (s_total_required > deltaX_remaining)
            {
                // Not enough distance for acceleration and deceleration
                // Adjust target speed (vt) accordingly
                vt = (float)Math.Sqrt((2 * a * d * deltaX_remaining) / (a + d));
                // Recalculate times and distances with adjusted vt
                v_accel_jerk = (a * JerkTime) / 2;
                t_accel_const = (vt - v_accel_jerk) / a;
                t1 = JerkTime + t_accel_const;
                s_accel = (a * JerkTime * JerkTime) / 6 + v_accel_jerk * t_accel_const + 0.5f * a * t_accel_const * t_accel_const;

                v_decel_jerk = (d * JerkTime) / 2;
                t_decel_const = (vt - v_decel_jerk) / d;
                t3 = JerkTime + t_decel_const;
                s_decel = (d * JerkTime * JerkTime) / 6 + v_decel_jerk * t_decel_const + 0.5f * d * t_decel_const * t_decel_const;

                s_total_required = s_accel + s_decel;
            }

            // Distance and time for constant speed phase
            float s2 = deltaX_remaining - s_total_required;
            float t2 = s2 / vt;

            totalTime += t1 + t2 + t3;

            return totalTime;
        }



        private List<Command> CreateMovementCommands(
    bool isShutterUsed,
    Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController,
    Dictionary<char, PositionerMovementInformation> positionerMovementInfos,
    Dictionary<char, LeadInfo>? leadInfo,
    float allocatedTime,
    float? waitUntilTime,
    Dictionary<char, float>? waitUntilPosDict,
    bool leadIn,
    bool leadOut)
        {
            var commandsMovement = new List<Command>();

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var controller = controllerGroup.Key;
                var controllerName = controller.Name;
                var devicesInGroup = controllerGroup.Value;
                var groupedDeviceNames = devicesInGroup.Select(device => device.Name).ToArray();

                var positionerInfos = new Dictionary<char, PositionerInfo>();
                foreach (var device in devicesInGroup)
                {
                    var deviceName = device.Name;
                    var info = positionerMovementInfos[deviceName];

                    

                    //// Handle WaitUntil
                    //float? waitUntil = null;
                    //if (waitUntilCondition.HasValue)
                    //{
                    //    switch (waitUntilCondition.Value)
                    //    {
                    //        case WaitUntilCondition.LeadInEnd:
                    //            waitUntil = leadIn ? leadInfo?.LeadInAllocatedTime : (float?)null;
                    //            break;
                    //        case WaitUntilCondition.LeadOutStart:
                    //            waitUntil = leadOut ? allocatedTime - (leadInfo?.LeadOutAllocatedTime ?? 0f) : (float?)null;
                    //            break;
                    //    }
                    //}

                    float? waitUntilPos = null;
                    if (waitUntilPosDict is not null && waitUntilPosDict.ContainsKey(deviceName))
                        waitUntilPos = waitUntilPosDict[deviceName];

                    LeadInfo? leadInformation = null;
                    if (leadInfo is not null && leadInfo.ContainsKey(deviceName))
                        leadInformation = leadInfo[deviceName];

                    positionerInfos[deviceName] = new PositionerInfo
                    {
                        LeadInformation = leadInformation,
                        WaitUntilPosition = waitUntilPos,
                        TargetSpeed = info.TargetSpeed,
                        Direction = info.TargetDirection,
                        TargetPosition = info.TargetPosition,
                    };
                }

                // Build ShutterInfo if shutter is used
                ShutterInfo? shutterInfo = null;
                if (isShutterUsed)
                {
                    // Assuming that DelayOn and DelayOff are relative to the movement start time
                    float delayOn = leadIn ? positionerInfos.Values.Max(pi => pi.LeadInformation?.LeadInAllocatedTime ?? 0f) : 0f;
                    float delayOff = leadOut ? allocatedTime - positionerInfos.Values.Max(pi => pi.LeadInformation?.LeadOutAllocatedTime ?? 0f) : allocatedTime;

                    shutterInfo = new ShutterInfo
                    {
                        DelayOn = delayOn,
                        DelayOff = delayOff,
                    };
                }

                var moveAParameters = new MoveAbsoluteParameters
                {
                    WaitUntilTime = waitUntilTime, // Set if there's a global wait until time
                    IsShutterUsed = isShutterUsed,
                    IsLeadOutUsed = leadOut,
                    IsLeadInUsed = leadIn,
                    AllocatedTime = allocatedTime,
                    PositionerInfo = positionerInfos,
                    ShutterInfo = shutterInfo,
                };

                commandsMovement.Add(new Command
                {
                    Action = CommandDefinitions.MoveAbsolute,
                    Await = true,
                    Parameters = moveAParameters,
                    TargetController = controllerName,
                    TargetDevices = groupedDeviceNames
                });
            }

            return commandsMovement;
        }

        private bool TryParseArguments(object?[] arguments, out char[] devNames, out float[] startPositions, out float[] endPositions)
        {
            devNames = Array.Empty<char>();
            startPositions = Array.Empty<float>();
            endPositions = Array.Empty<float>();

            if (arguments == null || arguments.Length == 0)
                return false;

            if (arguments[0] is not string firstArg)
                return false;

            devNames = firstArg.ToCharArray();
            int expectedPositionsCount = devNames.Length;

            if (arguments.Length < 1 + expectedPositionsCount * 2)
                return false;

            startPositions = new float[expectedPositionsCount];
            for (int i = 0; i < expectedPositionsCount; i++)
            {
                if (!TryConvertToFloat(arguments[i + 1], out startPositions[i]))
                    return false;
            }

            endPositions = new float[expectedPositionsCount];
            for (int i = 0; i < expectedPositionsCount; i++)
            {
                if (!TryConvertToFloat(arguments[i + 1 + expectedPositionsCount], out endPositions[i]))
                    return false;
            }

            return true;
        }

        private bool TryConvertToFloat(object? obj, out float value)
        {
            value = 0f;
            if (obj == null)
                return false;

            switch (obj)
            {
                case float f:
                    value = f;
                    return true;
                case double d:
                    value = (float)d;
                    return true;
                case int i:
                    value = i;
                    return true;
                default:
                    return float.TryParse(obj.ToString(), out value);
            }
        }
    }
}
