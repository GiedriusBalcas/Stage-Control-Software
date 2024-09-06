using standa_controller_software.command_manager;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public class PositionAndShutterController_Arduino : BaseController
    {
        private class MovementInformation
        {
            public float TargetPosition;
            public float TargetSpeed;
            public float AllocatedTime;
            public float Acceleration;
            public float Deceleration;
        }

        private Queue<MovementInformation> _movementInfoBuffer;
        public PositionAndShutterController_Arduino(string name) : base(name)
        {
            _methodMap[CommandDefinitionsLibrary.MoveAbsolute] = new MethodInformation()
            {
                MethodHandle = MoveAbsolute,
                AWaitAsync = AwaitQueueEnd,
                Quable = true,
                State = MethodState.Free,
            };
            //_methodMap["MoveAbsolute"] = MoveAbsolute;
            //_methodMap["MoveLineAbsolute"] = MoveLineAbsolute;
            //_methodMap["UpdateMoveSettings"] = UpdateMoveSettings;
            //_methodMap[CommandDefinitionsLibrary.ChangeShutterState.ToString()] = ChangeState;
            //_methodMap["WaitUntilStop"] = WaitUntilStop;
        }

        private async Task AwaitQueueEnd()
        {
            /// Either call the XIMC and get the buffer and movestatus
            /// or call the AK and get info from him.
            /// XIMC seems to handle interupts better.
        }

        private async Task ChangeState(Command command, List<BaseDevice> list, SemaphoreSlim slim, ConcurrentQueue<string> queue)
        {
            // if Change to ON, then we should prepare for a new movement block.
            // if change to OFF, then this marks the end of movement.

            // if shutter is currently off- maybe just retrhow this to the controller?
        }

        private Task UpdateMoveSettings(Command command, List<BaseDevice> list, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            /// MoveAbsoluteFunction wil only create this command if the settings need to be changed.
            /// await the current queue end if this is the case. 
            

            // save the parameters in memory.
            // call ximc.set_movement_settings() on execution;

            return Task.CompletedTask;
        }

        private async Task WaitUntilStop(Command command, List<BaseDevice> list, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            // save that wait until was called in memory.

            // switch(params).
            //   case: no params
            //      we will generate sync in pulse, when we get the sync_out pulse from last devices that moved.
            //   case: some params
            //      we will generate sync in pulse, when criteria is hit.
            //      either on time or position.
            //          if (position) - then once again this falls under PC.
            //          if(time) - this falls under how well the controller and devices are performing.

        }

        private async Task MoveAbsolute(Command command, List<BaseDevice> list, SemaphoreSlim slim, ConcurrentQueue<string> log)
        {
            // save that this was called.

            /// if the updateMocementSettings have changed, then we have to await the end of current queue.


            /// move command will bring:
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


            // command manager might keep track of what queable controller si currently working on its tasks. And await its end if new command has to go to a new controller.
            // might have a public property for all the methods? Let's not use the string for the key, but the DefinitionsHandle itself.
            // if the method has a property quable, then the command manager holds the current queing method and sends commands as long as the commands are for the same command. If it encounters a new controller command, it will await the previous queue to finish.

            /// MethodInfo()
            /// {
            ///     Func<> Method_handle,
            ///     bool Quable,
            ///     MethodState CurrectState
            /// }

            // remmember last state for movement initialization.
            // if it was launch = true

            // add movement info into queue to send to buffers?
            // then we have a problem knowing when the command line is actually finished.

        }

        public override void AddDevice(BaseDevice device)
        {
            throw new NotImplementedException();
        }
        public override void AddSlaveController(BaseController controller)
        {
            if (controller is ShutterController_Virtual shutterController)
            {
                SlaveControllers.Add(shutterController.Name, shutterController);
            }
            else if (controller is PositionerController_XIMC positionerController)
            {
                SlaveControllers.Add(positionerController.Name, positionerController);
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
                    await method.MethodHandle(command, devices, semaphore, log);
                else
                    _ = method.MethodHandle(command, devices, semaphore, log);
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
