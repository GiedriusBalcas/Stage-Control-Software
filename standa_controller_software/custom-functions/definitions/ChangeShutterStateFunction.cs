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
    public class ChangeShutterStateFunction : CustomFunction
    {
        public string Message { get; set; } = "";
        private readonly CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;
        private readonly JumpAbsoluteFunction _jumpAbsoluteFunction;

        public ChangeShutterStateFunction(CommandManager commandManager, ControllerManager controllerManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;
        }


        public override object? Execute(params object[] args)
        {
            if (!TryParseArguments(args, out var parsedDeviceNames, out bool wantedState))
                throw new ArgumentException("Argument parsing was unsuccessful. Wrong types.");


            ExecutionCore(parsedDeviceNames, wantedState);

            return null;
        }

        public void ExecutionCore(char[] parsedDeviceNames, bool wantedState)
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


                var commandParameters = new ChangeShutterStateParameters
                {
                    State = wantedState
                };

                ChangeStateCommandLine.Add(new Command
                {
                    Action = CommandDefinitions.ChangeShutterState,
                    Await = true,
                    Parameters = commandParameters,
                    TargetController = controllerName,
                    TargetDevices = groupedDeviceNames
                });
            }

            _commandManager.EnqueueCommandLine(ChangeStateCommandLine.ToArray());
            _commandManager.TryExecuteCommandLine(ChangeStateCommandLine.ToArray()).GetAwaiter().GetResult();
        }


        private bool TryParseArguments(object?[] arguments, out char[] devNames, out bool wantedState)
        {
            devNames = Array.Empty<char>();
            wantedState = false;

            if (arguments == null || arguments.Length == 0)
                return false;

            if (arguments[0] is not string firstArg)
                return false;

            devNames = firstArg.ToCharArray();

            if (arguments[1] is not bool wantedStateBool)
                return false;
            wantedState = wantedStateBool;

            return true;
        }

    }
}
