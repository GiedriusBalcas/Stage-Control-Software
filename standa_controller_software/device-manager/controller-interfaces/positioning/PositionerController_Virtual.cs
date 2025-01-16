using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
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

        public PositionerController_Virtual(string name, ILoggerFactory loggerFactory) : base(name, loggerFactory)
        {
            _logger = _loggerFactory.CreateLogger<PositionerController_Virtual>();

        }

        public override void AddDevice(BaseDevice device)
        {
            base.AddDevice(device);

            if (device is BasePositionerDevice positioningDevice)
            {
                _deviceInfo.TryAdd(positioningDevice.Name, new DeviceInformation());
            }
        }
        public override Task ForceStop()
        {
            return Task.CompletedTask;

        }
        protected override Task Home(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected override Task UpdateMoveSettings(Command command, SemaphoreSlim semaphore)
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
        protected override Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected override Task<bool> IsDeviceStationary(BasePositionerDevice device)
        {
            var result = true;
            return Task.FromResult(result);
        }
        protected override Task MoveAbsolute(Command command, SemaphoreSlim semaphore)
        {
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            var movementParams = command.Parameters as MoveAbsoluteParameters;

            if (movementParams.WaitUntilTime != null && movementParams.PositionerInfo.First().Value.WaitUntilPosition == null && false)
            {
                float waitUntilTime = movementParams.WaitUntilTime.Value;

                for (int i = 0; i < command.TargetDevices.Length; i++)
                {
                    var device = devices[i];
                    var posInfo = movementParams.PositionerInfo[device.Name];

                    // Movement parameters
                    float v0 = device.CurrentSpeed;
                    float vt = posInfo.TargetSpeed;
                    float a = device.Acceleration;
                    float d = device.Deceleration;
                    float x0 = device.CurrentPosition;
                    float x_target = posInfo.TargetPosition;
                    float direction = posInfo.Direction ? 1 : -1;

                    float totalDistance = Math.Abs(x_target - x0);

                    // Adjust initial speed to movement direction
                    float v0_dir = v0 * direction;

                    // Keep accelerations and speeds positive
                    a = Math.Abs(a);
                    d = Math.Abs(d);
                    vt = Math.Abs(vt);

                    // If initial speed is in the opposite direction, decelerate to zero first
                    float s_stop = 0f;
                    float t_stop = 0f;
                    if (Math.Sign(v0_dir) != Math.Sign(vt))
                    {
                        t_stop = Math.Abs(v0_dir) / d;
                        s_stop = v0_dir * t_stop - 0.5f * d * t_stop * t_stop;

                        totalDistance += Math.Abs(s_stop);
                        v0_dir = 0;
                    }

                    // Phase 1: Acceleration
                    float t_accel = (vt - v0_dir) / a;
                    float s_accel = v0_dir * t_accel + 0.5f * a * t_accel * t_accel;

                    // Phase 3: Deceleration
                    float t_decel = vt / d;
                    float s_decel = vt * t_decel - 0.5f * d * t_decel * t_decel;

                    // Total distance required for acceleration and deceleration
                    float s_total_required = s_accel + s_decel;

                    // Check if there's a constant speed phase
                    float s_const = 0f;
                    float t_const = 0f;
                    if (s_total_required + Math.Abs(s_stop) < totalDistance)
                    {
                        s_const = totalDistance - s_total_required - Math.Abs(s_stop);
                        t_const = s_const / vt;
                    }
                    else
                    {
                        // Adjust target speed
                        float discriminant = 2 * a * d * (totalDistance - Math.Abs(s_stop)) + d * v0_dir * v0_dir;
                        float vt_candidate_squared = discriminant / (a + d);
                        if (vt_candidate_squared < 0)
                            vt_candidate_squared = 0;

                        vt = (float)Math.Sqrt(vt_candidate_squared);

                        // Recalculate times and distances
                        t_accel = (vt - v0_dir) / a;
                        s_accel = v0_dir * t_accel + 0.5f * a * t_accel * t_accel;

                        t_decel = vt / d;
                        s_decel = vt * t_decel - 0.5f * d * t_decel * t_decel;

                        s_total_required = s_accel + s_decel;
                        s_const = 0f;
                        t_const = 0f;
                    }

                    // Total time
                    float t_total = t_stop + t_accel + t_const + t_decel;

                    // Determine the phase at waitUntilTime
                    float t_elapsed = waitUntilTime;

                    float position = x0;
                    float speed = v0;

                    if (t_elapsed <= t_stop)
                    {
                        // Decelerating to zero speed
                        float t = t_elapsed;
                        position = x0 + (v0_dir * t - 0.5f * d * t * t) * direction;
                        speed = (v0_dir - d * t) * direction;
                    }
                    else if (t_elapsed <= t_stop + t_accel)
                    {
                        // Acceleration phase
                        float t = t_elapsed - t_stop;
                        position = x0 + (s_stop + v0_dir * t + 0.5f * a * t * t) * direction;
                        speed = (v0_dir + a * t) * direction;
                    }
                    else if (t_elapsed <= t_stop + t_accel + t_const)
                    {
                        // Constant speed phase
                        float t = t_elapsed - t_stop - t_accel;
                        position = x0 + (s_stop + s_accel + vt * t) * direction;
                        speed = vt * direction;
                    }
                    else if (t_elapsed <= t_total)
                    {
                        // Deceleration phase
                        float t = t_elapsed - t_stop - t_accel - t_const;
                        position = x0 + (s_stop + s_accel + s_const + vt * t - 0.5f * d * t * t) * direction;
                        speed = (vt - d * t) * direction;
                    }
                    else
                    {
                        // Movement has finished
                        position = x_target;
                        speed = 0f;
                    }

                    device.CurrentPosition = position;
                    device.CurrentSpeed = speed;
                }
            }
            else if (!movementParams.PositionerInfo.Any(posInfo => posInfo.Value.WaitUntilPosition is null))
            {
                try
                {
                    for (int i = 0; i < command.TargetDevices.Length; i++)
                    {
                        var device = devices[i];
                        var posInfo = movementParams.PositionerInfo[device.Name];
                        var direction = posInfo.Direction ? 1 : -1;
                        var targetSpeed = Math.Abs(posInfo.TargetSpeed);
                        var initialSpeed = Math.Abs(device.CurrentSpeed);
                        var totalDistance = Math.Abs(posInfo.TargetPosition - device.CurrentPosition);

                        // Determine if we need to accelerate or decelerate
                        bool isAccelerating = targetSpeed > initialSpeed;

                        double accelDecelDistance = 0;

                        if (isAccelerating)
                        {
                            // Acceleration phase
                            accelDecelDistance = (Math.Pow(targetSpeed, 2) - Math.Pow(initialSpeed, 2)) / (2 * device.Acceleration);
                        }
                        else
                        {
                            // Deceleration phase
                            accelDecelDistance = (Math.Pow(initialSpeed, 2) - Math.Pow(targetSpeed, 2)) / (2 * device.Deceleration);
                        }

                        // Check if the total distance is sufficient
                        if (accelDecelDistance > totalDistance)
                        {
                            // Cannot reach targetSpeed, adjust it
                            if (isAccelerating)
                            {
                                targetSpeed = (float)Math.Sqrt(Math.Pow(initialSpeed, 2) + 2 * device.Acceleration * totalDistance);
                            }
                            else
                            {
                                targetSpeed = (float)Math.Sqrt(Math.Pow(initialSpeed, 2) - 2 * device.Deceleration * totalDistance);
                            }
                            accelDecelDistance = totalDistance;
                        }

                        float distanceToWaitUntilPosition = (float)Math.Abs((double)(posInfo.WaitUntilPosition - device.CurrentPosition));

                        float endVelocity = 0f;

                        if (distanceToWaitUntilPosition <= accelDecelDistance)
                        {
                            // We are still in acceleration/deceleration phase
                            if (isAccelerating)
                            {
                                // Acceleration phase
                                endVelocity = (float)Math.Sqrt(Math.Pow(initialSpeed, 2) + 2 * device.Acceleration * distanceToWaitUntilPosition);
                            }
                            else
                            {
                                // Deceleration phase
                                endVelocity = (float)Math.Sqrt(Math.Pow(initialSpeed, 2) - 2 * device.Deceleration * distanceToWaitUntilPosition);
                            }
                        }
                        else
                        {
                            // We have reached targetSpeed
                            endVelocity = (float)targetSpeed;
                        }

                        // Apply direction to endVelocity
                        endVelocity *= direction;

                        device.CurrentSpeed = endVelocity;
                        device.CurrentPosition = (float)posInfo.WaitUntilPosition;
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            else
            {
                for (int i = 0; i < command.TargetDevices.Length; i++)
                {
                    var device = devices[i];
                    device.CurrentPosition = movementParams.PositionerInfo[device.Name].TargetPosition;
                    device.CurrentSpeed = 0f;
                }
            }

            return Task.CompletedTask;
         }
        protected override Task Stop(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected override void ConnectDevice_implementation(BaseDevice device)
        {
            return;
        }


        protected override Task AddSyncInAction(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected override Task<int> GetBufferFreeSpace(Command command, SemaphoreSlim semaphore)
        {
            return Task.Run(() => 
            {
                int number = 10; 
                return number;
            });
        }

    }
}
