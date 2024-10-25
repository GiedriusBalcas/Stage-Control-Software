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

        public ArcAbsoluteFunction(CommandManager commandManager, ControllerManager controllerManager, JumpAbsoluteFunction jumpAbsoluteFunction)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            _jumpFunction = jumpAbsoluteFunction;

            SetProperty("Shutter", false);
            SetProperty("Accuracy", 0.1f);
            SetProperty("TimeAccuracy", 0.02f);
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
                            if(positionerDevice.Acceleration != positionerMovementInfos[deviceName].TargetAcceleration || positionerDevice.Deceleration != positionerMovementInfos[deviceName].TargetDeceleration)
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
                    Blending = true
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

            
            float segmentLength = Math.Max(trajectorySpeed * accuracy_time, accuracy);
            float dtheta = (float)(2 * Math.Atan(segmentLength / (2 * radius)));

            // lets round to fit segments nicely on our arc.
            int numberOfSegments = (int)Math.Round(Math.Abs(endAngle - startAngle) / dtheta);

            if(float.IsNaN(numberOfSegments) || numberOfSegments < 2)
            {
                throw new Exception("Unable to create an arc trajectory, number of segments is less than two.");
            }

            dtheta = Math.Abs(endAngle - startAngle) / numberOfSegments;

            //float additionalDistance/*ToStop = 2 * trajectorySpeed * trajectorySpeed / (2 * devices_arc.Min(device => Math.Min(device.Value.MaxAcceleration, device.Value.MaxDeceleration))) + 0.2f * trajectorySpeed; // [um*/

            // we will prologate the distance to travel, to give some time for next command execution.
            float additionalDistanceToStop = 0.2f * trajectorySpeed + 1 * trajectorySpeed * trajectorySpeed / (2 * devices_arc.Min(device => Math.Min(device.Value.MaxAcceleration, device.Value.MaxDeceleration))) + 0.2f * trajectorySpeed; // [um
            

            double angularVelocity = (trajectorySpeed / radius);
            var arcLength = dtheta * radius;
            float rethrow = arcLength / trajectorySpeed;

            double prevVelX = 0;
            double prevVelY = 0;

            float Ax, Ay, Bx, By;

            
            // move to start.
            var startPosX = (float)(Math.Cos(startAngle) * radius) + centerX;
            var startPosY = (float)(Math.Sin(startAngle) * radius) + centerY;

            double startVelX = Math.Abs(-trajectorySpeed * Math.Sin(startAngle));
            double startVelY = Math.Abs(trajectorySpeed * Math.Cos(startAngle));

            bool needStartMovement = false;
            bool needPositionChange = Math.Abs(startPosX - xDevice.CurrentPosition) > accuracy || Math.Abs(startPosY - yDevice.CurrentPosition) > accuracy;
            bool needSpeedChange = Math.Abs(startVelX - xDevice.CurrentSpeed) > accuracy / accuracy_time || Math.Abs(startVelY - yDevice.CurrentSpeed) > accuracy / accuracy_time;
            if (needPositionChange | needSpeedChange)
                needStartMovement = true;

            //if (needStartMovement || leadIn)
            //{
            //    // calculate lead in offset needed. to achieve startVelocities (startVelX, startVelY) at the startPositions (startPosX, startPosY)
            //    double accelX = xDevice.MaxAcceleration;
            //    double accelY = yDevice.MaxAcceleration;

            //    double deltaVx = startVelX;
            //    double deltaVy = startVelY;

            //    double leadInDistanceX = (deltaVx * deltaVx) / (2 * accelX);
            //    double leadInDistanceY = (deltaVy * deltaVy) / (2 * accelY);

            //    // Take the maximum required distance
            //    double leadInDistance = Math.Max( Math.Max(Math.Abs(leadInDistanceX), Math.Abs(leadInDistanceY)), accuracy * 2);
            //    double magnitude = Math.Sqrt(startVelX * startVelX + startVelY * startVelY);
            //    double dx = -startVelX / magnitude;
            //    double dy = -startVelY / magnitude;


            //    double leadInStartPosX = startPosX - dx * leadInDistance;
            //    double leadInStartPosY = startPosY + dy * leadInDistance;

            //    // jump to lead in start here without blending = false;

            //    var positionerMovementInfo_start = new Dictionary<char, PositionerMovementInformation>();
            //    var movementInfo_start = new PositionerMovementInformation
            //    {
            //        TargetPosition = (float)leadInStartPosX,
            //        TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetSpeed = (float)Math.Max(xDevice.DefaultSpeed, 1),
            //    };
            //    positionerMovementInfo_start[xName] = movementInfo_start;
            //    movementInfo_start = new PositionerMovementInformation
            //    {
            //        TargetPosition = (float)leadInStartPosY,
            //        TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetSpeed = (float)Math.Max(yDevice.DefaultSpeed, 1),
            //    };
            //    positionerMovementInfo_start[yName] = movementInfo_start;

            //    var commands_update_start = CreateUpdateCommands(positionerMovementInfo_start, groupedDevicesByController_arc);
            //    _commandManager.EnqueueCommandLine(commands_update_start);
            //    _commandManager.ExecuteCommandLine(commands_update_start).GetAwaiter().GetResult();
            //    _jumpFunction.ExecutionCore([xDevice.Name, yDevice.Name], [(float)leadInStartPosX, (float)leadInStartPosY], false, accuracy, null, null, false);


            //    // calculate the offset of the end of movement
            //    CalculateTangentEndpoint(
            //        centerX, centerY,
            //        (double)startPosX, (double)startPosY,
            //        additionalDistanceToStop,
            //        true,
            //        out double x_tang, out double y_tang);

            //    // jump to x_tang, y_tang

            //    positionerMovementInfo_start = new Dictionary<char, PositionerMovementInformation>();
            //    movementInfo_start = new PositionerMovementInformation
            //    {
            //        TargetPosition = (float)x_tang,
            //        TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetSpeed = (float)Math.Max(startVelX, 1),
            //    };
            //    positionerMovementInfo_start[xName] = movementInfo_start;
            //    movementInfo_start = new PositionerMovementInformation
            //    {
            //        TargetPosition = (float)y_tang,
            //        TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetSpeed = (float)Math.Max(startVelY, 1),
            //    };
            //    positionerMovementInfo_start[yName] = movementInfo_start;

            //    commands_update_start = CreateUpdateCommands(positionerMovementInfo_start, groupedDevicesByController_arc);
            //    _commandManager.EnqueueCommandLine(commands_update_start);
            //    _commandManager.ExecuteCommandLine(commands_update_start).GetAwaiter().GetResult();

            //    // CREATE MOVEMENT COMMANDS
            //    _jumpFunction.ExecutionCore([xDevice.Name, yDevice.Name], [(float)x_tang, (float)y_tang], shutter, accuracy, null, [startPosX, startPosY], true); //[startPosX, startPosY]
            //}

            float accelerationForArc = 5000f;

            var positionerMovementInfo = new Dictionary<char, PositionerMovementInformation>();
            var movementInfo = new PositionerMovementInformation
            {
                TargetAcceleration = (float)Math.Max(accelerationForArc, 10),
                TargetDeceleration = (float)Math.Max(accelerationForArc, 10),
                TargetSpeed = (float)Math.Max(trajectorySpeed, 1),
            };
            positionerMovementInfo[xName] = movementInfo;
            movementInfo = new PositionerMovementInformation
            {
                TargetAcceleration = (float)Math.Max(accelerationForArc, 10),
                TargetDeceleration = (float)Math.Max(accelerationForArc, 10),
                TargetSpeed = (float)Math.Max(trajectorySpeed, 1),
            };
            positionerMovementInfo[yName] = movementInfo;

            var commands_update = CreateUpdateCommands(positionerMovementInfo, groupedDevicesByController);
            _commandManager.EnqueueCommandLine(commands_update);
            _commandManager.ExecuteCommandLine(commands_update).GetAwaiter().GetResult();

            // loop through the segments until last one.
            prevVelX = Math.Abs(-trajectorySpeed * Math.Sin(startAngle));
            prevVelY = Math.Abs(trajectorySpeed * Math.Cos(startAngle));

            for (float theta = startAngle + dtheta; theta <= endAngle - dtheta; theta+= dtheta)
            {
                Ax = (float)(Math.Cos(theta - dtheta) * radius) + centerX;
                Ay = (float)(Math.Sin(theta - dtheta) * radius) + centerY;

                Bx = (float)(Math.Cos(theta) * radius) + centerX;
                By = (float)(Math.Sin(theta) * radius) + centerY;

                var midX = (Bx - Ax) / 2;
                var midY = (By - Ay) / 2;

                // CALCULATE ACCEL/DECELL AT MID POINT

                double accX = Math.Abs(-trajectorySpeed * angularVelocity * Math.Cos(theta - dtheta));
                double accY = Math.Abs(-trajectorySpeed * angularVelocity * Math.Sin(theta - dtheta));

                double velX = Math.Abs(-trajectorySpeed * Math.Sin(theta));
                double velY = Math.Abs(trajectorySpeed * Math.Cos(theta));

                double accelX_calc = (prevVelX - velX) / rethrow;
                double accelY_calc = (prevVelY - velY) / rethrow;

                if (accelX_calc > xDevice.MaxAcceleration || accelY_calc > yDevice.MaxAcceleration)
                    throw new Exception("Max Acceleration value of the device is issuficient for the arc movement.");

                prevVelX = velX;
                prevVelY = velY;
                
                // CALCULATE THE TANGENT ENDPOINT
                CalculateTangentEndpoint(
                    centerX, centerY,
                    (double)Bx, (double)By,
                    additionalDistanceToStop,
                    true,
                    out double x_tang, out double y_tang);

                // CREATE UPDATE MOVEMENT SETTINGS COMMANDS


                Message += "---------------------------------------------------------------------------------------------\n";
                Message += $"starting at: {xDevice.CurrentPosition}  |  {yDevice.CurrentPosition}\n";
                Message += $"rethrow at:  {Bx}  |  {By}\n";
                Message += $"target:      {x_tang}  |  {y_tang}\n";
                Message += $"speed:       {positionerMovementInfo[xName].TargetSpeed}  |  {positionerMovementInfo[yName].TargetSpeed}\n";
                Message += $"accel:       {positionerMovementInfo[xName].TargetAcceleration}  |  {positionerMovementInfo[yName].TargetAcceleration}\n";
                Message += $"decel:       {positionerMovementInfo[xName].TargetDeceleration}  |  {positionerMovementInfo[yName].TargetDeceleration}\n";

                Message += "---------------------------------------------------------------------------------------------\n";

                // CREATE MOVEMENT COMMANDS
                //_jumpFunction.ExecutionCore([xDevice.Name, yDevice.Name], [(float)x_tang, (float)y_tang], false, accuracy, rethrow, null, true); //[Bx, By]
                //_jumpFunction.ExecutionCore([xDevice.Name, yDevice.Name], [(float)Bx, (float)By], false, accuracy, null, [midX, midY], true); //[Bx, By]

                var positionerMovementInformation = GetMovementInformation(xDevice, yDevice, Bx, By, trajectorySpeed, (float)velX, (float)velY, accelerationForArc, out float allocatedTime);

                // Create the movement commands.
                List<Command> commandsMovement = CreateMovementCommands(false, groupedDevicesByController, positionerMovementInformation, allocatedTime, allocatedTime, null);

                _commandManager.EnqueueCommandLine(commandsMovement.ToArray());
                _commandManager.ExecuteCommandLine(commandsMovement.ToArray()).GetAwaiter().GetResult();
            }




            // move to end.
            var endPosX = (float)(Math.Cos(endAngle) * radius) + centerX;
            var endPosY = (float)(Math.Sin(endAngle) * radius) + centerY;

            double endVelX = Math.Abs(-trajectorySpeed * Math.Sin(endAngle));
            double endVelY = Math.Abs(trajectorySpeed * Math.Cos(endAngle));

            //if (leadOut)
            //{
            //    // calculate lead off offset needed

            //    var additionalLeadOffDistance = trajectorySpeed * trajectorySpeed / (2 * devices_arc.Min(device => Math.Min(device.Value.MaxAcceleration, device.Value.MaxDeceleration)));

            //    // calculate the offset of the end of movement
            //    CalculateTangentEndpoint(
            //        centerX, centerY,
            //        (double)endPosX, (double)endPosY,
            //        additionalLeadOffDistance,
            //        true,
            //        out double x_tang, out double y_tang);

            //    // jump to x_tan, y_tang, blending = true
                
            //    var positionerMovementInfo = new Dictionary<char, PositionerMovementInformation>();
            //    var movementInfo = new PositionerMovementInformation
            //    {
            //        TargetPosition = (float)x_tang,
            //        TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetSpeed = (float)Math.Max(endVelX, 1),
            //    };
            //    positionerMovementInfo[xName] = movementInfo;
            //    movementInfo = new PositionerMovementInformation
            //    {
            //        TargetPosition = (float)y_tang,
            //        TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetSpeed = (float)Math.Max(endVelY, 1),
            //    };
            //    positionerMovementInfo[yName] = movementInfo;

            //    var commands_update = CreateUpdateCommands(positionerMovementInfo, groupedDevicesByController_arc);
            //    _commandManager.EnqueueCommandLine(commands_update);
            //    _commandManager.ExecuteCommandLine(commands_update).GetAwaiter().GetResult();

            //    // CREATE MOVEMENT COMMANDS
            //    _jumpFunction.ExecutionCore([xDevice.Name, yDevice.Name], [(float)x_tang, (float)y_tang], shutter, accuracy, null, null, true);
            //}
            //else
            //{
            //    var positionerMovementInfo = new Dictionary<char, PositionerMovementInformation>();
            //    var movementInfo = new PositionerMovementInformation
            //    {
            //        TargetPosition = (float)endPosX,
            //        TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetSpeed = (float)Math.Max(endVelX, 1),
            //    };
            //    positionerMovementInfo[xName] = movementInfo;
            //    movementInfo = new PositionerMovementInformation
            //    {
            //        TargetPosition = (float)endPosY,
            //        TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
            //        TargetSpeed = (float)Math.Max(endVelY, 1),
            //    };
            //    positionerMovementInfo[yName] = movementInfo;

            //    var commands_update = CreateUpdateCommands(positionerMovementInfo, groupedDevicesByController_arc);
            //    _commandManager.EnqueueCommandLine(commands_update);
            //    _commandManager.ExecuteCommandLine(commands_update).GetAwaiter().GetResult();

            //    // CREATE MOVEMENT COMMANDS
            //    _jumpFunction.ExecutionCore([xDevice.Name, yDevice.Name], [(float)endPosX, (float)endPosY], shutter, accuracy, null, null, true);
            //}

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
                        //WaitUntilPosition = waitUntilPosDict is not null ? waitUntilPosDict[deviceName] : null, // TODO: Implement waitUntil logic if necessary
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
        private Dictionary<char, PositionerMovementInformation> GetMovementInformation(BasePositionerDevice deviceX, BasePositionerDevice deviceY, float targetPositionX, float targetPositionY, float trajectorySpeed, float speedX, float speedY, float acceleration, out float allocatedTime)
        {
                var positionerMovementInfos_jump = new Dictionary<char, PositionerMovementInformation>();

            var targetPosition = targetPositionX;
            var targetDistance = Math.Abs(targetPosition - deviceX.CurrentPosition);
            var targetDirection = targetPosition > deviceX.CurrentPosition;

            var movementInfo = new PositionerMovementInformation
            {
                StartingPosition = deviceX.CurrentPosition,
                StartingSpeed = deviceX.CurrentSpeed,
                CurrentTargetSpeed = deviceX.Speed,
                StartingAcceleration = deviceX.Acceleration,
                StartingDeceleration = deviceX.Deceleration,
                MaxAcceleration = deviceX.MaxAcceleration,
                MaxDeceleration = deviceX.MaxDeceleration,
                MaxSpeed = deviceX.MaxSpeed,
                TargetAcceleration = acceleration,
                TargetDeceleration = acceleration,
                TargetSpeed = speedX,
                TargetPosition = targetPosition,
                TargetDistance = targetDistance,
                TargetDirection = targetDirection,
            };

            positionerMovementInfos_jump[deviceX.Name] = movementInfo;



            targetPosition = targetPositionY;
            targetDistance = Math.Abs(targetPosition - deviceY.CurrentPosition);
            targetDirection = targetPosition > deviceY.CurrentPosition;

            movementInfo = new PositionerMovementInformation
            {
                StartingPosition = deviceY.CurrentPosition,
                StartingSpeed = deviceY.CurrentSpeed,
                CurrentTargetSpeed = deviceY.Speed,
                StartingAcceleration = deviceY.Acceleration,
                StartingDeceleration = deviceY.Deceleration,
                MaxAcceleration = deviceY.MaxAcceleration,
                MaxDeceleration = deviceY.MaxDeceleration,
                MaxSpeed = deviceY.MaxSpeed,
                TargetAcceleration = acceleration,
                TargetDeceleration = acceleration,
                TargetSpeed = speedY,
                TargetPosition = targetPosition,
                TargetDistance = targetDistance,
                TargetDirection = targetDirection,
            };

            positionerMovementInfos_jump[deviceY.Name] = movementInfo;
            
            var calculatedTime_x = CalculateTotalTimeForMovementInfo(movementInfo, out float timeToAccel_x, out float timeToDecel_x, out float totalTime_X);
            var calculatedTime_y = CalculateTotalTimeForMovementInfo(movementInfo, out float timeToAccel_y, out float timeToDecel_y, out float totalTime_y);

            allocatedTime = Math.Max(calculatedTime_x, calculatedTime_y);

            return positionerMovementInfos_jump;
        }

        private float CalculateTotalTimeForMovementInfo(PositionerMovementInformation info, out float timeToAccel, out float timeToDecel, out float totalTime)
        {
            timeToAccel = 0f;
            timeToDecel = 0f;
            totalTime = 0f;

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


            // If initial speed is in the opposite direction, decelerate to zero first
            if (v0_dir < 0)
            {
                // Time to decelerate to zero speed
                float t_stop = -v0_dir / d;
                // Distance covered during deceleration
                float s_stop = v0_dir * t_stop + 0.5f * (-d) * t_stop * t_stop;
                s_stop = Math.Abs(s_stop);

                totalTime += t_stop;
                deltaX_total -= s_stop * direction; // Remaining distance after stopping

                v0_dir = 0; // Reset initial speed after stopping
            }

            float deltaX_remaining = Math.Abs(deltaX_total);

            // Compute candidate maximum speed
            float numerator = 2 * a * d * deltaX_remaining + d * v0_dir * v0_dir;
            float denominator = a + d;
            float vMaxSquaredCandidate = numerator / denominator;
            float vMaxCandidate = (float)Math.Sqrt(vMaxSquaredCandidate);

            // Limit maximum speed to the target speed
            float vMax = Math.Min(vMaxCandidate, vt);

            // Calculate distances for acceleration and deceleration phases
            float s1 = (vMax * vMax - v0_dir * v0_dir) / (2 * a);
            float s3 = (vMax * vMax) / (2 * d);
            float s_total_required = s1 + s3;

            if (s_total_required > deltaX_remaining)
            {
                // Triangular profile
                vMaxSquaredCandidate = (2 * a * d * deltaX_remaining + d * v0_dir * v0_dir) / (a + d);
                vMax = (float)Math.Sqrt(vMaxSquaredCandidate);

                // Recalculate times
                float t1 = (vMax - v0_dir) / a;
                float t3 = vMax / d;
                totalTime += t1 + t3;

                timeToAccel = t1;
                timeToDecel = t3;
            }
            else
            {
                // Trapezoidal profile
                float s2 = deltaX_remaining - s1 - s3;

                // Calculate times for each phase
                float t1 = (vMax - v0_dir) / a;
                float t2 = s2 / vMax;
                float t3 = vMax / d;

                totalTime += t1 + t2 + t3;
                timeToAccel = t1;
                timeToDecel = t3;
            }

            return totalTime;
        }

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
