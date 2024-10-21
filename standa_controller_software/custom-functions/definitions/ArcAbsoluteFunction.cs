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
    public class ArcAbsoluteFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private readonly CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;

        public ArcAbsoluteFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            SetProperty("Shutter", false);
            SetProperty("Accuracy", 0.1f);
        }

        public override object? Execute(params object[] args)
        {
            //if (!TryParseArguments(args, out var parsedDeviceNames, out var parsedPositions, out var parsedWaitUntil))
            //    throw new ArgumentException("Argument parsing was unsuccessful. Wrong types.");

            //if (!TryGetProperty("Shutter", out var isShutterUsedObj))
            //    throw new Exception("Failed to get 'Shutter' property.");
            //var isShutterUsed = (bool)isShutterUsedObj;

            //if (!TryGetProperty("Accuracy", out var accuracyObj))
            //    throw new Exception("Failed to get 'Accuracy' property.");
            //var accuracy = (float)accuracyObj;

            _controllerManager.TryGetDevice<BasePositionerDevice>('x', out var devicex);
            var currX = devicex.CurrentPosition;
            _controllerManager.TryGetDevice<BasePositionerDevice>('y', out var devicey);
            var currY = devicey.CurrentPosition;

            float radius = 100f;


            // pazek gal accel max koks. o ne apskritimo lygti sprest, gi nemoki.
            // greiti reik padidint kolkas. gal tas accel maiso kazkur.

            // looks like we need faster updates.

            
            ExecutionCore('x', 'y', radius, 0f, 0f, 0, (float)Math.PI*2*2, 500f, 0.01f);

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


        private Command[] CreateMovementCommands(Dictionary<char, PositionerMovementInformation> positionerMovementInfos, Dictionary<BasePositionerController, List<BasePositionerDevice>> groupedDevicesByController, float allocatedTime)
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
                        WaitUntilTime = positionerMovementInfos[deviceName].Rethrow, // TODO: Implement waitUntil logic if necessary
                        TargetSpeed = positionerMovementInfos[deviceName].TargetSpeed,
                        Direction = positionerMovementInfos[deviceName].TargetDirection,
                        TargetPosition = positionerMovementInfos[deviceName].TargetPosition,
                    });

                var moveAParameters = new MoveAbsoluteParameters
                {
                    AllocatedTime = allocatedTime,
                    PositionerInfo = positionerInfos,
                 
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

            return commandsMovement.ToArray();
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

        public void ExecutionCore(char xName, char yName, float radius, float centerX, float centerY, float startAngle, float endAngle, float trajectorySpeed, float accuracy_time)
        {
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

            var groupedDevicesByController_arc = devices_arc.Values
                .GroupBy(device => controllers_arc[device])
                .ToDictionary(group => group.Key, group => group.ToList());

            // minimum segment criteria?
            // time?? segment_length / speed >= accuracy

            //TODO: Let's make sure we have high enough Accel/Decel values for theorethical circle.

            float segmentLength = trajectorySpeed * accuracy_time;

            // segment length must be at least twice as long as the deceleration distance for any of the stages. <-------- BS

            float maxDistanceToDescelerate = 2 * trajectorySpeed * trajectorySpeed / (2* devices_arc.Min(device => Math.Min(device.Value.MaxAcceleration, device.Value.MaxDeceleration))) + 0.1f * trajectorySpeed; // [um
            //float maxDistanceToDescelerate = 100; // [um]


            float dtheta = (float)(2 * Math.Atan(segmentLength / (2 * radius)));
            double angularVelocity = (trajectorySpeed / radius);
            var arcLength = dtheta * radius;
            float rethrow = arcLength / trajectorySpeed;
            double prevVelX = 0;
            double prevVelY = 0;
            double neededAccel = 0;

            float Ax, Ay, Bx = 0, By = 0;

            for (float theta = startAngle + dtheta; theta <= endAngle - dtheta; theta += dtheta)
            {
                Ax = (float)(Math.Cos(theta - dtheta) * radius) + centerX;
                Ay = (float)(Math.Sin(theta - dtheta) * radius) + centerY;

                Bx = (float)(Math.Cos(theta) * radius) + centerX;
                By = (float)(Math.Sin(theta) * radius) + centerY;

                float midX = (float)(Math.Cos(theta - dtheta / 2) * radius) + centerX;
                float midY = (float)(Math.Sin(theta - dtheta / 2) * radius) + centerY;

                // CALCULATE ACCEL/DECELL AT MID POINT

                double accX = Math.Abs(-trajectorySpeed * angularVelocity * Math.Cos(theta - dtheta));
                double accY = Math.Abs(-trajectorySpeed * angularVelocity * Math.Sin(theta - dtheta));

                double velX = Math.Abs(-trajectorySpeed * Math.Sin(theta));
                double velY = Math.Abs(trajectorySpeed * Math.Cos(theta));

                double accelX_calc = Math.Abs((prevVelX - velX) / rethrow);
                neededAccel = Math.Max(neededAccel, accelX_calc);

                prevVelX = velX;
                prevVelY = velY;
            }
            prevVelX = 0;
            prevVelY = 0;

            for (float theta = startAngle;  theta <= endAngle - dtheta; theta+= dtheta)
            {
                Ax = (float)(Math.Cos(theta - dtheta) * radius) + centerX;
                Ay = (float)(Math.Sin(theta - dtheta) * radius) + centerY;

                Bx = (float)(Math.Cos(theta) * radius) + centerX;
                By = (float)(Math.Sin(theta) * radius) + centerY;

                float midX = (float)(Math.Cos(theta - dtheta/2) * radius) + centerX;
                float midY = (float)(Math.Sin(theta - dtheta / 2) * radius) + centerY;

                // CALCULATE ACCEL/DECELL AT MID POINT

                double accX = Math.Abs(-trajectorySpeed * angularVelocity * Math.Cos(theta - dtheta));
                double accY = Math.Abs(-trajectorySpeed * angularVelocity * Math.Sin(theta - dtheta));

                double velX = Math.Abs(-trajectorySpeed * Math.Sin(theta));
                double velY = Math.Abs(trajectorySpeed * Math.Cos(theta));

                double accelX_calc = (prevVelX - velX) / rethrow;
                double accelY_calc = (prevVelY - velY) / rethrow;


                prevVelX = velX;
                prevVelY = velY;
                // CALCULATE RETHROW

                // CALCULATE THE TANGENT ENDPOINT

                /// x_tang,y_tang are the end point of movement operation
                /// 

                CalculateTangentEndpoint(
                    centerX, centerY,
                    (double)Bx, (double)By,
                    maxDistanceToDescelerate,
                    true,
                    out double x_tang, out double y_tang);

                // CREATE UPDATE MOVEMENT SETTINGS COMMANDS

                var positionerMovementInfo = new Dictionary<char, PositionerMovementInformation>();
                var movementInfo = new PositionerMovementInformation
                {
                    TargetPosition = (float)x_tang,
                    //TargetAcceleration = (float)Math.Max(neededAccel, 10),
                    //TargetDeceleration = (float)Math.Max(neededAccel, 10),
                    TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
                    TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
                    //TargetAcceleration = (float)Math.Max(Math.Abs(accelX_calc), 10),
                    //TargetDeceleration = (float)Math.Max(Math.Abs(accelX_calc), 10),
                    //TargetSpeed = (float)Math.Max(velX,1),
                    TargetSpeed = (float)Math.Max(xDevice.MaxSpeed, 1),

                    Rethrow = rethrow,
                };
                positionerMovementInfo[xName] = movementInfo;
                movementInfo = new PositionerMovementInformation
                {
                    TargetPosition = (float)y_tang,
                    TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
                    TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
                    //TargetAcceleration = (float)Math.Max(Math.Abs(accelY_calc), 10),
                    //TargetDeceleration = (float)Math.Max(Math.Abs(accelY_calc), 10),
                    //TargetSpeed = (float)Math.Max(velY, 1),
                    TargetSpeed = (float)Math.Max(xDevice.MaxSpeed, 1),

                    Rethrow = rethrow,
                };
                positionerMovementInfo[yName] = movementInfo;

                var commands_update = CreateUpdateCommands(positionerMovementInfo, groupedDevicesByController_arc);

                _commandManager.EnqueueCommandLine(commands_update);
                _commandManager.ExecuteCommandLine(commands_update).GetAwaiter().GetResult();
                // CREATE MOVEMENT COMMANDS

                float allocatedTime_arc = (float)(Math.Sqrt((y_tang - Ay) * (y_tang - Ay) + (x_tang - Ax) * (x_tang - Ax)) / trajectorySpeed);
                allocatedTime_arc = (float) ((arcLength + maxDistanceToDescelerate) / trajectorySpeed);

                var commands_movement = CreateMovementCommands(positionerMovementInfo, groupedDevicesByController_arc, allocatedTime_arc);
                _commandManager.EnqueueCommandLine(commands_movement);
                _commandManager.ExecuteCommandLine(commands_movement).GetAwaiter().GetResult();

            }

            double Bx_last = (float)(Math.Cos(endAngle) * radius) + centerX;
            double By_last = (float)(Math.Sin(endAngle) * radius) + centerY;

            double velX_last = Math.Abs(-trajectorySpeed * Math.Sin(endAngle));
            double velY_last = Math.Abs(trajectorySpeed * Math.Cos(endAngle));

            double accelX_calc_last = (prevVelX - velX_last) / rethrow;
            double accelY_calc_last = (prevVelY - velY_last) / rethrow;
            
            
            var positionerMovementInfo_last = new Dictionary<char, PositionerMovementInformation>();
            var movementInfo_last = new PositionerMovementInformation
            {
                TargetPosition = (float)Bx,
                //TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
                //TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
                TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
                TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
                TargetSpeed = (float)Math.Max(velX_last, 1),
                Rethrow = rethrow,
            };
            positionerMovementInfo_last[xName] = movementInfo_last;
            movementInfo_last = new PositionerMovementInformation
            {
                TargetPosition = (float)By,
                TargetAcceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
                TargetDeceleration = (float)Math.Max(xDevice.MaxAcceleration, 10),
                TargetSpeed = (float)Math.Max(velY_last, 1),
                Rethrow = rethrow,
            };
            positionerMovementInfo_last[yName] = movementInfo_last;

            var commands_update_last = CreateUpdateCommands(positionerMovementInfo_last, groupedDevicesByController_arc);

            _commandManager.EnqueueCommandLine(commands_update_last);
            _commandManager.ExecuteCommandLine(commands_update_last).GetAwaiter().GetResult();
            // CREATE MOVEMENT COMMANDS

            float allocatedTime_arc_last = (arcLength) / trajectorySpeed * 1.5f;

            var commands_movement_last = CreateMovementCommands(positionerMovementInfo_last, groupedDevicesByController_arc, allocatedTime_arc_last);
            _commandManager.EnqueueCommandLine(commands_movement_last);
            _commandManager.ExecuteCommandLine(commands_movement_last).GetAwaiter().GetResult();

            var akka = 1;
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
