using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.devices;
using System.Collections.Concurrent;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public partial class PositionAndShutterController_Sim : BaseMasterController
    {
        private struct PositionerInfo
        {
            public char[] Devices;
            public float[] TargetPositions;
            public float[] AllocatedTimes;
        }
        private struct ExecutionInformation
        {
            public char[] Devices;
            public bool Launch;
            public float Rethrow;
            public bool Shutter;
            public float Shutter_delay_on;
            public float Shutter_delay_off;
        }
        private struct MovementInformation
        {
            public Dictionary<string, PositionerInfo> PositionerInfoGroups;
            public ExecutionInformation ExecutionInformation;
        }


        private InternalSyncExecuter _syncExecuter;

        private Queue<MovementInformation> _buffer;
        private bool _launchPending = true;
        private ConcurrentQueue<string>? _log;

        public PositionAndShutterController_Sim(string name) : base(name)
        {
            _methodMap_multiControntroller[CommandDefinitions.ChangeShutterState] = new MultiControllerMethodInformation()
            {
                MethodHandle = ChangeState,
                Quable = true,
                State = MethodState.Free,
            }; 
            _methodMap_multiControntroller[CommandDefinitions.MoveAbsolute] = new MultiControllerMethodInformation()
            {
                MethodHandle = MoveAbsolute,
                Quable = true,
                State = MethodState.Free,
            };
            _methodMap_multiControntroller[CommandDefinitions.UpdateMoveSettings] = new MultiControllerMethodInformation()
            {
                MethodHandle = UpdateMoveSettings,
                Quable = true,
                State = MethodState.Free,
            };
            _methodMap[CommandDefinitions.WaitUntilStop] = new MethodInformation()
            {
                MethodHandle = WaitUntilStop,
                Quable = true,
                State = MethodState.Free,
            };

            _buffer = new Queue<MovementInformation>();
            _syncExecuter = new InternalSyncExecuter();
            _syncExecuter.SendMessage += GotMessageFromSyncExecuter;
            IsQuable = true;
        }


        private void GotMessageFromSyncExecuter(string Message)
        {
            if (Message == "0x01") // Arduino signaled buffer space is available
            {
                SendCommandIfAvailable();
            }
            else if (Message == "0x02") // Arduino signaled buffer is empty
            {
                Console.WriteLine("Arduino buffer is empty. All commands executed.");
            }
        }

        private async Task SendCommandIfAvailable()
        {
            if (_buffer.Count > 0)
            {
                var movementInformation = _buffer.Dequeue();
                var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                var execInfo = movementInformation.ExecutionInformation;

                await SendBufferItemToControllers(PosInfoControllerGroups, execInfo, SlaveControllersLocks, _log);
            }

        }

        private async Task ProcessQueue(Dictionary<string, SemaphoreSlim> semaphores, ConcurrentQueue<string> log)
        {
            log.Enqueue($"-------------------Starting to flushing buffer to controllers, movement buffer count: {_buffer.Count}.----------------");

            await FillControllerBuffers(semaphores, log);

            _launchPending = true;
            await _syncExecuter.ExecuteQueue(log);

        }

        private async Task FillControllerBuffers(Dictionary<string, SemaphoreSlim> semaphores, ConcurrentQueue<string> log)
        {
            int minFreeItemCount = GetMinFreeBufferItemCount();
            int bufferCount = _buffer.Count;

            for (int i = 0; i < Math.Min(minFreeItemCount, bufferCount); i++)
            {
                var movementInformation = _buffer.Dequeue();
                var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                var execInfo = movementInformation.ExecutionInformation;

                await SendBufferItemToControllers(PosInfoControllerGroups, execInfo, semaphores, log);
            }
        }

        private async Task SendBufferItemToControllers(Dictionary<string, PositionerInfo>? PosInfoControllerGroups, ExecutionInformation execInfo, Dictionary<string, SemaphoreSlim> semaphores, ConcurrentQueue<string> log)
        {
            foreach (var (controllerName, posInfoList) in PosInfoControllerGroups)
            {
                var deviceNamesCurrent = posInfoList.Devices;
                //object[][] parameters = new object[posInfoList.Devices.Length][];

                //for (int i = 0; i < posInfoList.Devices.Length; i++)
                //{
                //    parameters[i] = [posInfoList.TargetPositions, posInfoList.AllocatedTimes];
                //}
                var parameters = new AddSyncInActionParameters
                {
                    MovementInformation = new Dictionary<char, PositionTimePair>()
                };

                foreach (var deviceName in deviceNamesCurrent)
                {
                    var index = Array.IndexOf(deviceNamesCurrent, deviceName);
                    parameters.MovementInformation[deviceName] = new PositionTimePair
                    {
                        Position = posInfoList.TargetPositions[index],
                        Time = posInfoList.AllocatedTimes[index]
                    };
                }


                var command = new Command()
                {
                    Action = CommandDefinitions.AddSyncInAction,
                    TargetController = controllerName,
                    TargetDevices = posInfoList.Devices,
                    Parameters = parameters,
                    Await = true,
                };
                await SlaveControllers[controllerName].ExecuteCommandAsync(command, semaphores[controllerName], log);
            }

            // Sending the sync_execution_info

            _syncExecuter.AddBufferItem(execInfo);
        }


        private int GetMinFreeBufferItemCount()
        {
            int minFreeItemCount = int.MaxValue;
            foreach (var (controllerName, controller) in SlaveControllers)
            {
                if (controller is PositionerController_Sim positionerController)
                {
                    minFreeItemCount = Math.Min(minFreeItemCount, positionerController.CheckBufferFreeSpace());
                }
            }
            _syncExecuter.CheckFreeItemSpace();
            minFreeItemCount = Math.Min(minFreeItemCount, _syncExecuter.CheckFreeItemSpace());
            return minFreeItemCount;
        }

        private async Task ChangeState(Command[] commands, Dictionary<string, SemaphoreSlim> semaphors, ConcurrentQueue<string> log)
        {
            // if Change to ON, then we should prepare for a new movement block.
            // if change to OFF, then this marks the end of movement.

            // if shutter is currently off- maybe just retrhow this to the controller?

            //await AwaitQueuedItems(slim, log);

            //if (SlaveControllers.TryGetValue(command.TargetController, out var slaveController))
            //{
            //    await slaveController.ExecuteCommandAsync(command, slim, log);
            //}
            //else
            //    throw new Exception($"Slave controller {command.TargetController} was not found.");
        }

        private async Task UpdateMoveSettings(Command[] commands, Dictionary<string, SemaphoreSlim> semaphors, ConcurrentQueue<string> log)
        {
            /// MoveAbsoluteFunction wil only create this command if the settings need to be changed.
            /// await the current queue end if this is the case. 

            _launchPending = true;
            await AwaitQueuedItems(semaphors, log);

            foreach (Command command in commands)
            {
                if (SlaveControllers.TryGetValue(command.TargetController, out var slaveController))
                {
                    await slaveController.ExecuteCommandAsync(command, semaphors[command.TargetController], log);
                }
                else
                    throw new Exception($"Slave controller {command.TargetController} was not found.");

            }

            // save the parameters in memory ( why? allocated time must be provided by the moveAFunction).
            // call ximc.set_movement_settings() on execution;

        }

        private Task WaitUntilStop(Command command, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }

        private Task MoveAbsolute(Command[] commands, Dictionary<string, SemaphoreSlim> semaphors, ConcurrentQueue<string> log)
        {
            var commandParametersFromFirstCommand = commands.FirstOrDefault().Parameters as MoveAbsoluteParameters ?? throw new Exception("Unable to retrive MoveAbsolute parameters.");

            bool isLeadInUsed = commandParametersFromFirstCommand.IsLeadInUsed;
            bool isLeadOutUsed = commandParametersFromFirstCommand.IsLeadOutUsed;



            Dictionary<string, PositionerInfo> posInfoGroups = new Dictionary<string, PositionerInfo>();

            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                var commandParameters = command.Parameters as MoveAbsoluteParameters ?? throw new Exception("Unable to retrive MoveAbsolute parameters.");

                posInfoGroups[command.TargetController] = new PositionerInfo
                {
                    Devices = command.TargetDevices,
                    TargetPositions = command.TargetDevices.Select(deviceName => commandParameters.PositionerInfo[deviceName].TargetPosition).ToArray(),
                    AllocatedTimes = command.TargetDevices.Select(deviceName => commandParameters.AllocatedTime).ToArray(),
                };
            }
            //}

            var executionParameters = new ExecutionInformation()
            {
                Devices = commands.SelectMany(comm => comm.TargetDevices).ToArray(),
                Launch = _launchPending,
                Rethrow = 0f,
                Shutter = commandParametersFromFirstCommand.IsShutterUsed,
                Shutter_delay_off = commandParametersFromFirstCommand.IsShutterUsed ? commandParametersFromFirstCommand.ShutterInfo.DelayOff : 0f,
                Shutter_delay_on = commandParametersFromFirstCommand.IsShutterUsed ? commandParametersFromFirstCommand.ShutterInfo.DelayOn : 0f,
            };

            var moveInfo = new MovementInformation()
            {
                PositionerInfoGroups = posInfoGroups,
                ExecutionInformation = executionParameters
            };
            _buffer.Enqueue(moveInfo);

            _launchPending = false;


            return Task.CompletedTask;
        }

        public override void AddDevice(BaseDevice device)
        {
            throw new NotImplementedException();
        }
        public override void AddSlaveController(BaseController controller, SemaphoreSlim controllerLock)
        {
            if (controller is ShutterController_Sim shutterController)
            {
                SlaveControllers.Add(shutterController.Name, shutterController);
                SlaveControllersLocks.Add(shutterController.Name, controllerLock);
                _syncExecuter._shutterChangeState = (bool wantedState) =>
                {
                    _ = shutterController.ChangeStatePublic(wantedState);
                };
            }
            else if (controller is PositionerController_Sim positionerController)
            {
                SlaveControllers.Add(positionerController.Name, positionerController);
                SlaveControllersLocks.Add(positionerController.Name, controllerLock);

                foreach (var device in positionerController.GetDevices())
                {
                    char deviceName = device.Name;
                    _syncExecuter._positionerSyncInMap[deviceName] = () => positionerController.InvokeSynIn(deviceName);
                    positionerController.OnSyncOut += _syncExecuter.GotSyncOut;
                }
            }
        }


        public override Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
        {
            throw new NotImplementedException();
        }




        public override async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {

            throw new Exception("Slave controller shouldnt call this method, sir.");
            log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Executing {command.Action} command on device {string.Join(' ', command.TargetDevices)}");

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

        public override async Task ExecuteSlaveCommandsAsync(Command[] commands, Dictionary<string, SemaphoreSlim> semaphores, ConcurrentQueue<string> log)
        {
            _log = log;
            log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Executing {string.Join(' ', commands.Select(command => command.Action).ToArray())} command on device {string.Join(' ', commands.SelectMany(command => command.TargetDevices).ToArray())},  {string.Join(' ', commands.Select(command => command.Parameters.ToString()))}  ");

            var command = commands.First();
            if (_methodMap_multiControntroller.TryGetValue(commands.First().Action, out var method))
            {
                if (command.Await)
                    await method.MethodHandle(commands, semaphores, log);
                else
                    _ = method.MethodHandle(commands, semaphores, log);
            }
            else
            {
                throw new InvalidOperationException("Invalid action");
            }
        }

        public override async Task AwaitQueuedItems(Dictionary<string, SemaphoreSlim> semaphores, ConcurrentQueue<string> log)
        {
            /// Because I dont have the slave controllers after the constructor, I'll just create a new _syncExecuter here each time.
            foreach (var (controllerName, semaphore) in semaphores)
            {
                if (semaphore.CurrentCount == 0)
                    semaphore.Release();
            }

            /// For now, lets use this method to also launch the collected items.
            /// lauch the collected QueuedCommands

            await ProcessQueue(semaphores, log);

            await Task.Delay(10);

            /// await private queue items, until count == 0

            /// Either call the XIMC and get the buffer and movestatus
            /// or call the AK and get info from him.
            /// XIMC seems to handle interupts better.

            /// when all queue items have been called check the positioner buffers

            /// then await the movement_status
        }

        public override Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            _buffer.Clear();
            _syncExecuter.Stop();
            _launchPending = true;

            return Task.CompletedTask;
        }
    }
}
