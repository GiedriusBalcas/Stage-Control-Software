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

        public JumpAbsoluteFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            SetProperty("Shutter", false);
            SetProperty("Accuracy", 0.1f);
            SetProperty("LeadOut", false);
            SetProperty("WaitUntilTime", null, true);
        }


        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out var parsedDeviceNames, out var parsedPositions, out var parsedWaitUntil))
                throw new ArgumentException("Argument parsing was unsuccessful. Wrong types.");

            if (!TryGetProperty("Shutter", out var isShutterUsedObj))
                throw new Exception("Failed to get 'Shutter' property.");
            var isShutterUsed = (bool)isShutterUsedObj;

            if (!TryGetProperty("Accuracy", out var accuracyObj))
                throw new Exception("Failed to get 'Accuracy' property.");
            var accuracy = (float)accuracyObj;

            if (!TryGetProperty("WaitUntilTime", out var waitUntilTimeObj))
                throw new Exception("Failed to get 'WaitUntilTime' property.");
            float? waitUntilTime = waitUntilTimeObj is null? null : (float)waitUntilTimeObj;


            ExecutionCore(parsedDeviceNames, parsedPositions, isShutterUsed, accuracy, waitUntilTime, parsedWaitUntil);

            return null;
        }

        public void ExecutionCore(char[] parsedDeviceNames, float[] parsedPositions, bool isShutterUsed, float accuracy, float? waitUntilTime, float[]? parsedWaitUntilPos)
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

                    if (!isAtTargetPosition || !isMoving)
                    {
                        deviceNameList.Add(parsedDeviceNames[i]);
                        positionsList.Add(parsedPositions[i]);
                        if(parsedWaitUntilPos is not null && parsedWaitUntilPos.Length == parsedPositions.Length)
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
                        TargetPosition = targetPosition,
                        TargetDistance = targetDistance,
                        TargetDirection = targetDirection,
                    };

                    positionerMovementInfos[name] = movementInfo;

                    // Try to calculate total time for the movement.
                    // Trivial, no Jerk.

                    allocatedTime = (float)Math.Max(allocatedTime, CustomFunctionHelper.CalculateTotalTime(
                        movementInfo.TargetDistance,
                        movementInfo.TargetSpeed,
                        movementInfo.TargetAcceleration,
                        movementInfo.TargetDeceleration,
                        movementInfo.StartingSpeed));
                }
                else
                {
                    throw new Exception($"Unable to retrieve positioner device {name}.");
                }
            }

            // Check if kinematic parameters need to be updated
            bool kinematicParametersNeedUpdate = positionerMovementInfos.Values.Any(info =>
                info.TargetAcceleration != info.StartingAcceleration ||
                info.TargetDeceleration != info.StartingDeceleration ||
                info.TargetSpeed != info.CurrentTargetSpeed);

            if (kinematicParametersNeedUpdate)
            {
                List<Command> updateParametersCommandLine = CreateUpdateCommands(positionerMovementInfos, groupedDevicesByController);

                _commandManager.EnqueueCommandLine(updateParametersCommandLine.ToArray());
                _commandManager.ExecuteCommandLine(updateParametersCommandLine.ToArray()).GetAwaiter().GetResult();
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
            if(waitUntilPosList.Count == positionsList.Count)
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

                    timeToWaitUntilPosition[name] = timeToPosition;
                }

                // Find the minimal time among all devices
                float minTime = timeToWaitUntilPosition.Values.Min();
                waitUntilTime = minTime;
            }

            // Create the movement commands.
            List<Command> commandsMovement = CreateMovementCommands(isShutterUsed, groupedDevicesByController, positionerMovementInfos, allocatedTime, waitUntilTime, waitUntilPosDict);

            _commandManager.EnqueueCommandLine(commandsMovement.ToArray());
            _commandManager.ExecuteCommandLine(commandsMovement.ToArray()).GetAwaiter().GetResult();
        }

        private float CalculateTimeToReachPosition(PositionerMovementInformation info, float targetPosition)
        {
            // Extract movement parameters
            float x0 = info.StartingPosition;
            float v0 = info.StartingSpeed;
            float vt = info.TargetSpeed;
            float a = info.TargetAcceleration;
            float d = info.TargetDeceleration;
            float x_target = targetPosition;

            float deltaX = x_target - x0;
            float direction = Math.Sign(deltaX); // +1 for positive, -1 for negative

            // Adjust parameters based on movement direction
            a *= direction;
            d *= direction;
            v0 *= direction;
            vt *= direction;

            deltaX = Math.Abs(deltaX);

            // Calculate times and distances for acceleration and deceleration
            float tAcc = (vt - v0) / a;
            float dAcc = (vt * vt - v0 * v0) / (2 * a);

            float tDec = vt / d;
            float dDec = (vt * vt) / (2 * d);

            if (dAcc + dDec <= deltaX)
            {
                // Trapezoidal profile
                float dConst = deltaX - (dAcc + dDec);
                float tConst = dConst / vt;
                float totalTime = tAcc + tConst + tDec;

                // Positions at the end of each phase
                float xEndAcc = x0 + direction * dAcc;
                float xEndConst = xEndAcc + direction * dConst;
                float xEndDec = xEndConst + direction * dDec;

                if (direction * (x_target - x0) <= direction * (xEndAcc - x0))
                {
                    // Reached during acceleration
                    float deltaX_phase = x_target - x0;
                    float t = (-v0 + (float)Math.Sqrt(v0 * v0 + 2 * a * deltaX_phase)) / a;
                    return t;
                }
                else if (direction * (x_target - xEndAcc) <= direction * (xEndConst - xEndAcc))
                {
                    // Reached during constant speed
                    float deltaX_phase = x_target - xEndAcc;
                    float t = tAcc + deltaX_phase / vt;
                    return t;
                }
                else if (direction * (x_target - xEndConst) <= direction * (xEndDec - xEndConst))
                {
                    // Reached during deceleration
                    float deltaX_phase = x_target - xEndConst;
                    float A = 0.5f * d;
                    float B = -vt;
                    float C = deltaX_phase;
                    float discriminant = B * B - 4 * A * C;
                    if (discriminant < 0)
                        throw new Exception("No real solution for deceleration phase");

                    float t_dec = (-B - (float)Math.Sqrt(discriminant)) / (2 * A);
                    float t = tAcc + tConst + t_dec;
                    return t;
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
                float numerator = 2 * a * d * deltaX + d * v0 * v0;
                float denominator = a + d;
                float vMax = (float)Math.Sqrt(numerator / denominator);

                float tAccNew = (vMax - v0) / a;
                float tDecNew = vMax / d;
                float totalTime = tAccNew + tDecNew;

                // Position at end of acceleration
                float xEndAcc = x0 + direction * (v0 * tAccNew + 0.5f * a * tAccNew * tAccNew);

                if (direction * (x_target - x0) <= direction * (xEndAcc - x0))
                {
                    // Reached during acceleration
                    float deltaX_phase = x_target - x0;
                    float t = (-v0 + (float)Math.Sqrt(v0 * v0 + 2 * a * deltaX_phase)) / a;
                    return t;
                }
                else
                {
                    // Reached during deceleration
                    float deltaX_phase = x_target - xEndAcc;
                    float A = 0.5f * d;
                    float B = -vMax;
                    float C = deltaX_phase;
                    float discriminant = B * B - 4 * A * C;
                    if (discriminant < 0)
                        throw new Exception("No real solution for deceleration phase");

                    float t_dec = (-B - (float)Math.Sqrt(discriminant)) / (2 * A);
                    float t = tAccNew + t_dec;
                    return t;
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

                // Extract movement parameters
                float v0 = info.StartingSpeed;
                float vt = info.TargetSpeed;
                float a = info.TargetAcceleration;
                float d = info.TargetDeceleration;
                float S_total = info.TargetDistance;
                float x0 = info.StartingPosition;

                // Calculate times and distances for acceleration and deceleration
                float tAcc = (vt - v0) / a;
                float dAcc = (vt * vt - v0 * v0) / (2 * a);

                float tDec = vt / d;
                float dDec = (vt * vt) / (2 * d);

                if (dAcc + dDec <= S_total)
                {
                    // Trapezoidal (with constant speed phase)
                    float dConst = S_total - (dAcc + dDec);
                    float tConst = dConst / vt;
                    float totalTime = tAcc + tConst + tDec;

                    float t = waitUntilTime;

                    if (t <= tAcc)
                    {
                        // Acceleration phase
                        float position = x0 + v0 * t + 0.5f * a * t * t;
                        positionsAtWaitTime[name] = position;
                    }
                    else if (t <= tAcc + tConst)
                    {
                        // Constant speed phase
                        float position = x0 + dAcc + vt * (t - tAcc);
                        positionsAtWaitTime[name] = position;
                    }
                    else if (t <= totalTime)
                    {
                        // Deceleration phase
                        float tDecPhase = t - tAcc - tConst;
                        float position = x0 + dAcc + dConst + vt * tDecPhase - 0.5f * d * tDecPhase * tDecPhase;
                        positionsAtWaitTime[name] = position;
                    }
                    else
                    {
                        // Movement completed
                        positionsAtWaitTime[name] = x0 + S_total;
                    }
                }
                else
                {
                    // Triangular profile (no constant speed phase)
                    // Calculate maximum achievable speed (vMax)
                    float numerator = 2 * a * d * S_total + d * v0 * v0;
                    float denominator = a + d;
                    float vMax = (float)Math.Sqrt(numerator / denominator);

                    float tAccNew = (vMax - v0) / a;
                    float tDecNew = vMax / d;
                    float totalTime = tAccNew + tDecNew;

                    float t = waitUntilTime;

                    if (t <= tAccNew)
                    {
                        // Acceleration phase
                        float position = x0 + v0 * t + 0.5f * a * t * t;
                        positionsAtWaitTime[name] = position;
                    }
                    else if (t <= totalTime)
                    {
                        // Deceleration phase
                        float tDecPhase = t - tAccNew;
                        float position = x0 + (v0 * tAccNew + 0.5f * a * tAccNew * tAccNew) + vMax * tDecPhase - 0.5f * d * tDecPhase * tDecPhase;
                        positionsAtWaitTime[name] = position;
                    }
                    else
                    {
                        // Movement completed
                        positionsAtWaitTime[name] = x0 + S_total;
                    }
                }
            }

            return positionsAtWaitTime;
        }

        private List<Command> CreateMovementCommands(bool isShutterUsed, Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController, Dictionary<char, PositionerMovementInformation> positionerMovementInfos, float allocatedTime, float? waitUntilTime, Dictionary<char,float>? waitUntilPosDict)
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
                        WaitUntil = waitUntilPosDict is not null? waitUntilPosDict[deviceName] : null, // TODO: Implement waitUntil logic if necessary
                        TargetSpeed = positionerMovementInfos[deviceName].TargetSpeed,
                        Direction = positionerMovementInfos[deviceName].TargetDirection,
                        TargetPosition = positionerMovementInfos[deviceName].TargetPosition,
                    });

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
                        DelayOn = 0f,
                        DelayOff = 0f,
                    } : null
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
