using opentk_painter_library.render_objects;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.devices;
using System.Collections.Concurrent;
using System.Numerics;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public class PositionAndShutterController_Painter : BaseMasterPositionerAndShutterController
    {
        private LineObjectCollection _lineObjectCollection;
        private ToolInformation _toolInformation;
        private readonly Vector4 _engagedColor = new Vector4(1, 0, 0, 1);
        private readonly Vector4 _leadColor = new Vector4(1f, 1f, 0.0f, 1f);
        private readonly Vector4 _disengagedColor = new Vector4(0.57f,0.69f,0.50f, 0.5f);

        public PositionAndShutterController_Painter(string name, ConcurrentQueue<string> log, LineObjectCollection lineObjectCollection, ToolInformation toolInformation) : base(name, log)
        {
            _lineObjectCollection = lineObjectCollection;
            _toolInformation = toolInformation;
        }

        public override List<BaseDevice> GetDevices()
        {
            return new List<BaseDevice>();
        }
        public override void AddDevice(BaseDevice device)
        {
            throw new NotImplementedException();
        }
        public override void AddSlaveController(BaseController controller, SemaphoreSlim controllerLock)
        {
            if (controller is ShutterController_Virtual shutterController)
            {
                SlaveControllers.Add(shutterController.Name, shutterController);
                SlaveControllersLocks.Add(shutterController.Name, controllerLock);
            }
            else if (controller is BasePositionerController positionerController)
            {
                SlaveControllers.Add(positionerController.Name, positionerController);
                SlaveControllersLocks.Add(positionerController.Name, controllerLock);
            }
        }
        public override Task ForceStop()
        {
            return Task.CompletedTask;

        }

        public override Task AwaitQueuedItems(SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }

        protected override Task ConnectDevice(Command command, SemaphoreSlim semaphore)
        {
            throw new NotImplementedException();
        }
        protected override async Task MoveAbsolute(Command[] commands, SemaphoreSlim semaphore)
        {
            var startPositions = _toolInformation.CalculateToolPositionUpdate();

            var parameters = commands.Select(command => command.Parameters as MoveAbsoluteParameters).ToList();

            bool isLeadIn = parameters.Any(parameter => parameter.IsLeadInUsed);
            bool isLeadOut = parameters.Any(parameter => parameter.IsLeadOutUsed);
            var isEngaged = _toolInformation.IsOn || parameters.Any(controllerParameter => controllerParameter.IsShutterUsed);

            if (isLeadIn)
            {
                // go to the intermediate point
                foreach (Command command in commands)
                {
                    var targetControllerName = command.TargetController;
                    if (command.Parameters is MoveAbsoluteParameters controllerParameters)
                    {
                        var newParameters = new MoveAbsoluteParameters();
                        var posInformations = new Dictionary<char, PositionerInfo>();
                        foreach (char deviceName in command.TargetDevices)
                        {
                            posInformations[deviceName] = new PositionerInfo
                            {
                                TargetPosition = controllerParameters.PositionerInfo[deviceName].LeadInformation.LeadInEndPos,
                                TargetSpeed = controllerParameters.PositionerInfo[deviceName].TargetSpeed
                            };
                        }
                        newParameters.PositionerInfo = posInformations;
                        Command newCommand = new Command
                        {
                            Action = CommandDefinitions.MoveAbsolute,
                            TargetController = targetControllerName,
                            TargetDevices = command.TargetDevices,
                            Parameters = newParameters,
                        };

                        await ExecuteSlaveCommand(newCommand);
                    }
                }

                var leadInEndPositions = _toolInformation.CalculateToolPositionUpdate();
                _lineObjectCollection.AddLine(startPositions, leadInEndPositions, _leadColor);
                startPositions = leadInEndPositions;
            }
            if (isLeadOut)
            {
                // go to the intermediate point
                foreach (Command command in commands)
                {
                    var targetControllerName = command.TargetController;
                    var ControllerParameters = command.Parameters as MoveAbsoluteParameters;
                    var newParameters = new MoveAbsoluteParameters();
                    var posInformations = new Dictionary<char, PositionerInfo>();
                    foreach (char deviceName in command.TargetDevices)
                    {
                        posInformations[deviceName] = new PositionerInfo
                        {
                            TargetPosition = ControllerParameters.PositionerInfo[deviceName].LeadInformation.LeadOutStartPos,
                            TargetSpeed = ControllerParameters.PositionerInfo[deviceName].TargetSpeed
                        };
                    }
                    newParameters.PositionerInfo = posInformations;
                    Command newCommand = new Command
                    {
                        Action = CommandDefinitions.MoveAbsolute,
                        TargetController = targetControllerName,
                        TargetDevices = command.TargetDevices,
                        Parameters = newParameters,
                    };

                    await ExecuteSlaveCommand(newCommand);
                }

                var constSpeedEndPositions = _toolInformation.CalculateToolPositionUpdate();
                _lineObjectCollection.AddLine(startPositions, constSpeedEndPositions, isEngaged? _engagedColor : _disengagedColor);
                startPositions = constSpeedEndPositions;

                // go to end
                foreach (Command command in commands)
                {
                    var targetController = command.TargetController;
                    await ExecuteSlaveCommand(command);
                }
                var endPositions = _toolInformation.CalculateToolPositionUpdate();
                _lineObjectCollection.AddLine(startPositions, endPositions, isLeadOut? _leadColor : _disengagedColor);

            }
            else
            {
                foreach (Command command in commands)
                {
                    var targetController = command.TargetController;
                    await ExecuteSlaveCommand(command);
                }
                var endPositions = _toolInformation.CalculateToolPositionUpdate();
                _lineObjectCollection.AddLine(startPositions, endPositions, isEngaged ? _engagedColor : _disengagedColor);
            }
        }
        protected override Task UpdateMoveSettings(Command[] commands, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected override Task ChangeState(Command[] commands, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected override Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected override Task Stop(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        
    }
}
