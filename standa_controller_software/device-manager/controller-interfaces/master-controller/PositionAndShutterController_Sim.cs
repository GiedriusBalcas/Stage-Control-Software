using standa_controller_software.command_manager;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public class PositionAndShutterController_Sim : BaseController
    {
        /// <summary>
        /// This is a simulation of a real master controller for positioning and shutter synchronization
        /// Master Controller work-flow:
        ///     gather all the QuableCommands before execution
        ///     Execute the collected QuableCommands
        ///     Await till the slave controllers have executed the commands fully
        /// </summary>
        private class InternalSyncExecuter
        {
            private Queue<ExecutionInformation> _buffer = new Queue<ExecutionInformation>();
            public Dictionary<char, Action> _positionerSyncInMap = new Dictionary<char, Action>();
            public Action<bool> _shutterChangeState;
            public Dictionary<char, bool> syncOutFlags = new Dictionary<char, bool>();
            public Dictionary<char, bool> sendSyncInTo = new Dictionary<char, bool>();
            Stopwatch millis = new Stopwatch();
            private QueueState _queueState = QueueState.Waiting;
            public enum QueueState
            {
                Running,
                Waiting
            }
            public InternalSyncExecuter()
            {
                

                syncOutFlags = new Dictionary<char, bool>();
            }
            public void AddBufferItem(ExecutionInformation executionInformation)
            {
                _buffer.Enqueue(executionInformation);
            }
            private void ResetSyncOutFlags()
            {
                foreach (var key in syncOutFlags.Keys.ToList())
                {
                    syncOutFlags[key] = true;
                }
            }
            public void GotSyncOut(char deviceName)
            {
                syncOutFlags[deviceName] = true;
                // check if all falgs are true
                // if so launch sync in to all target devices

                /// How to know the target devices?
                /// I must look up from the next item in queue?
                ///     will I always have the next item?
                CheckAndRelaunchSyncIn();

                //return Task.CompletedTask;
            }
            private void CheckAndRelaunchSyncIn()
            {
                if (syncOutFlags.All(kvp => kvp.Value == true))
                {
                    var devicesToRethrow = sendSyncInTo.Where(kvp => kvp.Value == true)
                                  .Select(kvp => kvp.Key)
                                  .ToArray();

                    SendSyncIn(devicesToRethrow);
                }
            }

            public async Task ExecutionQueue()
            {
                if (_queueState == QueueState.Running)
                    return;

                millis.Restart();

                while (_buffer.Count > 0)
                {
                    var executionInformation = _buffer.Dequeue();

                    ResetSyncOutFlags();
                    if (executionInformation.Launch)
                    {
                        SendSyncIn(executionInformation.Devices);
                    }

                    //sendSyncInTo
                    if (_buffer.Count > 0)
                    {
                        var nextBufferItem = _buffer.Peek();
                        nextBufferItem.Devices.Select(device => sendSyncInTo[device] = true);
                    }
                    else
                    {
                        foreach (var key in sendSyncInTo.Keys.ToList())
                        {
                            sendSyncInTo[key] = false;
                        }
                    }

                    // send sync_in to all targetted devices.
                    var currentTime = millis.ElapsedMilliseconds;

                    bool shutterOnPending = true;
                    bool shutterOffPending = true;
                    while (true)
                    {
                        if (executionInformation.Shutter)
                        {
                            if (shutterOnPending && millis.ElapsedMilliseconds > currentTime + executionInformation.Shutter_delay_on)
                            {
                                shutterOnPending = false;
                                //turn on
                                _shutterChangeState.Invoke(true);
                            }
                            if (shutterOffPending && millis.ElapsedMilliseconds > currentTime + executionInformation.Shutter_delay_off)
                            {
                                shutterOffPending = false;
                                //turn off
                                _shutterChangeState.Invoke(false);

                            }
                        }

                        if(executionInformation.Rethrow != 0f && millis.ElapsedMilliseconds > currentTime + executionInformation.Rethrow)
                        {
                            var devicesToRethrow = sendSyncInTo.Where(kvp => kvp.Value == true)
                                  .Select(kvp => kvp.Key)
                                  .ToArray();
                            
                            SendSyncIn(devicesToRethrow);

                            break;
                        }
                        await Task.Delay(5);
                    }

                }
            }

            private void SendSyncIn(char[] devices)
            {
                Action[] calls = new Action[devices.Length];
                int indexer = 0;
                foreach (var device in devices)
                {
                    calls[indexer] = _positionerSyncInMap[device];
                    syncOutFlags[device] = false;
                    indexer++;
                }

                foreach (var call in calls)
                {
                    call.Invoke();
                }
            }

            public int CheckFreeItemSpace()
            {
                int maxSize = 10;
                int currentSize = _buffer.Count;

                return maxSize - currentSize;
            }
        }



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
        public PositionAndShutterController_Sim(string name) : base(name)
        {
            _methodMap[CommandDefinitions.MoveAbsolute] = new MethodInformation()
            {
                MethodHandle = MoveAbsolute,
                AWaitAsync = (SemaphoreSlim semaphore, ConcurrentQueue<string> log) => AwaitQueuedItems(semaphore, log),
                Quable = true,
                State = MethodState.Free,
            };
            _methodMap[CommandDefinitions.UpdateMoveSettings] = new MethodInformation()
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
        }

        public override async Task AwaitQueuedItems(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            /// Because I dont have the slave controllers after the constructor, I'll just create a new _syncExecuter here each time.


            /// For now, lets use this method to also launch the collected items.
            /// lauch the collected QueuedCommands

            //await ProcessQueue(semaphore, log);

            await Task.Delay(10);

            /// await private queue items, until count == 0

            /// Either call the XIMC and get the buffer and movestatus
            /// or call the AK and get info from him.
            /// XIMC seems to handle interupts better.

            /// when all queue items have been called check the positioner buffers

            /// then await the movement_status
        }


        private async Task ProcessQueue(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            while (_buffer.Count > 0)
            {
                var movementInformation = _buffer.Dequeue();
                var PosInfoControllerGroups = movementInformation.PositionerInfoGroups;
                var execInfo = movementInformation.ExecutionInformation;

                
                // Check if the buffers arent full.

                int minFreeItemCount = GetMinFreeBufferItemCount();

                // if they are full, let's launch them.
                if (minFreeItemCount < 1)
                {
                    _syncExecuter.ExecutionQueue();
                }

                while(minFreeItemCount > 0)
                {
                    minFreeItemCount = GetMinFreeBufferItemCount();
                    await Task.Delay(100);
                }
                // wait until some buffer from both is freed up.

                // Sending add_sync_in_action


                foreach (var (controllerName, posInfoList) in PosInfoControllerGroups)
                {
                    var deviceNamesCurrent = posInfoList.Devices;
                    object[][] parameters = new object[posInfoList.Devices.Length][];

                    for (int i = 0; i < posInfoList.Devices.Length; i++)
                    {
                        parameters[i] = [posInfoList.TargetPositions, posInfoList.AllocatedTimes];
                    }

                    var command = new Command()
                    {
                        Action = CommandDefinitions.AddSyncInAction,
                        TargetController = controllerName,
                        TargetDevices = posInfoList.Devices,
                        Parameters = parameters,
                        Await = true,
                    };
                    await SlaveControllers[controllerName].ExecuteCommandAsync(command, semaphore, log);
                }

                // Sending the sync_execution_info

                _syncExecuter.AddBufferItem(execInfo);

            }
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

        private async Task ChangeState(Command command, List<BaseDevice> list, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            // if Change to ON, then we should prepare for a new movement block.
            // if change to OFF, then this marks the end of movement.

            // if shutter is currently off- maybe just retrhow this to the controller?

            await AwaitQueuedItems(slim, log);

            if (SlaveControllers.TryGetValue(command.TargetController, out var slaveController))
            {
                await slaveController.ExecuteCommandAsync(command, slim, log);
            }
            else
                throw new Exception($"Slave controller {command.TargetController} was not found.");
        }

        private async Task UpdateMoveSettings(Command command, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            /// MoveAbsoluteFunction wil only create this command if the settings need to be changed.
            /// await the current queue end if this is the case. 

            await AwaitQueuedItems(slim, log);

            if (SlaveControllers.TryGetValue(command.TargetController, out var slaveController))
            {
                await slaveController.ExecuteCommandAsync(command, slim, log);
            }
            else
                throw new Exception($"Slave controller {command.TargetController} was not found.");

            // save the parameters in memory ( why? allocated time must be provided by the moveAFunction).
            // call ximc.set_movement_settings() on execution;

        }

        private Task WaitUntilStop(Command command, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }

            private Task MoveAbsolute(Command command, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            // save that this was called.

            /// if the updateMocementSettings have changed, then we have to await the end of current queue.


            /// move command will bring:
            ///      traj:
            ///         isLine
            ///      shutter:
            ///          target state
            ///          delay on (not the intrinsic one)
            ///          delay off
            ///      positioner:
            ///          target position
            ///          target speed
            ///          allocated time
            ///          acceleration
            ///          deceleration

            /// send to ximc:
            ///      set_sync_in_action( target position, allocated time )
            ///
            /// send to AK:
            ///      set_state
            ///      (
            ///      launch = false,
            ///          rethrow after = 100 ms,         // AK always tracks when the last sync_in_launch happened
            ///      shutter = true,
            ///          delay_shutter_on = 1ms,
            ///          delay_shutter_off = 1ms,
            ///      launch = false?
            ///
            ///      rethrowing after 3 sync_outs is default after command.
            ///      unless rethrow is not equal 0.
            ///      )


            /// command manager might keep track of what queable controller si currently working on its tasks. And await its end if new command has to go to a new controller.
            /// might have a public property for all the methods? Let's not use the string for the key, but the DefinitionsHandle itself.
            /// if the method has a property quable, then the command manager holds the current queing method and sends commands as long as the commands are for the same command. If it encounters a new controller command, it will await the previous queue to finish.

            /// MethodInfo()
            /// {
            ///     Func<> Method_handle,
            ///     bool Quable,
            ///     MethodState CurrectState
            /// }

            // remmember last state for movement initialization.
            // if it was launch = true
            List<char> devNames = ['x', 'y', 'z'];
            List<float> targetPositions = [10f, 10f, 20f];
            List<float> allocatedTimes = [100f, 100f, 100f];

            var posInfo = new PositionerInfo
            {
                Devices = devNames.ToArray(),
                AllocatedTimes = allocatedTimes.ToArray(),
                TargetPositions = targetPositions.ToArray(),
            };



            Dictionary<string, PositionerInfo> PosInfoControllerGroups = new Dictionary<string, PositionerInfo>();




            var deviceNames = posInfo.Devices;

            // Iterate over all devices in the PositionerInfo
            for (int i = 0; i < deviceNames.Length; i++)
            {
                char deviceName = deviceNames[i];

                // Find which controller owns the device
                foreach (var controllerEntry in SlaveControllers)
                {
                    var controllerName = controllerEntry.Key;
                    var controller = controllerEntry.Value;

                    // Check if the controller has the device
                    if (controller.GetDevices().Any(device => device.Name == deviceName))
                    {
                        // If the controller owns the device, add it to the group
                        if (!PosInfoControllerGroups.ContainsKey(controllerName))
                        {
                            // Create a new PositionerInfo for the controller if not already in the dictionary
                            PosInfoControllerGroups[controllerName] = new PositionerInfo()
                            {
                                Devices = new List<char>().ToArray(),
                                TargetPositions = new List<float>().ToArray(),
                                AllocatedTimes = new List<float>().ToArray()
                            };
                        }

                        // Add device info to the PositionerInfo for this controller
                        var controllerPosInfo = PosInfoControllerGroups[controllerName];

                        // Convert arrays to lists for easy appending
                        var devicesList = controllerPosInfo.Devices.ToList();
                        var targetPositionsList = controllerPosInfo.TargetPositions.ToList();
                        var allocatedTimesList = controllerPosInfo.AllocatedTimes.ToList();

                        devicesList.Add(deviceName);
                        targetPositionsList.Add(posInfo.TargetPositions[i]);
                        allocatedTimesList.Add(posInfo.AllocatedTimes[i]);

                        // Convert lists back to arrays and store them back in the PositionerInfo
                        PosInfoControllerGroups[controllerName] = new PositionerInfo()
                        {
                            Devices = devicesList.ToArray(),
                            TargetPositions = targetPositionsList.ToArray(),
                            AllocatedTimes = allocatedTimesList.ToArray()
                        };

                        break; // No need to check other controllers if we found the right one
                    }
                }
            }


            var moveInfo = new MovementInformation()
            {
                PositionerInfoGroups = PosInfoControllerGroups,
                ExecutionInformation = new ExecutionInformation()
                {
                    Devices = deviceNames.ToArray(),
                    Launch = true,
                    Rethrow = 0f,
                    Shutter = true,
                    Shutter_delay_off = 0f,
                    Shutter_delay_on = 0f,
                }
            };
            _buffer.Enqueue(moveInfo);

            return Task.CompletedTask;
        }

        public override void AddDevice(BaseDevice device)
        {
            throw new NotImplementedException();
        }
        public override void AddSlaveController(BaseController controller)
        {
            if (controller is ShutterController_Sim shutterController)
            {
                SlaveControllers.Add(shutterController.Name, shutterController);
            }
            else if (controller is PositionerController_Sim positionerController)
            {
                SlaveControllers.Add(positionerController.Name, positionerController);
                
                
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
                controllerCopy.AddSlaveController(slaveController.Value.GetCopy());
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

       
    }
}
