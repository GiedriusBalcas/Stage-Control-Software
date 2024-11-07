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
using standa_controller_software.device_manager.devices.positioning;

namespace standa_controller_software.custom_functions.definitions
{
    public class SetPowerFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private readonly CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;
        private readonly JumpAbsoluteFunction _jumpAbsoluteFunction;

        public SetPowerFunction(CommandManager commandManager, ControllerManager controllerManager, JumpAbsoluteFunction jumpAbsoluteFunction)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            _jumpAbsoluteFunction = jumpAbsoluteFunction;
            SetProperty("Shutter", false);
            SetProperty("Accuracy", 0.05f);
            SetProperty("LeadOut", false);
            SetProperty("WaitUntilTime", null, true);
            SetProperty("Blending", false);
        }


        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out var parsedDeviceNames, out var parsedPositions))
                throw new ArgumentException("Argument parsing was unsuccessful. Wrong types.");


            if (!TryGetProperty("Accuracy", out var accuracyObj))
                throw new Exception("Failed to get 'Accuracy' property.");
            var accuracy = (float)accuracyObj;


            ExecutionCore(parsedDeviceNames, parsedPositions, accuracy);

            return null;
        }

        public void ExecutionCore(char[] parsedDeviceNames, float[] parsedPowerValues, float accuracy)
        {

            /// Jump command, starts movement from initial position & speed
            /// to a target position.
            /// Trajectory is ignored, unlike the Line command.
            /// Jerk is ignored.


            var deviceNameList = new List<char>();
            var positionsList = new List<float>();

            // Build lists of devices that need to move
            for (int i = 0; i < parsedDeviceNames.Length; i++)
            {
                if (_controllerManager.TryGetDevice<AttenuatorPositionerDevice>(parsedDeviceNames[i], out var attenuator))
                {
                    var convertedPosition = attenuator.ConvertFromPowerToPosition(parsedPowerValues[i]);
                    var isAtTargetPosition = Math.Abs(attenuator.CurrentPosition - convertedPosition) < accuracy;
                    var isMoving = attenuator.CurrentSpeed > 0;

                    if (!isAtTargetPosition || isMoving)
                    {
                        deviceNameList.Add(parsedDeviceNames[i]);
                        positionsList.Add(convertedPosition);
                    }
                }
                else
                {
                    throw new Exception($"Unable to retrieve attenuator device {parsedDeviceNames[i]}.");
                }
            }

            if (deviceNameList.Count == 0)
                return;

            var deviceNames = deviceNameList.ToArray();
            var positions = positionsList.ToArray();

            _jumpAbsoluteFunction.ExecutionCore(deviceNames, positions, false, accuracy, null, null, false);
        }


        private bool TryParseArguments(object?[] arguments, out char[] devNames, out float[] positions)
        {
            devNames = Array.Empty<char>();
            positions = Array.Empty<float>();

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
