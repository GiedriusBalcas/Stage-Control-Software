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
    public class ChangeShutterStateForIntervalFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private readonly CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;
        private readonly JumpAbsoluteFunction _jumpAbsoluteFunction;

        public ChangeShutterStateForIntervalFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
        }


        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out var parsedDeviceNames, out float duration))
                throw new ArgumentException("Argument parsing was unsuccessful. Wrong types.");


            ExecutionCore(parsedDeviceNames, duration);

            return null;
        }

        public void ExecutionCore(char[] parsedDeviceNames, float duration)
        {
            var devices = parsedDeviceNames
                .Select(name => (success: _controllerManager.TryGetDevice<BaseShutterDevice>(name, out var shutterDevice), name, shutterDevice))
                .Where(t => t.success)
                .ToDictionary(t => t.name, t => t.shutterDevice);
            // Retrieve controllers and group devices by controller
            var controllers = devices.Values
                .ToDictionary(device => device, device =>
                {
                    if (_controllerManager.TryGetDeviceController<BaseShutterController>(device.Name, out BaseShutterController controller))
                        return controller;
                    else
                        throw new Exception($"Unable to find controller for device: {device.Name}.");
                });

            var groupedDevicesByController = devices.Values
                .GroupBy(device => controllers[device])
                .ToDictionary(group => group.Key, group => group.ToList());

            var ChangeStateCommandLine = new List<Command>();

            foreach (var controllerGroup in groupedDevicesByController)
            {
                var controllerName = controllerGroup.Key.Name;
                var groupedDeviceNames = controllerGroup.Value.Select(device => device.Name).ToArray();


                var commandParameters = new ChangeShutterStateForIntervalParameters
                {
                    Duration = duration,
                };

                ChangeStateCommandLine.Add(new Command
                {
                    Action = CommandDefinitions.ChangeShutterStateOnInterval,
                    Await = true,
                    Parameters = commandParameters,
                    TargetController = controllerName,
                    TargetDevices = groupedDeviceNames,
                    EstimatedTime = commandParameters.Duration
                });
            }

            _commandManager.EnqueueCommandLine(ChangeStateCommandLine.ToArray());
            _commandManager.ExecuteCommandLine(ChangeStateCommandLine.ToArray()).GetAwaiter().GetResult();
        }


        private bool TryParseArguments(object?[] arguments, out char[] devNames, out float duration)
        {
            devNames = Array.Empty<char>();
            duration = 0f;

            if (arguments == null || arguments.Length == 0)
                return false;

            if (arguments[0] is not string firstArg)
                return false;

            devNames = firstArg.ToCharArray();

            if (!TryConvertToFloat(arguments[1], out duration))
                return false;

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
