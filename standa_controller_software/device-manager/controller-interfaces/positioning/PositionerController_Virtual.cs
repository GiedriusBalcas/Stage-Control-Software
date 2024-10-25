using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace standa_controller_software.device_manager.controller_interfaces.positioning
{
    public class PositionerController_Virtual : BasePositionerController
    {
        //----------Virtual axes private data---------------
        private class DeviceInformation
        {
            private float _acceleration = 1000;
            private float _deceleration = 1000;
            private float _speed = 100;
            public float Acceleration
            {
                get { return _acceleration; }
                set { _acceleration = Math.Min(value, this.MaxAcceleration); ; }
            }
            public float Deceleration
            {
                get { return _deceleration; }
                set { _deceleration = Math.Min(value, this.MaxDeceleration); ; }
            }
            public float Speed
            {
                get { return _speed; }
                set { _speed = Math.Min(value, this.MaxSpeed); ; }
            }
            public float CurrentPosition { get; set; } = 0;
            public float CurrentSpeed { get; set; } = 0;
            public float MaxAcceleration { get; set; } = 10000;
            public float MaxDeceleration { get; set; } = 10000;
            public float MaxSpeed { get; set; } = 1000;
            public uint MoveStatus { get; set; } = 0;
        }
        private ConcurrentDictionary<char, DeviceInformation> _deviceInfo = new ConcurrentDictionary<char, DeviceInformation>();

        //---------------------------------------------------

        public PositionerController_Virtual(string name) : base(name) { }
        
        public override void AddDevice(BaseDevice device)
        {
            base.AddDevice(device);

            if (device is BasePositionerDevice positioningDevice)
            {
                _deviceInfo.TryAdd(positioningDevice.Name, new DeviceInformation());
            }
        }

        protected override Task UpdateMoveSettings(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            var movementParams = command.Parameters as UpdateMovementSettingsParameters;
            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];

                float speedValue = movementParams.MovementSettingsInformation[device.Name].TargetSpeed;
                float accelValue = movementParams.MovementSettingsInformation[device.Name].TargetAcceleration;
                float decelValue = movementParams.MovementSettingsInformation[device.Name].TargetDeceleration;

                device.Speed = Math.Min(speedValue, device.MaxSpeed);
                device.Acceleration = Math.Min(accelValue, device.MaxAcceleration);
                device.Deceleration = Math.Min(decelValue, device.MaxDeceleration);
            }

            return Task.CompletedTask;
        }


        protected override Task WaitUntilStop(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();

            //for (int i = 0; i < devices.Length; i++)
            //{
            //    if (command.Parameters[i] != null && command.Parameters[i].Length != 0 )
            //    {
            //        float targetPosition = (float)(command.Parameters[i][0]);
            //        bool direction = (bool)(command.Parameters[i][1]);

            //        devices[i].CurrentPosition = targetPosition;
            //    }
                
            //}

            return Task.CompletedTask;
        }

        protected override Task WaitUntilStopPolar(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }

        public override Task UpdateStatesAsync(ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }

        public override BaseController GetCopy()
        {
            var controller = new PositionerController_Sim(Name);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value);
            }
            return controller;
        }

        protected override Task MoveAbsolute(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            var movementParams = command.Parameters as MoveAbsoluteParameters;

            if(movementParams.PositionerInfo.First().Value.WaitUntilPosition == null)
            {
                for (int i = 0; i < command.TargetDevices.Length; i++)
                {
                    var device = devices[i];
                    device.CurrentPosition = movementParams.PositionerInfo[device.Name].TargetPosition;
                    device.CurrentSpeed = 0f;
                }
            }
            else
            {
                for (int i = 0; i < command.TargetDevices.Length; i++)
                {
                    var device = devices[i];
                    var posInfo = movementParams.PositionerInfo[device.Name];
                    var direction = posInfo.Direction ? 1 : -1;
                    var targetSpeed = posInfo.TargetSpeed;
                    var initialSpeed = device.CurrentSpeed;
                    var totalDistance = Math.Abs(posInfo.TargetPosition - device.CurrentPosition);


                    if (Math.Sign(initialSpeed) != Math.Sign(targetSpeed))
                    {
                        var decelToZeroDistance = Math.Pow(initialSpeed, 2) / (2 * device.Deceleration);

                        // Adjust totalDistance by subtracting the distance used for decelerating to zero
                        totalDistance += (float)decelToZeroDistance;
                    }


                    // Phase 1: Calculate the acceleration distance (distance to reach the target speed from the initial speed)
                    double accelDistance = (Math.Pow(targetSpeed, 2) - Math.Pow(device.CurrentSpeed, 2)) / (2 * device.Acceleration);

                    // Phase 3: Calculate the deceleration distance (distance to decelerate from the target speed to 0)
                    double decelDistance = Math.Pow(targetSpeed, 2) / (2 * device.Deceleration);

                    // Check if the total distance is long enough to reach the target speed
                    if (accelDistance + decelDistance > totalDistance)
                    {
                        // No constant speed phase; adjust the target speed so it can only accelerate and decelerate within the total distance
                        double maxSpeedReached = Math.Sqrt((2 * device.Acceleration * totalDistance * device.Deceleration) / (device.Acceleration + device.Deceleration));

                        // Adjust targetSpeed to maxSpeedReached if total distance is not enough to reach the original target speed
                        targetSpeed = (float)maxSpeedReached;

                        // Recalculate the acceleration and deceleration distances for this adjusted target speed
                        accelDistance = (Math.Pow(targetSpeed, 2) - Math.Pow(device.CurrentSpeed, 2)) / (2 * device.Acceleration);
                        decelDistance = Math.Pow(targetSpeed, 2) / (2 * device.Deceleration);
                    }

                    // Phase 2: Calculate the constant speed phase distance (if applicable)
                    double constantSpeedDistance = totalDistance - accelDistance - decelDistance;
                    var endVelocity = 0f;
                    var distanceToWaitUntilPosition = Math.Abs((double)(posInfo.WaitUntilPosition - device.CurrentPosition));

                    if (distanceToWaitUntilPosition <= accelDistance)
                    {
                        // In the acceleration phase: use the equation v^2 = v0^2 + 2 * a * d
                        endVelocity = (float)Math.Sqrt(Math.Pow(device.CurrentSpeed, 2) + 2 * device.Acceleration * distanceToWaitUntilPosition);
                    }
                    else if(distanceToWaitUntilPosition > accelDistance + constantSpeedDistance)
                    {
                        // In the deceleration phase: calculate the position in the deceleration phase
                        double decelPosition = distanceToWaitUntilPosition - accelDistance - constantSpeedDistance;
                        endVelocity = (float)Math.Sqrt(Math.Pow(targetSpeed, 2) - 2 * device.Deceleration * decelPosition);
                    }
                    else
                    {
                        // In the constant speed phase
                        endVelocity = targetSpeed;
                    }

                    //device.CurrentSpeed = 0f;
                    device.CurrentSpeed = endVelocity * direction;
                    device.CurrentPosition = (float)posInfo.WaitUntilPosition;
                }

                //throw new NotImplementedException();
            }

            return Task.CompletedTask;
         }

        public override Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }
    }
}
