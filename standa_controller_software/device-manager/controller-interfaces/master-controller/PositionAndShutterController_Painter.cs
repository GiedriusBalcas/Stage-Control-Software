using opentk_painter_library.render_objects;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public class PositionAndShutterController_Painter : BaseMasterController
    {
        private LineObjectCollection _lineObjectCollection;
        private ToolInformation _toolInformation;
        private readonly Vector4 _engagedColor = new Vector4(1, 0, 0, 1);
        private readonly Vector4 _leadColor = new Vector4(1f, 1f, 0.0f, 1f);
        private readonly Vector4 _disengagedColor = new Vector4(0.57f,0.69f,0.50f, 0.5f);

        public PositionAndShutterController_Painter(string name, LineObjectCollection lineObjectCollection, ToolInformation toolInformation) : base(name)
        {
            _lineObjectCollection = lineObjectCollection;
            _toolInformation = toolInformation;

            _multiControllerMethodMap[CommandDefinitions.MoveAbsolute] = new MultiControllerMethodInformation()
            {
                MethodHandle = MoveAbsolute,
                Quable = true,
                State = MethodState.Free,
            };
            
            _multiControllerMethodMap[CommandDefinitions.ChangeShutterState] = new MultiControllerMethodInformation()
            {
                MethodHandle = ChangeState,
                Quable = true,
                State = MethodState.Free,
            };

            //_methodMap["UpdateMoveSettings"] = UpdateMoveSettings;

            //_methodMap["WaitUntilStop"] = WaitUntilStop;
        }

        private async Task MoveAbsolute(Command[] commands, SemaphoreSlim semaphore, Dictionary<string, SemaphoreSlim> slaveSemaphors, ConcurrentQueue<string> log)
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
                    var ControllerParameters = command.Parameters as MoveAbsoluteParameters;
                    var newParameters = new MoveAbsoluteParameters();
                    var posInformations = new Dictionary<char, PositionerInfo>();
                    foreach (char deviceName in command.TargetDevices)
                    {
                        posInformations[deviceName] = new PositionerInfo
                        {
                            TargetPosition = ControllerParameters.PositionerInfo[deviceName].LeadInformation.LeadInEndPos,
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


                    if (SlaveControllers.TryGetValue(targetControllerName, out BaseController targetController))
                    {
                        await targetController.ExecuteCommandAsync(newCommand, slaveSemaphors[targetControllerName], log);
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


                    if (SlaveControllers.TryGetValue(targetControllerName, out BaseController targetController))
                    {
                        await targetController.ExecuteCommandAsync(newCommand, slaveSemaphors[targetControllerName], log);
                    }
                }

                var constSpeedEndPositions = _toolInformation.CalculateToolPositionUpdate();
                _lineObjectCollection.AddLine(startPositions, constSpeedEndPositions, isEngaged? _engagedColor : _disengagedColor);
                startPositions = constSpeedEndPositions;

                // go to end
                foreach (Command command in commands)
                {
                    var targetController = command.TargetController;
                    if (SlaveControllers.TryGetValue(targetController, out BaseController positionerController))
                    {
                        await positionerController.ExecuteCommandAsync(command, slaveSemaphors[targetController], log);
                    }
                }
                var endPositions = _toolInformation.CalculateToolPositionUpdate();
                _lineObjectCollection.AddLine(startPositions, endPositions, isLeadOut? _leadColor : _disengagedColor);

            }
            else
            {
                foreach (Command command in commands)
                {
                    var targetController = command.TargetController;
                    if (SlaveControllers.TryGetValue(targetController, out BaseController positionerController))
                    {
                        await positionerController.ExecuteCommandAsync(command, slaveSemaphors[targetController], log);
                    }
                }
                var endPositions = _toolInformation.CalculateToolPositionUpdate();
                _lineObjectCollection.AddLine(startPositions, endPositions, isEngaged ? _engagedColor : _disengagedColor);
            }
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
            }
            else if (controller is BasePositionerController positionerController)
            {
                SlaveControllers.Add(positionerController.Name, positionerController);
                //positionerController.OnSyncOut += OnSyncOutReveived;
            }
        }

        private void OnSyncOutReveived(string deviceName)
        {

        }

        public override Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
        {
            throw new NotImplementedException();
        }
        private async Task ChangeState(Command[] commands, SemaphoreSlim semaphore, Dictionary<string, SemaphoreSlim> slaveSemaphors, ConcurrentQueue<string> log)
        {

        }
        public override async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {

            List<BaseDevice> devices = new List<BaseDevice>();

            foreach (var deviceName in command.TargetDevices)
            {
                Dictionary<char, BaseDevice> slaveDevices = new Dictionary<char, BaseDevice>();
                foreach (var slaveController in SlaveControllers)
                {
                    slaveController.Value.GetDevices().ForEach(slaveDevice => slaveDevices.Add(slaveDevice.Name, slaveDevice));
                }

                if (slaveDevices.TryGetValue(deviceName, out BaseDevice device))
                {
                    devices.Add(device);
                }
                else
                {
                    // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Device {deviceName} not found in controller {command.TargetController}");
                }
            }

            if (_methodMap.TryGetValue(command.Action, out var method))
            {
                if (command.Await)
                    await method.MethodHandle(command, semaphore, log);
                else
                    _ = method.MethodHandle(command, semaphore, log);
            }
            else
            {
                throw new InvalidOperationException("Invalid action");
            }
        }

        public override BaseController GetCopy()
        {
            var controllerCopy = new PositionAndShutterController_Virtual(this.Name);
            foreach (var slaveController in SlaveControllers)
            {
                controllerCopy.AddSlaveController(slaveController.Value.GetCopy(), SlaveControllersLocks[slaveController.Key]);
            }

            return controllerCopy;
        }

        public override List<BaseDevice> GetDevices()
        {
            return new List<BaseDevice>();
        }

        public override Task UpdateStatesAsync(ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }


        public override Task AwaitQueuedItems(SemaphoreSlim semaphore, Dictionary<string, SemaphoreSlim> slaveSemaphors, ConcurrentQueue<string> log)
        {
            throw new NotImplementedException();
        }

        public override Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }
    }
}
