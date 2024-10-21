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
    public class LineAbsoluteFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private readonly CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;

        public LineAbsoluteFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            SetProperty("Shutter", false);
            SetProperty("Accuracy", 0.1f);
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

            var deviceNameList = new List<char>();
            var positionsList = new List<float>();
            var waitUntilList = new List<float>();

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
                        if (parsedWaitUntil.Length > i)
                            waitUntilList.Add(parsedWaitUntil[i]);
                    }
                }
                else
                {
                    throw new Exception($"Unable to retrieve positioner device {parsedDeviceNames[i]}.");
                }
            }

            if (deviceNameList.Count == 0)
                return null;

            var deviceNames = deviceNameList.ToArray();
            var positions = positionsList.ToArray();
            var waitUntil = waitUntilList.ToArray();

            // Map devices and controllers
            var devices = new Dictionary<char, BasePositionerDevice>();
            var positionerMovementInfos = new Dictionary<char, PositionerMovementInformation>();
            float allocatedTime = 0f;

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
                        TargetPosition = targetPosition,
                        TargetDistance = targetDistance,
                        TargetDirection = targetDirection,
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
                    };

                    positionerMovementInfos[name] = movementInfo;

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

            // Retrieve controllers and group devices by controller
            var controllers = new Dictionary<BasePositionerDevice, BasePositionerController>();
            foreach (var device in devices.Values)
            {
                if (_controllerManager.TryGetDeviceController<BasePositionerController>(device.Name, out var controller))
                {
                    controllers[device] = controller;
                }
                else
                {
                    throw new Exception($"Unable to retrieve controller for device {device.Name}.");
                }
            }

            var groupedDevicesByController = devices.Values
                .GroupBy(device => controllers[device])
                .ToDictionary(group => group.Key, group => group.ToList());

            // Check if kinematic parameters need to be updated
            bool kinematicParametersNeedUpdate = positionerMovementInfos.Values.Any(info =>
                info.TargetAcceleration != info.StartingAcceleration ||
                info.TargetDeceleration != info.StartingDeceleration ||
                info.TargetSpeed != info.CurrentTargetSpeed);

            if (kinematicParametersNeedUpdate)
            {
                var updateParametersCommandLine = new List<Command>();

                foreach (var controllerGroup in groupedDevicesByController)
                {
                    var controllerName = controllerGroup.Key.Name;
                    var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();

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
                        MovementSettingsInformation = movementSettings
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

                _commandManager.EnqueueCommandLine(updateParametersCommandLine.ToArray());
                _commandManager.ExecuteCommandLine(updateParametersCommandLine.ToArray()).GetAwaiter().GetResult();
            }

            // Prepare movement commands
            var commandsMovement = new List<Command>();

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var controllerName = controllerGroup.Key.Name;
                var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();

                var positionerInfos = groupedDeviceNames.ToDictionary(
                    deviceName => deviceName,
                    deviceName => new PositionerInfo
                    {
                        WaitUntil = null, // TODO: Implement waitUntil logic if necessary
                        TargetSpeed = positionerMovementInfos[deviceName].TargetSpeed,
                        Direction = positionerMovementInfos[deviceName].TargetDirection,
                        TargetPosition = positionerMovementInfos[deviceName].TargetPosition,
                    });

                var moveAParameters = new MoveAbsoluteParameters
                {
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

            _commandManager.EnqueueCommandLine(commandsMovement.ToArray());
            _commandManager.ExecuteCommandLine(commandsMovement.ToArray()).GetAwaiter().GetResult();

            return null;
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
