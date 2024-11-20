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
using System;
using System.Collections.Generic;
using System.Linq;

namespace standa_controller_software.custom_functions.definitions
{
    public class JumpAbsoluteFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private readonly CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;
        private readonly ChangeShutterStateFunction changeShutterStateFunction;

        public JumpAbsoluteFunction(CommandManager commandManager, ControllerManager controllerManager, ChangeShutterStateFunction changeShutterStateFunction)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            this.changeShutterStateFunction = changeShutterStateFunction;
            SetProperty("Shutter", false);
            SetProperty("Accuracy", 0.05f);
            SetProperty("LeadOut", false);
            SetProperty("WaitUntilTime", null, true);
            SetProperty("Blending", false);
        }


        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out var parsedDeviceNames, out var parsedPositions, out var parsedWaitUntil))
                throw new ArgumentException("Argument parsing was unsuccessful. Wrong types.");

            if (!TryGetProperty("Shutter", out var isShutterUsedObj))
                throw new Exception("Failed to get 'Shutter' property.");
            var isShutterUsed = (bool)isShutterUsedObj;

            if (!TryGetProperty("Blending", out var blendingObj))
                throw new Exception("Failed to get 'Blending' property.");
            var blending = (bool)blendingObj;

            if (!TryGetProperty("Accuracy", out var accuracyObj))
                throw new Exception("Failed to get 'Accuracy' property.");
            var accuracy = (float)accuracyObj;

            if (!TryGetProperty("WaitUntilTime", out var waitUntilTimeObj))
                throw new Exception("Failed to get 'WaitUntilTime' property.");
            float? waitUntilTime = waitUntilTimeObj is null ? null : (float)waitUntilTimeObj;


            ExecutionCore(parsedDeviceNames, parsedPositions, isShutterUsed, accuracy, waitUntilTime, parsedWaitUntil, blending);

            return null;
        }

        public void ExecutionCore(char[] parsedDeviceNames, float[] parsedPositions, bool isShutterUsed, float accuracy, float? waitUntilTime, float[]? parsedWaitUntilPos, bool blending)
        {

            /// Jump command, starts movement from initial position & speed
            /// to a target position.
            /// Trajectory is ignored, unlike the Line command.
            /// Jerk is ignored.


            var deviceNameList = new List<char>();
            var positionsList = new List<float>();
            var waitUntilPosList = new List<float>();

            // Build lists of devices that need to move
            for (int i = 0; i < parsedDeviceNames.Length; i++)
            {
                if (_controllerManager.TryGetDevice<BasePositionerDevice>(parsedDeviceNames[i], out var positioner))
                {
                    var isAtTargetPosition = Math.Abs(positioner.CurrentPosition - parsedPositions[i]) < accuracy;
                    var isMoving = positioner.CurrentSpeed > 0;

                    if (!isAtTargetPosition || isMoving)
                    {
                        deviceNameList.Add(parsedDeviceNames[i]);
                        positionsList.Add(parsedPositions[i]);
                        if (parsedWaitUntilPos is not null && parsedWaitUntilPos.Length == parsedPositions.Length)
                            waitUntilPosList.Add(parsedWaitUntilPos[i]);
                    }
                }
                else
                {
                    throw new Exception($"Unable to retrieve positioner device {parsedDeviceNames[i]}.");
                }
            }

            if (deviceNameList.Count == 0)
                return;

            var deviceNames = deviceNameList.ToArray();
            var positions = positionsList.ToArray();

            // Map devices and controllers
            var devices = deviceNames
                .Select(name => (success: _controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner), name, positioner))
                .Where(t => t.success)
                .ToDictionary(t => t.name, t => t.positioner);
            // Retrieve controllers and group devices by controller
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

            var positionerMovementInfos = new Dictionary<char, PositionerMovementInformation>();
            float allocatedTime = 0f;
            float allocatedTimeWithoutDecel = 0f;


            // Set-up movement parameters for each device.
            /// Jump- movement to a target position, without a clear starting point.
            /// Max acceleration values are used
            /// Target speed is described by the Default Speed
            /// 

            foreach (var name in deviceNames)
            {
                if (_controllerManager.TryGetDevice<BasePositionerDevice>(name, out var positioner))
                {
                    devices[name] = positioner;

                    var targetPosition = positions[Array.IndexOf(deviceNames, name)];
                    var targetDistance = Math.Abs(targetPosition - positioner.CurrentPosition);
                    var targetDirection = targetPosition > positioner.CurrentPosition;

                    var movementInfo = new PositionerMovementInformation
                    {
                        StartingMovementParameters = new StartingMovementParameters
                        {
                            Position = positioner.CurrentPosition,
                            TargetSpeed = positioner.Speed,
                            Speed = positioner.CurrentSpeed,
                            Acceleration = positioner.Acceleration,
                            Deceleration = positioner.Deceleration,
                            Direction = positioner.CurrentSpeed >= 0
                        },

                        TargetMovementParameters = new TargetMovementParameters
                        {
                            Position = targetPosition,
                            TargetSpeed = positioner.DefaultSpeed,
                            Speed = 0f,
                            Acceleration = positioner.MaxAcceleration,
                            Deceleration = positioner.MaxDeceleration,
                            Direction = targetDirection,
                            Distance = targetDistance,
                            Rethrow = 0f
                        },
                    };

                    positionerMovementInfos[name] = movementInfo;

                    // Try to calculate total time for the movement.
                    // Trivial, no Jerk.
                    CustomFunctionHelper.CalculateKinParametersForMovementInfo(ref movementInfo);

                    //positionerMovementInfos[name].

                    allocatedTime = (float)Math.Max(allocatedTime, movementInfo.KinematicParameters.TotalTime);
                    allocatedTimeWithoutDecel = (float)Math.Max(allocatedTimeWithoutDecel, movementInfo.KinematicParameters.TotalTime - movementInfo.KinematicParameters.ConstantSpeedEndTime/ 2);
                }
                else
                {
                    throw new Exception($"Unable to retrieve positioner device {name}.");
                }
            }

            // Check if kinematic parameters need to be updated

            bool kinematicParametersNeedUpdate =
                positionerMovementInfos.Values.Any(info =>
                    info.TargetMovementParameters.Acceleration != info.StartingMovementParameters.Acceleration ||
                    info.TargetMovementParameters.Deceleration != info.TargetMovementParameters.Deceleration ||
                    info.TargetMovementParameters.TargetSpeed != info.StartingMovementParameters.TargetSpeed)
                || deviceNames.Any(name => devices[name].UpdatePending
                || !blending);

            if (kinematicParametersNeedUpdate)
            {
                List<Command> updateParametersCommandLine = CreateUpdateCommands(positionerMovementInfos, groupedDevicesByController, blending);

                _commandManager.EnqueueCommandLine(updateParametersCommandLine.ToArray());
                _commandManager.TryExecuteCommandLine(updateParametersCommandLine.ToArray()).GetAwaiter().GetResult();
                foreach (var (name, device) in devices)
                {
                    device.UpdatePending = false;
                }
            }


            // we need to handle the wait until logic here if needed
            Dictionary<char, float>? waitUntilPosDict = null;
            if (waitUntilTime is float waitUntilTimeFloat)
            {
                /// movement will continue until time {waitUntilTime} is reached.
                /// After the time is reached another command will commence.
                /// 
                // we should calculate the end position here.
                var waitPositions_calc = CalculateWaitUntilPosition(positionerMovementInfos, waitUntilTimeFloat);
                waitUntilPosDict = waitPositions_calc;
            }
            if (waitUntilPosList.Count == positionsList.Count)
            {
                /// movements will continue until 
                /// the first device reaches its corresponding 
                /// wait until Position value
                /// 
                // we must calculate the time needed to reach the condition
                var timeToWaitUntilPosition = new Dictionary<char, float>();
                waitUntilPosDict = new Dictionary<char, float>();
                for (int i = 0; i < deviceNames.Length; i++)
                {
                    var name = deviceNames[i];
                    var info = positionerMovementInfos[name];
                    var waitUntilPosition = waitUntilPosList[i];
                    waitUntilPosDict[name] = waitUntilPosition;
                    // Calculate time to reach waitUntilPosition
                    float timeToPosition = CalculateTimeToReachPosition(info, waitUntilPosition);

                    if (float.IsNormal(timeToPosition))
                        timeToWaitUntilPosition[name] = timeToPosition;
                }

                //TODO : check if we want min or max.
                // Find the minimal time among all devices
                float minTime = timeToWaitUntilPosition.Values.Min();
                waitUntilTime = minTime;
            }

            // Create the movement commands.
            List<Command> commandsMovement = CreateMovementCommands(isShutterUsed, groupedDevicesByController, positionerMovementInfos, allocatedTime, waitUntilTime, waitUntilPosDict);

            _commandManager.EnqueueCommandLine(commandsMovement.ToArray());
            _commandManager.TryExecuteCommandLine(commandsMovement.ToArray()).GetAwaiter().GetResult();
        }


        //private float CalculateTotalTimeForMovementInfo(PositionerMovementInformation info, out float timeToAccel, out float timeToDecel, out float totalTime)
        //{
        //    timeToAccel = 0f;
        //    timeToDecel = 0f;
        //    totalTime = 0f;

        //    float x0 = info.StartingPosition;
        //    float v0 = info.StartingSpeed;
        //    float vt = info.TargetSpeed;
        //    float a = info.TargetAcceleration;
        //    float d = info.TargetDeceleration;
        //    float x_target = info.TargetPosition;

        //    // Calculate total movement direction
        //    float deltaX_total = x_target - x0;
        //    float direction = Math.Sign(deltaX_total); // +1 for positive, -1 for negative

        //    // Adjust initial speed to movement direction
        //    float v0_dir = v0 * direction;

        //    // Keep accelerations and speeds positive
        //    a = Math.Abs(a);
        //    d = Math.Abs(d);
        //    vt = Math.Abs(vt);


        //    // If initial speed is in the opposite direction, decelerate to zero first
        //    if (v0_dir < 0)
        //    {
        //        // Time to decelerate to zero speed
        //        float t_stop = -v0_dir / d;
        //        // Distance covered during deceleration
        //        float s_stop = v0_dir * t_stop + 0.5f * (-d) * t_stop * t_stop;
        //        s_stop = Math.Abs(s_stop);

        //        totalTime += t_stop;
        //        deltaX_total -= s_stop * direction; // Remaining distance after stopping

        //        v0_dir = 0; // Reset initial speed after stopping
        //    }

        //    float deltaX_remaining = Math.Abs(deltaX_total);

        //    // Compute candidate maximum speed
        //    float numerator = 2 * a * d * deltaX_remaining + d * v0_dir * v0_dir;
        //    float denominator = a + d;
        //    float vMaxSquaredCandidate = numerator / denominator;
        //    float vMaxCandidate = (float)Math.Sqrt(vMaxSquaredCandidate);

        //    // Limit maximum speed to the target speed
        //    float vMax = Math.Min(vMaxCandidate, vt);

        //    // Calculate distances for acceleration and deceleration phases
        //    float s1 = (vMax * vMax - v0_dir * v0_dir) / (2 * a);
        //    float s3 = (vMax * vMax) / (2 * d);
        //    float s_total_required = s1 + s3;

        //    if (s_total_required > deltaX_remaining)
        //    {
        //        // Triangular profile
        //        vMaxSquaredCandidate = (2 * a * d * deltaX_remaining + d * v0_dir * v0_dir) / (a + d);
        //        vMax = (float)Math.Sqrt(vMaxSquaredCandidate);

        //        // Recalculate times
        //        float t1 = (vMax - v0_dir) / a;
        //        float t3 = vMax / d;
        //        totalTime += t1 + t3;

        //        timeToAccel = t1;
        //        timeToDecel = t3;
        //    }
        //    else
        //    {
        //        // Trapezoidal profile
        //        float s2 = deltaX_remaining - s1 - s3;

        //        // Calculate times for each phase
        //        float t1 = (vMax - v0_dir) / a;
        //        float t2 = s2 / vMax;
        //        float t3 = vMax / d;

        //        totalTime += t1 + t2 + t3;
        //        timeToAccel = t1;
        //        timeToDecel = t3;
        //    }

        //    return totalTime;
        //}

        private float CalculateTimeToReachPosition(PositionerMovementInformation info, float waitUntilPosition)
        {
            // Extract movement parameters
            float x0 = info.StartingMovementParameters.Position;
            float v0 = info.StartingMovementParameters.Speed;
            float vt = info.TargetMovementParameters.TargetSpeed;
            float a = info.TargetMovementParameters.Acceleration;
            float d = info.TargetMovementParameters.Deceleration;
            float x_target = waitUntilPosition;

            // Calculate total movement direction
            float deltaX_total = info.TargetMovementParameters.Position - x0;
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
                // Distance covered during deceleration
                float s_stop = v0_dir * t_stop + 0.5f * (-d) * t_stop * t_stop;
                s_stop = Math.Abs(s_stop);

                totalTime += t_stop;
                x0 += direction * s_stop; // Update starting position after stopping
                v0_dir = 0; // Reset initial speed after stopping
            }

            // Adjust target position after initial deceleration
            deltaX_total = info.TargetMovementParameters.Position - x0;
            float deltaX = x_target - x0;
            float deltaX_dir = direction * deltaX;

            // Calculate times and distances for acceleration and deceleration
            float tAcc = (vt - v0_dir) / a;
            float dAcc = v0_dir * tAcc + 0.5f * a * tAcc * tAcc;

            float tDec = vt / d;
            float dDec = vt * tDec - 0.5f * d * tDec * tDec;

            float totalDistanceAccDec = dAcc + dDec;

            if (totalDistanceAccDec <= Math.Abs(deltaX_total))
            {
                // Trapezoidal profile
                float dConst = Math.Abs(deltaX_total) - totalDistanceAccDec;
                float tConst = dConst / vt;
                //totalTime += tAcc + tConst + tDec;

                // Positions at the end of each phase
                float xEndAcc = x0 + direction * dAcc;
                float xEndConst = xEndAcc + direction * dConst;
                float xEndDec = xEndConst + direction * dDec;

                if (direction * (x_target - x0) <= direction * (xEndAcc - x0))
                {
                    // Reached during acceleration
                    float A = 0.5f * a;
                    float B = v0_dir;
                    float C = -direction * (x_target - x0);

                    float discriminant = B * B - 4 * A * C;
                    if (discriminant < 0)
                        throw new Exception("No real solution in acceleration phase");

                    float sqrtDiscriminant = (float)Math.Sqrt(discriminant);
                    float t1 = (-B + sqrtDiscriminant) / (2 * A);
                    float t2 = (-B - sqrtDiscriminant) / (2 * A);

                    float t = t1 >= 0 ? t1 : t2;
                    return totalTime + t;
                }
                else if (direction * (x_target - xEndAcc) <= direction * (xEndConst - xEndAcc))
                {
                    // Reached during constant speed
                    float t = tAcc + (direction * (x_target - xEndAcc)) / vt;
                    return totalTime + t;
                }
                else if (direction * (x_target - xEndConst) <= direction * (xEndDec - xEndConst))
                {
                    // Reached during deceleration
                    float deltaX_phase = direction * (x_target - xEndConst);

                    float A = -0.5f * d;
                    float B = vt;
                    float C = -deltaX_phase;

                    float discriminant = B * B - 4 * A * C;
                    if (discriminant < 0)
                        throw new Exception("No real solution in deceleration phase");

                    float sqrtDiscriminant = (float)Math.Sqrt(discriminant);
                    float t_dec1 = (-B + sqrtDiscriminant) / (2 * A);
                    float t_dec2 = (-B - sqrtDiscriminant) / (2 * A);

                    float t_dec = t_dec1 >= 0 ? t_dec1 : t_dec2;
                    float t = tAcc + tConst + t_dec;
                    return totalTime + t;
                }
                else
                {
                    // Target position beyond the movement
                    return totalTime;
                }
            }
            else
            {
                // Triangular profile
                // Calculate maximum achievable speed (vMax)
                float numerator = 2 * a * d * Math.Abs(deltaX_total) + d * v0_dir * v0_dir;
                float denominator = a + d;
                float vMaxSquared = numerator / denominator;
                float vMax = (float)Math.Sqrt(vMaxSquared);

                // Recalculate times and distances
                float tAccNew = (vMax - v0_dir) / a;
                float dAccNew = v0_dir * tAccNew + 0.5f * a * tAccNew * tAccNew;

                float tDecNew = vMax / d;
                float dDecNew = vMax * tDecNew - 0.5f * d * tDecNew * tDecNew;

                totalTime += tAccNew + tDecNew;

                // Position at end of acceleration
                float xEndAcc = x0 + direction * dAccNew;

                if (direction * (x_target - x0) <= direction * (xEndAcc - x0))
                {
                    // Reached during acceleration
                    float A = 0.5f * a;
                    float B = v0_dir;
                    float C = -direction * (x_target - x0);

                    float discriminant = B * B - 4 * A * C;
                    if (discriminant < 0)
                        throw new Exception("No real solution in acceleration phase");

                    float sqrtDiscriminant = (float)Math.Sqrt(discriminant);
                    float t1 = (-B + sqrtDiscriminant) / (2 * A);
                    float t2 = (-B - sqrtDiscriminant) / (2 * A);

                    float t = t1 >= 0 ? t1 : t2;
                    return totalTime + t;
                }
                else
                {
                    // Reached during deceleration
                    float deltaX_phase = direction * (x_target - xEndAcc);

                    float A = -0.5f * d;
                    float B = vMax;
                    float C = -deltaX_phase;

                    float discriminant = B * B - 4 * A * C;
                    if (discriminant < 0)
                        throw new Exception("No real solution in deceleration phase");

                    float sqrtDiscriminant = (float)Math.Sqrt(discriminant);
                    float t_dec1 = (-B + sqrtDiscriminant) / (2 * A);
                    float t_dec2 = (-B - sqrtDiscriminant) / (2 * A);

                    float t_dec = t_dec1 >= 0 ? t_dec1 : t_dec2;
                    float t = tAccNew + t_dec;
                    return totalTime + t;
                }
            }
        }

        private Dictionary<char, float> CalculateWaitUntilPosition(Dictionary<char, PositionerMovementInformation> positionerMovementInfos, float waitUntilTime)
        {
            var positionsAtWaitTime = new Dictionary<char, float>();

            foreach (var kvp in positionerMovementInfos)
            {
                var name = kvp.Key;
                var info = kvp.Value;
                float t = waitUntilTime;

                // Extract movement parameters
                float x0 = info.StartingMovementParameters.Position;
                float v0 = info.StartingMovementParameters.Speed;
                float vt = info.TargetMovementParameters.TargetSpeed;
                float a = info.TargetMovementParameters.Acceleration;
                float d = info.TargetMovementParameters.Deceleration;
                float x_target = info.TargetMovementParameters.Position;

                // Calculate total movement direction
                float deltaX_total = x_target - x0;
                int direction = Math.Sign(deltaX_total); // +1 for positive, -1 for negative
                float v0_dir = v0 * direction;

                float position = x0;

                (float t1, float t2, float t3, float s1, float s2, float s3, float vMax) = CustomFunctionHelper.CalculateMainParameters(x0, v0, vt, a, d, x_target);


                // Now calculate position at time t
                if (t <= t1)
                {
                    // Acceleration phase
                    position = x0 + direction * (v0_dir * t + 0.5f * a * t * t);
                }
                else if (t <= t1 + t2)
                {
                    // Constant speed phase
                    float tConst = t - t1;
                    position = x0 + direction * (s1 + vMax * tConst);
                }
                else if (t <= t1 + t2 + t3)
                {
                    // Deceleration phase
                    float tDec = t - t1 - t2;
                    position = x0 + direction * (s1 + s2 + vMax * tDec - 0.5f * d * tDec * tDec);
                }
                else
                {
                    // Movement completed
                    position = x_target;
                }

                positionsAtWaitTime[name] = position;
            }

            return positionsAtWaitTime;
        }


        private List<Command> CreateMovementCommands(bool isShutterUsed, Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController, Dictionary<char, PositionerMovementInformation> positionerMovementInfos, float allocatedTime, float? waitUntilTime, Dictionary<char, float>? waitUntilPosDict)
        {
            var commandsMovement = new List<Command>();

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var controllerName = controllerGroup.Key.Name;
                var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();

                var positionerInfos = groupedDeviceNames.ToDictionary(
                    deviceName => deviceName,
                    deviceName => new PositionerInfo
                    {
                        WaitUntilPosition = waitUntilPosDict is not null ? waitUntilPosDict[deviceName] : null,
                        TargetSpeed = positionerMovementInfos[deviceName].TargetMovementParameters.TargetSpeed,
                        Direction = positionerMovementInfos[deviceName].TargetMovementParameters.Direction,
                        TargetPosition = positionerMovementInfos[deviceName].TargetMovementParameters.Position,
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
                    });

                var shutterDevice = _controllerManager.GetDevices<ShutterDevice>().First();

                var moveAParameters = new MoveAbsoluteParameters
                {
                    WaitUntilTime = waitUntilTime,
                    IsShutterUsed = isShutterUsed,
                    IsLeadOutUsed = false,
                    IsLeadInUsed = false,
                    AllocatedTime = allocatedTime,
                    PositionerInfo = positionerInfos,
                    ShutterInfo = isShutterUsed ? new ShutterInfo
                    {
                        DelayOn = shutterDevice is not null ? Math.Max(shutterDevice.DelayOn, 0) : float.NaN,
                        DelayOff = shutterDevice is not null ? Math.Max(shutterDevice.DelayOff, 0) : float.NaN,
                    } : new ShutterInfo()
                };

                commandsMovement.Add(new Command
                {
                    Action = CommandDefinitions.MoveAbsolute,
                    Await = true,
                    Parameters = moveAParameters,
                    TargetController = controllerName,
                    TargetDevices = groupedDeviceNames,
                    EstimatedTime = waitUntilTime ?? positionerInfos.Select(info => info.Value.MovementInformation.TotalTime).Max()
                });
            }

            return commandsMovement;
        }

        private List<Command> CreateUpdateCommands(Dictionary<char, PositionerMovementInformation> positionerMovementInfos, Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController, bool blending)
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
                        if (positionerMovementInfos[deviceName].StartingMovementParameters.Acceleration!= positionerMovementInfos[deviceName].TargetMovementParameters.Acceleration 
                        || positionerMovementInfos[deviceName].StartingMovementParameters.Deceleration != positionerMovementInfos[deviceName].TargetMovementParameters.Deceleration)
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
                    Blending = blending
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

        private bool TryParseArguments(object?[] arguments, out char[] devNames, out float[] positions, out float[] waitUntil)
        {
            devNames = Array.Empty<char>();
            positions = Array.Empty<float>();
            waitUntil = Array.Empty<float>();

            if (arguments == null || arguments.Length == 0)
                return false;

            if (arguments[0] is not string firstArg)
                return false;

            devNames = firstArg.ToCharArray();
            int expectedPositionsCount = devNames.Length;

            if (arguments.Length < 1 + expectedPositionsCount)
                return false;

            positions = new float[expectedPositionsCount];
            for (int i = 0; i < expectedPositionsCount; i++)
            {
                if (!TryConvertToFloat(arguments[i + 1], out positions[i]))
                    return false;
            }

            int waitUntilCount = arguments.Length - (1 + expectedPositionsCount);
            waitUntil = new float[waitUntilCount];
            for (int i = 0; i < waitUntilCount; i++)
            {
                if (!TryConvertToFloat(arguments[i + 1 + expectedPositionsCount], out waitUntil[i]))
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
