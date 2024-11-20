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
using static standa_controller_software.custom_functions.definitions.LineAbsoluteFunction;
using Antlr4.Runtime.Misc;

namespace standa_controller_software.custom_functions.definitions
{
    public class ArcAbsoluteFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private readonly CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;
        private readonly JumpAbsoluteFunction _jumpFunction;
        private readonly ChangeShutterStateFunction changeShutterStateFunction;

        public ArcAbsoluteFunction(CommandManager commandManager, ControllerManager controllerManager, JumpAbsoluteFunction jumpAbsoluteFunction, ChangeShutterStateFunction changeShutterStateFunction)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            _jumpFunction = jumpAbsoluteFunction;
            this.changeShutterStateFunction = changeShutterStateFunction;
            SetProperty("Shutter", false);
            SetProperty("Accuracy", 1f);
            SetProperty("TimeAccuracy", 0.03f);
            SetProperty("LeadIn", false);
            SetProperty("Speed", 100f);
            SetProperty("WaitUntilCondition", null, true);
            SetProperty("LeadOut", false);
        }

        public override object? Execute(params object[] args)
        {
            // arcA("xy", radius, centerX, centerY, startAngle, endAngle, CCW);

            if (!TryParseArguments(args, out var parsedDeviceNames, out var parsedRadius, out var parsedCenterPositions, out var parsedStartAngle, out var parsedEndAngle, out var parsedIsCCW))
                throw new ArgumentException("Argument parsing was unsuccessful. Wrong types.");

            if (!TryGetProperty("Shutter", out var isShutterUsedObj))
                throw new Exception("Failed to get 'Shutter' property.");
            var isShutterUsed = (bool)isShutterUsedObj;

            if (!TryGetProperty("Accuracy", out var accuracyObj))
                throw new Exception("Failed to get 'Accuracy' property.");
            var accuracy = (float)accuracyObj;

            if (!TryGetProperty("TimeAccuracy", out var timeAccuracyObj))
                throw new Exception("Failed to get 'Accuracy' property.");
            var timeAccuracy = (float)timeAccuracyObj;

            if (!TryGetProperty("Speed", out var trajSpeedObj))
                throw new Exception("Failed to get 'Speed' property.");
            var trajectorySpeed = (float)trajSpeedObj;

            if (!TryGetProperty("LeadIn", out var leadInObj))
                throw new Exception("Failed to get 'LeadIn' property.");
            var leadIn = (bool)leadInObj;

            if (!TryGetProperty("LeadOut", out var leadOutObj))
                throw new Exception("Failed to get 'LeadOut' property.");
            var leadOut = (bool)leadOutObj;



            ExecutionCore(parsedDeviceNames[0], parsedDeviceNames[1], parsedRadius, parsedCenterPositions[0], parsedCenterPositions[0], parsedStartAngle, parsedEndAngle, parsedIsCCW, trajectorySpeed, timeAccuracy, accuracy, isShutterUsed, leadIn, leadOut);


            return null;
        }
        private static void CalculateTangentEndpoint(
            double centerX, double centerY,
            double x_B, double y_B,
            double const_distance,
            bool isCounterClockwise,
            out double x_tang, out double y_tang)
        {
            // Calculate the angle theta at point (x_B, y_B)
            double dx = x_B - centerX;
            double dy = y_B - centerY;
            double theta = Math.Atan2(dy, dx); // Angle in radians

            // Calculate unit tangent vector components based on direction
            double t_unit_x, t_unit_y;
            if (isCounterClockwise)
            {
                // CCW direction
                t_unit_x = -Math.Sin(theta);
                t_unit_y = Math.Cos(theta);
            }
            else
            {
                // CW direction
                t_unit_x = Math.Sin(theta);
                t_unit_y = -Math.Cos(theta);
            }

            // Scale the unit tangent vector by the desired length
            double t_scaled_x = t_unit_x * const_distance;
            double t_scaled_y = t_unit_y * const_distance;

            // Compute the endpoint of the tangent vector
            x_tang = x_B + t_scaled_x;
            y_tang = y_B + t_scaled_y;
        }


        private Command[] CreateUpdateCommands(Dictionary<char, PositionerMovementInformation> positionerMovementInfos, Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController)
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
                    Blending = false
                };

                updateParametersCommandLine.Add(new Command
                {
                    Action = CommandDefinitions.UpdateMoveSettings,
                    Await = true,
                    Parameters = commandParameters,
                    TargetController = controllerName,
                    TargetDevices = groupedDeviceNames,
                });
            }

            return updateParametersCommandLine.ToArray();
        }

        public void ExecutionCore(char xName, char yName, float radius, float centerX, float centerY, float startAngle, float endAngle, bool isCCW, float trajectorySpeed, float accuracy_time, float accuracy, bool shutter, bool leadIn, bool leadOut)
        {
            Message = string.Empty;

            if (accuracy < 0 || accuracy_time < 0)
                throw new Exception("Wrong accuracy values provided.");

            _controllerManager.TryGetDevice<BasePositionerDevice>(xName, out BasePositionerDevice xDevice);
            _controllerManager.TryGetDevice<BasePositionerDevice>(yName, out BasePositionerDevice yDevice);
            var devices_arc = new Dictionary<char, BasePositionerDevice>();
            devices_arc[xName] = xDevice;
            devices_arc[yName] = yDevice;

            // Retrieve controllers and group devices by controller
            var controllers_arc = new Dictionary<BasePositionerDevice, BasePositionerController>();

            if (_controllerManager.TryGetDeviceController<BasePositionerController>(xName, out var controller))
            {
                controllers_arc[xDevice] = controller;
            }
            if (_controllerManager.TryGetDeviceController<BasePositionerController>(yName, out controller))
            {
                controllers_arc[yDevice] = controller;
            }

            var groupedDevicesByController = devices_arc.Values
                .GroupBy(device => controllers_arc[device])
                .ToDictionary(group => group.Key, group => group.ToList());

            double angularVelocity = (trajectorySpeed / radius);

            float dtheta_accuracy = (float)(2 * Math.Acos(1 - accuracy / radius));
            float dtheta_accuracy_time = (float)(angularVelocity * accuracy_time);



            float dtheta = (float)(Math.Max(dtheta_accuracy, dtheta_accuracy_time));


            // lets round to fit segments nicely on our arc.
            int numberOfSegments = (int)Math.Floor(Math.Abs(endAngle - startAngle) / dtheta);

            if (float.IsNaN(numberOfSegments) || numberOfSegments < 2)
            {
                throw new Exception("Unable to create an arc trajectory, number of segments is less than two.");
            }

            dtheta = Math.Abs(endAngle - startAngle) / numberOfSegments;



            float accelerationForArc = trajectorySpeed * trajectorySpeed / radius * 1.1f;

            if (accelerationForArc > xDevice.MaxAcceleration || accelerationForArc > yDevice.MaxAcceleration)
                throw new Exception("Max Acceleration value of the device is issuficient for the arc movement.");


            float additionalDistanceToStop = accuracy_time * trajectorySpeed + 1 * trajectorySpeed * trajectorySpeed / (2 * accelerationForArc); // [um


            var arcLength = dtheta * radius;
            float rethrow = arcLength / trajectorySpeed;



            // --------------move to start------------------------------------.
            var startPosX = (float)(Math.Cos(startAngle) * radius) + centerX;
            var startPosY = (float)(Math.Sin(startAngle) * radius) + centerY;

            double startVelX = isCCW? Math.Abs(-trajectorySpeed * Math.Sin(startAngle)) : Math.Abs(trajectorySpeed * Math.Sin(startAngle));
            double startVelY = isCCW ? Math.Abs(trajectorySpeed * Math.Cos(startAngle)) : Math.Abs(-trajectorySpeed * Math.Cos(startAngle));

            bool needStartMovement = false;
            bool needPositionChange = Math.Abs(startPosX - xDevice.CurrentPosition) > accuracy || Math.Abs(startPosY - yDevice.CurrentPosition) > accuracy;
            bool needSpeedChange = Math.Abs(startVelX - xDevice.CurrentSpeed) > accuracy / accuracy_time || Math.Abs(startVelY - yDevice.CurrentSpeed) > accuracy / accuracy_time;
            bool needAccelChange = Math.Abs(accelerationForArc - xDevice.Acceleration) > 1
            || Math.Abs(accelerationForArc - xDevice.Deceleration) > 1
            || Math.Abs(accelerationForArc - yDevice.Acceleration) > 1
            || Math.Abs(accelerationForArc - yDevice.Deceleration) > 1;
            if (needPositionChange | needSpeedChange)
                needStartMovement = true;

            if (true)
            {
                float distanceToAccelerate = trajectorySpeed * trajectorySpeed / (2 * accelerationForArc); // [um
                float timeToAccelerate = trajectorySpeed / accelerationForArc;

                CalculateTangentEndpoint(
                    centerX, centerY,
                    (double)startPosX, (double)startPosY,
                    distanceToAccelerate,
                    !isCCW,
                    out double x_tang_start, out double y_tang_start);

                _jumpFunction.ExecutionCore([xDevice.Name, yDevice.Name], [(float)x_tang_start, (float)y_tang_start], false, accuracy, null, null, false);


                // now go to the start pos + additional distance


                var positionerMovementInfo = new Dictionary<char, PositionerMovementInformation>();
                var movementInfo = new PositionerMovementInformation
                {
                    TargetMovementParameters = new TargetMovementParameters
                    {
                        Acceleration = (float)Math.Max(accelerationForArc, 10),
                        Deceleration = (float)Math.Max(accelerationForArc, 10),
                        TargetSpeed = (float)Math.Max(trajectorySpeed, 1),
                    }
                };
                positionerMovementInfo[xName] = movementInfo;
                movementInfo = new PositionerMovementInformation
                {
                    TargetMovementParameters = new TargetMovementParameters
                    {
                        Acceleration = (float)Math.Max(accelerationForArc, 10),
                        Deceleration = (float)Math.Max(accelerationForArc, 10),
                        TargetSpeed = (float)Math.Max(trajectorySpeed, 1),
                    }
                };
                positionerMovementInfo[yName] = movementInfo;

                var commands_update = CreateUpdateCommands(positionerMovementInfo, groupedDevicesByController);
                _commandManager.EnqueueCommandLine(commands_update);
                _commandManager.TryExecuteCommandLine(commands_update).GetAwaiter().GetResult();

                CalculateTangentEndpoint(
                        centerX, centerY,
                        (double)startPosX, (double)startPosY,
                        additionalDistanceToStop,
                        isCCW,
                        out double x_tang_start_over, out double y_tang_start_over);

                var positionerMovementInformation_start = GetMovementInformation(xDevice, yDevice, (float)x_tang_start_over, (float)y_tang_start_over, trajectorySpeed, (float)startVelX, (float)startVelY, (float)0, (float)0, accelerationForArc, out float allocatedTime_start);



                float allocatedTime_guess_start = (additionalDistanceToStop) / trajectorySpeed + timeToAccelerate;

                var waitUntilPos_start = new Dictionary<char, float>();
                waitUntilPos_start[xDevice.Name] = startPosX;
                waitUntilPos_start[yDevice.Name] = startPosY;

                ShutterInfo shutterInfo = new ShutterInfo();
                var shutterDevice = _controllerManager.GetDevices<ShutterDevice>().First();

                shutterInfo.DelayOn = Math.Max( timeToAccelerate*1000f - shutterDevice.DelayOn, 0 );
                shutterInfo.DelayOff = float.NaN;

                // Create the movement commands.
                List<Command> commandsMovement_start = CreateMovementCommands(shutter, groupedDevicesByController, positionerMovementInformation_start, allocatedTime_guess_start, timeToAccelerate, waitUntilPos_start, shutterInfo); //rethrow_guess* multiplier     allocatedTime_guess * multiplier

                _commandManager.EnqueueCommandLine(commandsMovement_start.ToArray());
                _commandManager.TryExecuteCommandLine(commandsMovement_start.ToArray()).GetAwaiter().GetResult();
            }

            
            float Bx, By;

            // loop through the segments until last one.


            double prevVelX = isCCW ? Math.Abs(-trajectorySpeed * Math.Sin(startAngle)) : Math.Abs(trajectorySpeed * Math.Sin(startAngle));
            double prevVelY = isCCW ? Math.Abs(trajectorySpeed * Math.Cos(startAngle)) : Math.Abs(-trajectorySpeed * Math.Cos(startAngle));

            float allocatedTime_guess = (arcLength + additionalDistanceToStop) / trajectorySpeed;

            for (float theta = startAngle + (isCCW? dtheta: -dtheta); isCCW? theta < endAngle : theta > endAngle; theta += (isCCW ? dtheta : - dtheta))
            {
                Bx = (float)(Math.Cos(theta) * radius) + centerX;
                By = (float)(Math.Sin(theta) * radius) + centerY;

                // CALCULATE ACCEL/DECELL AT MID POINT

                float velX_avg = (Bx - xDevice.CurrentPosition) / rethrow;
                float velY_avg = (By - yDevice.CurrentPosition) / rethrow;


                // CALCULATE THE TANGENT ENDPOINT
                CalculateTangentEndpoint(
                    centerX, centerY,
                    (double)Bx, (double)By,
                    additionalDistanceToStop,
                    isCCW,
                    out double x_tang, out double y_tang);

                // CREATE UPDATE MOVEMENT SETTINGS COMMANDS
                var positionerMovementInformation = GetMovementInformation(xDevice, yDevice, (float)x_tang, (float)y_tang, trajectorySpeed, (float)velX_avg, (float)velY_avg, (float)prevVelX, (float)prevVelY, accelerationForArc, out float allocatedTime);



                var waitUntilPos = new Dictionary<char, float>();
                waitUntilPos[xDevice.Name] = Bx;
                waitUntilPos[yDevice.Name] = By;

                var shutterInfo_segments = new ShutterInfo()
                {
                    DelayOn = float.NaN,
                    DelayOff = float.NaN
                };

                // Create the movement commands.
                List<Command> commandsMovement = CreateMovementCommands(shutter, groupedDevicesByController, positionerMovementInformation, allocatedTime_guess, rethrow, waitUntilPos, shutterInfo_segments); //rethrow     waitUntilPos

                _commandManager.EnqueueCommandLine(commandsMovement.ToArray());
                _commandManager.TryExecuteCommandLine(commandsMovement.ToArray()).GetAwaiter().GetResult();


                prevVelX = velX_avg;
                prevVelY = velY_avg;
            }




            // move to end.
            bool endIsNeeded = true;
            if (endIsNeeded)
            {
                Bx = (float)(Math.Cos(endAngle) * radius) + centerX;
                By = (float)(Math.Sin(endAngle) * radius) + centerY;
                
                float velX_avg_end = (Bx - xDevice.CurrentPosition) / rethrow;
                float velY_avg_end = (By - yDevice.CurrentPosition) / rethrow;

                float additionalDistanceToStop_end = trajectorySpeed * trajectorySpeed / (2 * accelerationForArc); // [um


                // CALCULATE THE TANGENT ENDPOINT
                CalculateTangentEndpoint(
                    centerX, centerY,
                    (double)Bx, (double)By,
                    additionalDistanceToStop_end,
                    isCCW,
                    out double x_tang_end, out double y_tang_end);
                
                // CREATE UPDATE MOVEMENT SETTINGS COMMANDS
                var positionerMovementInformation_end = GetMovementInformation(xDevice, yDevice, (float)x_tang_end, (float)y_tang_end, trajectorySpeed, (float)velX_avg_end, (float)velY_avg_end, (float)prevVelX, (float)prevVelY, accelerationForArc, out float allocatedTime);

                var waitUntilPos_end = new Dictionary<char, float>();
                waitUntilPos_end[xDevice.Name] = Bx;
                waitUntilPos_end[yDevice.Name] = By;

                float allocatedTime_guess_end = (arcLength + additionalDistanceToStop_end) / trajectorySpeed;

                float timeToDecelerate = allocatedTime_guess_end - rethrow;

                ShutterInfo shutterInfo = new ShutterInfo();
                var shutterDevice = _controllerManager.GetDevices<ShutterDevice>().First();

                shutterInfo.DelayOn = float.NaN;
                shutterInfo.DelayOff = Math.Max(timeToDecelerate*1000f + shutterDevice.DelayOff, 0);

                // Create the movement commands.
                List<Command> commandsMovement_end = CreateMovementCommands(shutter, groupedDevicesByController, positionerMovementInformation_end, allocatedTime_guess, rethrow, waitUntilPos_end, shutterInfo); //

                _commandManager.EnqueueCommandLine(commandsMovement_end.ToArray());
                _commandManager.TryExecuteCommandLine(commandsMovement_end.ToArray()).GetAwaiter().GetResult();
            }

        }

        private List<Command> CreateMovementCommands(bool isShutterUsed, Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController, Dictionary<char, PositionerMovementInformation> positionerMovementInfos, float allocatedTime, float? waitUntilTime, Dictionary<char, float>? waitUntilPosDict, ShutterInfo shutterInfo)
        {
            var commandsMovement = new List<Command>();

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var controllerName = controllerGroup.Key.Name;
                var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).Where(name => positionerMovementInfos.ContainsKey(name)).ToArray();

                var positionerInfos = groupedDeviceNames.ToDictionary(
                    deviceName => deviceName,
                    deviceName => new PositionerInfo
                    {
                        WaitUntilPosition = waitUntilPosDict is null ? null : waitUntilPosDict[deviceName],
                        TargetSpeed = positionerMovementInfos[deviceName].TargetMovementParameters.TargetSpeed,
                        Direction = positionerMovementInfos[deviceName].TargetMovementParameters.Direction,
                        TargetPosition = positionerMovementInfos[deviceName].TargetMovementParameters.Position,
                    });

                var moveAParameters = new MoveAbsoluteParameters
                {
                    WaitUntilTime = waitUntilTime,
                    IsShutterUsed = isShutterUsed,
                    IsLeadOutUsed = false,
                    IsLeadInUsed = false,
                    AllocatedTime = allocatedTime,
                    PositionerInfo = positionerInfos,
                    ShutterInfo = isShutterUsed ? shutterInfo : new ShutterInfo()
                };

                commandsMovement.Add(new Command
                {
                    Action = CommandDefinitions.MoveAbsolute,
                    Await = true,
                    Parameters = moveAParameters,
                    TargetController = controllerName,
                    TargetDevices = groupedDeviceNames,
                    EstimatedTime = waitUntilTime?? positionerMovementInfos.Values.Select(info => info.KinematicParameters.TotalTime).Max()
                });
            }

            return commandsMovement;
        }
        private Dictionary<char, PositionerMovementInformation> GetMovementInformation(BasePositionerDevice deviceX, BasePositionerDevice deviceY, float targetPositionX, float targetPositionY, float trajectorySpeed, float speedX, float speedY, float prevVelX, float prevVelY, float acceleration, out float allocatedTime)
        {
            var positionerMovementInfos_jump = new Dictionary<char, PositionerMovementInformation>();

            var targetPosition = targetPositionX;
            var targetDistance = Math.Abs(targetPosition - deviceX.CurrentPosition);
            var targetDirection = targetPosition > deviceX.CurrentPosition;

            var movementInfo_x = new PositionerMovementInformation
            {
                PositionerParameters = new PositionerParameters
                {
                    MaxAcceleration = deviceX.MaxAcceleration,
                    MaxDeceleration = deviceX.MaxDeceleration,
                    MaxSpeed = deviceX.MaxSpeed,
                },
                StartingMovementParameters = new StartingMovementParameters
                {
                    Position = deviceX.CurrentPosition,
                    Speed = prevVelX,
                    TargetSpeed = deviceX.Speed,
                    Acceleration = deviceX.Acceleration,
                    Deceleration = deviceX.Deceleration,
                },
                TargetMovementParameters = new TargetMovementParameters
                {
                    Acceleration = acceleration,
                    Deceleration = acceleration,
                    TargetSpeed = Math.Max(speedX, 11),
                    Position = targetPosition,
                    Distance = targetDistance,
                    Direction = targetDirection,
                }
            };
            CustomFunctionHelper.CalculateKinParametersForMovementInfo(ref movementInfo_x);
            var calculatedTime_x = movementInfo_x.KinematicParameters.TotalTime;

            if (movementInfo_x.TargetMovementParameters.Distance > 0.01 && Math.Abs(movementInfo_x.TargetMovementParameters.TargetSpeed) > 0)
                positionerMovementInfos_jump[deviceX.Name] = movementInfo_x;



            targetPosition = targetPositionY;
            targetDistance = Math.Abs(targetPosition - deviceY.CurrentPosition);
            targetDirection = targetPosition > deviceY.CurrentPosition;


            var movementInfo_y = new PositionerMovementInformation
            {
                PositionerParameters = new PositionerParameters
                {
                    MaxAcceleration = deviceY.MaxAcceleration,
                    MaxDeceleration = deviceY.MaxDeceleration,
                    MaxSpeed = deviceY.MaxSpeed,
                },
                StartingMovementParameters = new StartingMovementParameters
                {
                    Position = deviceY.CurrentPosition,
                    Speed = prevVelY,
                    TargetSpeed = deviceY.Speed,
                    Acceleration = deviceY.Acceleration,
                    Deceleration = deviceY.Deceleration,
                },
                TargetMovementParameters = new TargetMovementParameters
                {
                    Acceleration = acceleration,
                    Deceleration = acceleration,
                    TargetSpeed = Math.Max(speedY, 11),
                    Position = targetPosition,
                    Distance = targetDistance,
                    Direction = targetDirection,
                }
            };
            CustomFunctionHelper.CalculateKinParametersForMovementInfo(ref movementInfo_y);
            var calculatedTime_y = movementInfo_y.KinematicParameters.TotalTime;

            if (movementInfo_y.TargetMovementParameters.Distance > 0.01 && Math.Abs(movementInfo_y.TargetMovementParameters.TargetSpeed) > 0)
                positionerMovementInfos_jump[deviceY.Name] = movementInfo_y;


            allocatedTime = Math.Max(movementInfo_x.KinematicParameters.TotalTime - (movementInfo_x.KinematicParameters.TotalTime - movementInfo_x.KinematicParameters.ConstantSpeedEndTime) / 2,
                movementInfo_y.KinematicParameters.TotalTime - (movementInfo_y.KinematicParameters.TotalTime - movementInfo_y.KinematicParameters.ConstantSpeedEndTime) / 2);

            return positionerMovementInfos_jump;
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
        //    int direction = Math.Sign(deltaX_total); // +1 for positive, -1 for negative

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
        //        float t_stop = Math.Abs(v0_dir) / d;
        //        // Distance covered during deceleration
        //        float s_stop = (v0_dir * v0_dir) / (2 * d);

        //        totalTime += t_stop;
        //        deltaX_total -= s_stop * direction; // Remaining distance after stopping

        //        v0_dir = 0f; // Reset initial speed after stopping
        //    }

        //    float deltaX_remaining = deltaX_total * direction; // Ensure positive

        //    // Determine whether we need to accelerate or decelerate to reach vt
        //    bool needsAcceleration = v0_dir < vt;

        //    // Calculate acceleration or deceleration distance and time to reach vt
        //    float s1 = 0f;
        //    float t1 = 0f;

        //    if (needsAcceleration)
        //    {
        //        // Acceleration phase
        //        s1 = (vt * vt - v0_dir * v0_dir) / (2 * a);
        //        t1 = (vt - v0_dir) / a;
        //    }
        //    else
        //    {
        //        // Deceleration phase
        //        s1 = (v0_dir * v0_dir - vt * vt) / (2 * d);
        //        t1 = (v0_dir - vt) / d;
        //    }

        //    // Deceleration to stop
        //    float s3 = (vt * vt) / (2 * d);
        //    float t3 = vt / d;

        //    // Total required distance
        //    float s_total_required = s1 + s3;

        //    if (s_total_required > deltaX_remaining)
        //    {
        //        // Need to adjust vt for triangular profile
        //        float numerator = 2 * a * d * deltaX_remaining + (needsAcceleration ? d : a) * v0_dir * v0_dir;
        //        float denominator = a + d;

        //        if (denominator == 0)
        //        {
        //            throw new Exception("Denominator in speed calculation is zero.");
        //        }

        //        float vMaxSquaredCandidate = numerator / denominator;
        //        vMaxSquaredCandidate = Math.Max(0, vMaxSquaredCandidate);
        //        float vMax = (float)Math.Sqrt(vMaxSquaredCandidate);

        //        if (needsAcceleration)
        //        {
        //            s1 = (vMax * vMax - v0_dir * v0_dir) / (2 * a);
        //            t1 = (vMax - v0_dir) / a;
        //        }
        //        else
        //        {
        //            s1 = (v0_dir * v0_dir - vMax * vMax) / (2 * d);
        //            t1 = (v0_dir - vMax) / d;
        //        }

        //        s3 = (vMax * vMax) / (2 * d);
        //        t3 = vMax / d;

        //        totalTime += t1 + t3;

        //        timeToAccel = t1;
        //        timeToDecel = t3;
        //    }
        //    else
        //    {
        //        // Trapezoidal profile
        //        float s2 = deltaX_remaining - s_total_required;
        //        float t2 = s2 / vt;

        //        totalTime += t1 + t2 + t3;

        //        timeToAccel = t1;
        //        timeToDecel = t3;
        //    }

        //    return totalTime;
        //}
        //private float CalculateTotalTimeForMovementInfo(PositionerMovementInformation info, out float timeToAccel, out float kaka, out float totalTime)
        //{
        //    kaka = 0f;
        //    timeToAccel = 0f;
        //    totalTime = 0f;

        //    float x0 = info.StartingPosition;
        //    float v0 = info.StartingSpeed;
        //    float vt = info.TargetSpeed;
        //    float a = info.TargetAcceleration;
        //    float x_target = info.TargetPosition;

        //    // Calculate total movement direction
        //    float deltaX_total = x_target - x0;
        //    int direction = Math.Sign(deltaX_total); // +1 for positive, -1 for negative

        //    // Adjust initial speed to movement direction
        //    float v0_dir = v0 * direction;

        //    // Keep acceleration and speeds positive
        //    a = Math.Abs(a);
        //    vt = Math.Abs(vt);

        //    // If initial speed is in the opposite direction, decelerate to zero first
        //    if (v0_dir < 0)
        //    {
        //        // Time to decelerate to zero speed
        //        float t_stop = Math.Abs(v0_dir) / a;
        //        // Distance covered during deceleration
        //        float s_stop = (v0_dir * v0_dir) / (2 * a);

        //        totalTime += t_stop;
        //        deltaX_total -= s_stop * direction; // Remaining distance after stopping

        //        v0_dir = 0f; // Reset initial speed after stopping
        //    }

        //    float deltaX_remaining = deltaX_total * direction; // Ensure positive

        //    // Determine whether we need to accelerate to reach vt
        //    bool needsAcceleration = v0_dir < vt;

        //    // Calculate acceleration distance and time to reach vt
        //    float s1 = 0f;
        //    float t1 = 0f;

        //    if (needsAcceleration)
        //    {
        //        // Acceleration phase
        //        s1 = (vt * vt - v0_dir * v0_dir) / (2 * a);
        //        t1 = (vt - v0_dir) / a;
        //    }
        //    else
        //    {
        //        // Deceleration phase (not applicable without deceleration at the end)
        //        s1 = 0f;
        //        t1 = 0f;
        //        vt = v0_dir; // Maintain current speed
        //    }

        //    // Check if we can reach vt before reaching the target position
        //    if (s1 >= deltaX_remaining)
        //    {
        //        // Cannot reach vt; adjust maximum speed
        //        float vMaxSquared = v0_dir * v0_dir + 2 * a * deltaX_remaining;
        //        float vMax = (float)Math.Sqrt(Math.Max(0, vMaxSquared));

        //        s1 = deltaX_remaining;
        //        t1 = (vMax - v0_dir) / a;
        //        vt = vMax;
        //    }

        //    float s2 = deltaX_remaining - s1;
        //    float t2 = s2 / vt;

        //    totalTime += t1 + t2;
        //    timeToAccel = t1;

        //    return totalTime;
        //}


        private bool TryParseArguments(object?[] arguments, out char[] deviceNames, out float radius, out float[] centerPositions, out float startAngle, out float endAngle, out bool isCCW)
        {
            deviceNames = Array.Empty<char>();
            radius = 0f;
            centerPositions = Array.Empty<float>();
            startAngle = 0f;
            endAngle = 0f;
            isCCW = false;

            int idx = 0;

            int expectedArgumentCount = 1 + 1 + 2 + 1 + 1 + 1;
            if (arguments.Length != expectedArgumentCount)
                return false;

            if (arguments == null || arguments.Length == 0)
                return false;

            // names
            if (arguments[0] is not string firstArg)
                return false;
            deviceNames = firstArg.ToCharArray();
            idx++;

            // radius
            if (!TryConvertToFloat(arguments[idx], out radius))
                return false;
            idx++;

            // cemter_positions
            centerPositions = new float[2];
            for (int i = 0; i < 2; i++)
            {
                if (!TryConvertToFloat(arguments[i + idx], out centerPositions[i]))
                    return false;
            }
            idx += 2;

            // start angle
            if (!TryConvertToFloat(arguments[idx], out startAngle))
                return false;
            idx++;

            // end angle
            if (!TryConvertToFloat(arguments[idx], out endAngle))
                return false;
            idx++;

            // names
            if (arguments[idx] is not bool parsedIsCCW)
                return false;
            isCCW = parsedIsCCW;

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
