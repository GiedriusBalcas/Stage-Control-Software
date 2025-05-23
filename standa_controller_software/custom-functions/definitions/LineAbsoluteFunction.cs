﻿using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.custom_functions.helpers;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.devices;
using standa_controller_software.device_manager.devices.shutter;
using System.Globalization;
using text_parser_library;

namespace standa_controller_software.custom_functions.definitions
{
    public class LineAbsoluteFunction : CustomFunction
    {
        public enum WaitUntilCondition
        {
            LeadInEnd,
            LeadOutStart
        }


        public string Message { get; set; } = "";
        private readonly CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;
        private readonly JumpAbsoluteFunction _jumpAbsoluteFunction;
        private readonly ChangeShutterStateFunction changeShutterStateFunction;
        private readonly float JerkTime = 0.0000003f;

        public LineAbsoluteFunction(CommandManager commandManager, ControllerManager controllerManager, JumpAbsoluteFunction jumpFunction, ChangeShutterStateFunction changeShutterStateFunction)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            _jumpAbsoluteFunction = jumpFunction;
            this.changeShutterStateFunction = changeShutterStateFunction;
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
            var isShutterUsed = isShutterUsedObj is null? false : (bool)isShutterUsedObj;

            if (!TryGetProperty("Accuracy", out var accuracyObj))
                throw new Exception("Failed to get 'Accuracy' property.");
            if (!TryConvertToFloat(accuracyObj, out float accuracy))
                throw new Exception("Failed to get 'Accuracy' property.");

            if (!TryGetProperty("Speed", out var trajSpeedObj))
                throw new Exception("Failed to get 'Speed' property.");
            if(!TryConvertToFloat(trajSpeedObj, out float trajectorySpeed))
                throw new Exception("Failed to get 'Speed' property.");


            if (!TryGetProperty("LeadIn", out var leadInObj))
                throw new Exception("Failed to get 'LeadIn' property.");
            var leadIn = leadInObj is null? false : (bool)leadInObj;

            if (!TryGetProperty("LeadOut", out var leadOutObj))
                throw new Exception("Failed to get 'LeadOut' property.");
            var leadOut = leadOutObj is null? false : (bool)leadOutObj;

            ExecutionCore(parsedDeviceNames, parsedStartPositions, parsedEndPositions, trajectorySpeed, isShutterUsed, accuracy, leadIn, leadOut);

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
            bool leadOut)
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
                leadInfo = [];
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
                var targetDirection = endPos >= startPos;

                positionerMovementInfos[name] = new PositionerMovementInformation
                {
                    PositionerParameters = new PositionerParameters
                    {
                        MaxAcceleration = positioner.MaxAcceleration,
                        MaxDeceleration = positioner.MaxDeceleration,
                        MaxSpeed = positioner.MaxSpeed,
                    },
                    StartingMovementParameters = new StartingMovementParameters
                    {
                        Position = startPos,
                        Speed = 0f,
                    },
                    TargetMovementParameters = new TargetMovementParameters
                    {
                        Position = endPos,
                        Direction = targetDirection,
                        Distance = targetDistance
                    },
                    KinematicParameters = new KinematicParameters
                    {
                        ConstantSpeedEndPosition = 0f,
                        ConstantSpeedEndTime = 0f,
                        ConstantSpeedStartPosition = 0f,
                        ConstantSpeedStartTime = 0f,
                        TotalTime = 0f,
                    }
                };
            }

            // STEP 4: Calculate line kinematic parameters
            if (!TryGetLineKinParametersInitial(trajectorySpeed, ref positionerMovementInfos))
                throw new Exception("Failed to calculate kinematic parameters for line movement.");
            


            // Now that we have TargetAcceleration and TargetDeceleration, we can calculate lead-in and lead-out offsets

            // STEP 5: Adjust positions based on lead-in and lead-out
            var initialPositions = new Dictionary<char, float>();
            var needToMoveToInitialPositions = false;

            foreach (var name in deviceNamesFiltered)
            {
                var positioner = devices[name];
                var info = positionerMovementInfos[name];
                float initialPos = info.StartingMovementParameters.Position;

                // Adjust starting position for lead-in
                if (leadIn && leadInfo is not null)
                {
                    //info.TargetMovementParameters.TargetSpeed = trajectorySpeed;
                    // Calculate LeadIn offset using TargetAcceleration and TargetSpeed
                    var leadInOffset = CalculateLeadInOffset(info, out float allocatedTime_LeadIn);
                    initialPos -= leadInOffset;

                    leadInfo[name].LeadInStartPos = initialPos;
                    leadInfo[name].LeadInEndPos = info.StartingMovementParameters.Position;
                    leadInfo[name].LeadInAllocatedTime = allocatedTime_LeadIn;

                    positionerMovementInfos[name] = info;
                }

                initialPositions[name] = initialPos;
                info.StartingMovementParameters.Position = initialPos;
                // Adjust target position for lead-out
                if (leadOut && leadInfo is not null)
                {
                    var leadOutOffset = CalculateLeadOutOffset(info, out float allocatedTime_leadOut);
                    var leadOutEndPosition = info.TargetMovementParameters.Position + leadOutOffset;
                    leadInfo[name].LeadOutStartPos = info.TargetMovementParameters.Position;
                    leadInfo[name].LeadOutEndPos = leadOutEndPosition;
                    leadInfo[name].LeadOutAllocatedTime = allocatedTime_leadOut;

                    info.TargetMovementParameters.Position= leadOutEndPosition;
                    // Update TargetDistance after adjusting TargetPosition
                    info.TargetMovementParameters.Distance = Math.Abs(info.TargetMovementParameters.Position- info.StartingMovementParameters.Position);
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

            if (leadIn && leadInfo is not null)
            {
                foreach (var (name, leadInformation) in leadInfo)
                {
                    float timeToReachSpeed = leadInformation.LeadInAllocatedTime;
                    allocatedTimeLeadIn = Math.Max(allocatedTimeLeadIn, timeToReachSpeed);
                }
            }

            if (leadOut && leadInfo is not null)
            {
                foreach (var (name, leadInformation) in leadInfo)
                {
                    float timeToStop = leadInformation.LeadOutAllocatedTime;
                    allocatedTimeLeadOut = Math.Max(allocatedTimeLeadOut, timeToStop);
                }
            }

            // let's update the line kin parameters
            if (!TryGetLineKinParameters(trajectorySpeed, ref positionerMovementInfos, out float timeToAccel_recalc, out float timeToDecel_recalc, out float totalTime_recalc))
                throw new Exception("Failed to calculate kinematic parameters for line movement.");

            // let's fill the kinematic parameter table
            foreach (var (name, posInformation) in positionerMovementInfos)
            {
                posInformation.KinematicParameters.ConstantSpeedStartPosition = leadInfo is not null? leadInfo[name].LeadInEndPos : posInformation.StartingMovementParameters.Position;
                posInformation.KinematicParameters.ConstantSpeedStartTime = timeToAccel_recalc;

                posInformation.KinematicParameters.ConstantSpeedEndPosition = leadInfo is not null ? leadInfo[name].LeadOutStartPos : posInformation.TargetMovementParameters.Position;
                posInformation.KinematicParameters.ConstantSpeedEndTime= totalTime_recalc - timeToDecel_recalc;

                posInformation.KinematicParameters.TotalTime = totalTime_recalc;
            }
            // STEP 9: Update movement settings if necessary
            List<Command> updateParametersCommandLine = CreateUpdateCommands(positionerMovementInfos, groupedDevicesByController);

            _commandManager.EnqueueCommandLine([.. updateParametersCommandLine]);
            _commandManager.TryExecuteCommandLine([.. updateParametersCommandLine]).GetAwaiter().GetResult();

            // STEP 10: Create and execute movement commands

            Dictionary<char, float>? waitUntilPosDict = null;
            float? waitUntilTIme = null;

            var movementCommandsLine = CreateMovementCommands(
                isShutterUsed,
                groupedDevicesByController,
                positionerMovementInfos,
                leadInfo,
                //totalAllocatedTime_calculated - (totalAllocatedTime_calculated - allocatedTimeLine)/2, // why STANDA WHYYYYY
                totalTime_recalc - timeToDecel_recalc / 2,
                waitUntilTIme,
                waitUntilPosDict,
                leadIn,
                leadOut);


            if (isShutterUsed)
            {
                var shutterDevice = _controllerManager.GetDevices<ShutterDevice>().FirstOrDefault() ?? throw new Exception("Shutter device not present.");
                changeShutterStateFunction.ExecutionCore([shutterDevice.Name], true);
            }

            _commandManager.EnqueueCommandLine([.. movementCommandsLine]);
            _commandManager.TryExecuteCommandLine([.. movementCommandsLine]).GetAwaiter().GetResult();

            if (isShutterUsed)
            {
                var shutterDevice = _controllerManager.GetDevices<ShutterDevice>().FirstOrDefault() ?? throw new Exception("Shutter device not present.");
                changeShutterStateFunction.ExecutionCore([shutterDevice.Name], false);
            }
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
                        if (positionerDevice.Acceleration != positionerMovementInfos[deviceName].TargetMovementParameters.Acceleration || positionerDevice.Deceleration != positionerMovementInfos[deviceName].TargetMovementParameters.Deceleration)
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
                        TargetAcceleration = positionerMovementInfos[deviceName].TargetMovementParameters.Acceleration,
                        TargetDeceleration = positionerMovementInfos[deviceName].TargetMovementParameters.Deceleration,
                        TargetSpeed = positionerMovementInfos[deviceName].TargetMovementParameters.TargetSpeed,
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
            float acceleration = info.TargetMovementParameters.Acceleration;
            float targetSpeed = info.TargetMovementParameters.TargetSpeed;
            float directionMultiplier = info.TargetMovementParameters.Direction ? 1 : -1;

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
            float deceleration = info.TargetMovementParameters.Deceleration;
            float targetSpeed = info.TargetMovementParameters.TargetSpeed;
            float directionMultiplier = info.TargetMovementParameters.Direction ? 1 : -1;

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


        private bool TryGetLineKinParameters(
        float trajectorySpeed,
        ref Dictionary<char, PositionerMovementInformation> positionerMovementInfos,
        out float timeToAccel, out float timeToDecel, out float totalTime)
        {
            char[] deviceNames = [.. positionerMovementInfos.Keys];
            Dictionary<char, float> movementRatio = [];

            if (_controllerManager.ToolInformation is null)
                throw new Exception("Unable to fetch tool information.");

            //Calculate the initial and final tool positions
            var startToolPoint = _controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfos.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.StartingMovementParameters.Position)
                );

            var endToolPoint = _controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfos.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.TargetMovementParameters.Position)
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
                movementRatio[name] = trajectoryLength / positionerMovementInfos[name].TargetMovementParameters.Distance;
                projectedMaxAccelerations[name] = positionerMovementInfos[name].PositionerParameters.MaxAcceleration * movementRatio[name];
                projectedMaxDecelerations[name] = positionerMovementInfos[name].PositionerParameters.MaxDeceleration * movementRatio[name];
                projectedMaxSpeeds[name] = positionerMovementInfos[name].PositionerParameters.MaxSpeed * movementRatio[name];
            }
            var projectedMaxAcceleration = projectedMaxAccelerations.Min(kvp => kvp.Value);
            var projectedMaxDeceleration = projectedMaxDecelerations.Min(kvp => kvp.Value);
            var projectedMaxSpeed = Math.Min(trajectorySpeedCalculated, projectedMaxSpeeds.Min(kvp => kvp.Value));

            var timesToAccel = new Dictionary<char, float>();
            var timesToDecel = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                positionerMovementInfos[name].TargetMovementParameters.Acceleration = projectedMaxAcceleration / movementRatio[name];
                positionerMovementInfos[name].TargetMovementParameters.Deceleration = projectedMaxDeceleration / movementRatio[name];
                positionerMovementInfos[name].TargetMovementParameters.TargetSpeed = projectedMaxSpeed / movementRatio[name];

                int direction = positionerMovementInfos[name].TargetMovementParameters.Direction ? 1 : -1;
                timesToAccel[name] = Math.Abs(positionerMovementInfos[name].TargetMovementParameters.TargetSpeed * direction - positionerMovementInfos[name].StartingMovementParameters.Speed) / positionerMovementInfos[name].TargetMovementParameters.Acceleration;
                timesToDecel[name] = Math.Abs(positionerMovementInfos[name].TargetMovementParameters.TargetSpeed * direction - 0) / positionerMovementInfos[name].TargetMovementParameters.Deceleration;
            }

            var maxTimeToAccel = timesToAccel.Max(kvp => kvp.Value);
            var maxTimeToDecel = timesToAccel.Max(kvp => kvp.Value);

            foreach (char name in deviceNames)
            {
                positionerMovementInfos[name].TargetMovementParameters.Acceleration = positionerMovementInfos[name].TargetMovementParameters.Acceleration * (timesToAccel[name] / maxTimeToAccel);
                positionerMovementInfos[name].TargetMovementParameters.Deceleration = positionerMovementInfos[name].TargetMovementParameters.Deceleration * (timesToDecel[name] / maxTimeToDecel);
            }

            var selectedPosInfo = positionerMovementInfos.Where(kvp => kvp.Value.TargetMovementParameters.Acceleration > 0).First().Value;
            //var projectedAccel = positionerMovementInfos[selectedPosInfo.Key].TargetAcceleration * movementRatio[selectedPosInfo.Key];
            //var projectedDecel = positionerMovementInfos[selectedPosInfo.Key].TargetDeceleration * movementRatio[selectedPosInfo.Key];
            //var projectedTargetSpeed = positionerMovementInfos[selectedPosInfo.Key].TargetSpeed * movementRatio[selectedPosInfo.Key];

            //allocatedTime = CalculateTotalTimeForMovementInfo(selectedPosInfo.Value, out timeToAccel, out timeToDecel, out totalTime);
            CustomFunctionHelper.CalculateKinParametersForMovementInfo(ref selectedPosInfo);


            totalTime = selectedPosInfo.KinematicParameters.TotalTime;
            timeToAccel = selectedPosInfo.KinematicParameters.ConstantSpeedStartTime;
            timeToDecel = totalTime - selectedPosInfo.KinematicParameters.ConstantSpeedEndTime;


            return true;
        }

        private bool TryGetLineKinParametersInitial(
        float trajectorySpeed,
        ref Dictionary<char, PositionerMovementInformation> positionerMovementInfos)
        {
            char[] deviceNames = [.. positionerMovementInfos.Keys];
            Dictionary<char, float> movementRatio = [];


            if (_controllerManager.ToolInformation is null)
                throw new Exception("Unable to fetch tool information.");

            //Calculate the initial and final tool positions
            var startToolPoint = _controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfos.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.StartingMovementParameters.Position)
                );

            var endToolPoint = _controllerManager.ToolInformation.CalculateToolPositionUpdate
                (
                    positionerMovementInfos.ToDictionary(positionerInfo => positionerInfo.Key, kvp => kvp.Value.TargetMovementParameters.Position)
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
                movementRatio[name] = trajectoryLength / positionerMovementInfos[name].TargetMovementParameters.Distance;
                projectedMaxAccelerations[name] = positionerMovementInfos[name].PositionerParameters.MaxAcceleration * movementRatio[name];
                projectedMaxDecelerations[name] = positionerMovementInfos[name].PositionerParameters.MaxDeceleration * movementRatio[name];
                projectedMaxSpeeds[name] = positionerMovementInfos[name].PositionerParameters.MaxSpeed * movementRatio[name];
            }
            var projectedMaxAcceleration = projectedMaxAccelerations.Min(kvp => kvp.Value);
            var projectedMaxDeceleration = projectedMaxDecelerations.Min(kvp => kvp.Value);
            var projectedMaxSpeed = Math.Min(trajectorySpeedCalculated, projectedMaxSpeeds.Min(kvp => kvp.Value));

            var timesToAccel = new Dictionary<char, float>();
            var timesToDecel = new Dictionary<char, float>();

            foreach (char name in deviceNames)
            {
                positionerMovementInfos[name].TargetMovementParameters.Acceleration = projectedMaxAcceleration / movementRatio[name];
                positionerMovementInfos[name].TargetMovementParameters.Deceleration = projectedMaxDeceleration / movementRatio[name];
                positionerMovementInfos[name].TargetMovementParameters.TargetSpeed = projectedMaxSpeed / movementRatio[name];

                int direction = positionerMovementInfos[name].TargetMovementParameters.Direction ? 1 : -1;
                timesToAccel[name] = Math.Abs(positionerMovementInfos[name].TargetMovementParameters.TargetSpeed * direction - positionerMovementInfos[name].StartingMovementParameters.Speed) / positionerMovementInfos[name].TargetMovementParameters.Acceleration;
                timesToDecel[name] = Math.Abs(positionerMovementInfos[name].TargetMovementParameters.TargetSpeed * direction - 0) / positionerMovementInfos[name].TargetMovementParameters.Deceleration;
            }

            var maxTimeToAccel = timesToAccel.Max(kvp => kvp.Value);
            var maxTimeToDecel = timesToDecel.Max(kvp => kvp.Value);

            foreach (char name in deviceNames)
            {
                positionerMovementInfos[name].TargetMovementParameters.Acceleration = positionerMovementInfos[name].TargetMovementParameters.Acceleration * (timesToAccel[name] / maxTimeToAccel);
                positionerMovementInfos[name].TargetMovementParameters.Deceleration = positionerMovementInfos[name].TargetMovementParameters.Deceleration * (timesToDecel[name] / maxTimeToDecel);
            }

            var selectedPosInfo = positionerMovementInfos.Where(kvp => kvp.Value.TargetMovementParameters.Acceleration > 0).First().Value;
            
            return true;
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

                    float? waitUntilPos = null;
                    if (waitUntilPosDict is not null && waitUntilPosDict.TryGetValue(deviceName, out float value))
                        waitUntilPos = value;

                    LeadInfo? leadInformation = null;
                    if (leadInfo is not null && leadInfo.TryGetValue(deviceName, out LeadInfo? leadInfoValue))
                        leadInformation = leadInfoValue;

                    positionerInfos[deviceName] = new PositionerInfo
                    {
                        LeadInformation = leadInformation,
                        WaitUntilPosition = waitUntilPos,
                        TargetSpeed = info.TargetMovementParameters.TargetSpeed,
                        Direction = info.TargetMovementParameters.Direction,
                        TargetPosition = info.TargetMovementParameters.Position,
                        MovementInformation = new MovementInformation()
                        {
                            StartPosition = positionerMovementInfos[deviceName].StartingMovementParameters.Position,
                            EndPosition = positionerMovementInfos[deviceName].TargetMovementParameters.Position,
                            TotalTime = positionerMovementInfos[deviceName].KinematicParameters.TotalTime,
                            ConstantSpeedStartTime = positionerMovementInfos[deviceName].KinematicParameters.ConstantSpeedStartTime,
                            ConstantSpeedEndTime = positionerMovementInfos[deviceName].KinematicParameters.ConstantSpeedEndTime,
                            ConstantSpeedEndPosition = positionerMovementInfos[deviceName].KinematicParameters.ConstantSpeedEndPosition,
                            ConstantSpeedStartPosition = positionerMovementInfos[deviceName].KinematicParameters.ConstantSpeedStartPosition,
                        }
                    };
                }

                // Build ShutterInfo if shutter is used
                ShutterInfo shutterInfo = new();
                if (isShutterUsed)
                {
                    var shutterDevice = _controllerManager.GetDevices<ShutterDevice>().First();

                    // Assuming that DelayOn and DelayOff are relative to the movement start time
                    float delayOn = leadIn ? Math.Max(positionerInfos.Values.Max(pi => pi.LeadInformation?.LeadInAllocatedTime * 1000f ?? 0f) - shutterDevice.DelayOn, 0) : 0f;
                    float delayOff = leadOut ? Math.Max(positionerInfos.Values.Max(pi => pi.LeadInformation?.LeadOutAllocatedTime * 1000f ?? 0f) + shutterDevice.DelayOff, 0) : 0f;

                    //float delayOn = leadIn ? Math.Max(positionerMovementInfos.Values.Max(pi => pi.KinematicParameters.ConstantSpeedStartTime* 1000f) - shutterDevice.DelayOn, 0) : 0f;
                    //float delayOff = leadOut ? Math.Max(positionerMovementInfos.Values.Max(pi => pi.KinematicParameters.ConstantSpeedEndTime * 1000f) - shutterDevice.DelayOn, 0) : 0f;



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
                    TargetDevices = groupedDeviceNames,
                    EstimatedTime = positionerMovementInfos.Values.Select(info => info.KinematicParameters.TotalTime).Max()
                });
            }

            return commandsMovement;
        }

        private static bool TryParseArguments(object?[] arguments, out char[] devNames, out float[] startPositions, out float[] endPositions)
        {
            devNames = [];
            startPositions = [];
            endPositions = [];

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

        private static bool TryConvertToFloat(object? obj, out float value)
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
                case string s:
                    return float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
                default:
                    return float.TryParse(obj.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
            }
        }
    }

}