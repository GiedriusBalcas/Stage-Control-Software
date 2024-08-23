using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace standa_controller_software.device_manager.controller_interfaces.positioning
{
    public class VirtualPositionerController : BasePositionerController
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
        private ConcurrentDictionary<string, DeviceInformation> _deviceInfo = new ConcurrentDictionary<string, DeviceInformation>();
        // name | id
        private Dictionary<string, string> _deviceIDs;
        //---------------------------------------------------


        public VirtualPositionerController(string name) : base(name)
        {
        }

        public override void AddDevice(IDevice device)
        {
            base.AddDevice(device);

            if (device is IPositionerDevice positioningDevice)
            {
                _deviceInfo.TryAdd(positioningDevice.Name, new DeviceInformation()
                {
                    CurrentPosition = positioningDevice.CurrentPosition,
                    CurrentSpeed = positioningDevice.CurrentSpeed,
                    MaxAcceleration = positioningDevice.MaxAcceleration,
                    MaxDeceleration = positioningDevice.MaxDeceleration,
                    MaxSpeed = positioningDevice.MaxSpeed,
                    Speed = positioningDevice.Speed,
                    Acceleration = positioningDevice.Acceleration,
                    Deceleration = positioningDevice.Deceleration,
                });
            }
        }

        protected override async Task MoveAbsolute(Command command, IPositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore)
        {
            float targetPosition = (float)(command.Parameters[0]);

            
            var task = UpdateCommandMoveA(device.Name, targetPosition, cancellationToken);
            semaphore.Release();
            await task;
        }

        protected override async Task UpdateMoveSettings(Command command, IPositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore)
        {
            float speedValue = (float)(command.Parameters[0]);
            float accelValue = (float)(command.Parameters[1]);
            float decelValue = (float)(command.Parameters[2]);

            var task = UpdateMovementSettings(device.Name, speedValue, accelValue, decelValue, cancellationToken);
            semaphore.Release();
            await task;
        }

        private Task UpdateMovementSettings(string name, float speedValue, float accelValue, float decelValue, CancellationToken cancellationToken)
        {
            _deviceInfo[name].Speed = speedValue;
            _deviceInfo[name].Acceleration = accelValue;
            _deviceInfo[name].Deceleration = decelValue;

            return Task.CompletedTask;
        }

        protected override async Task WaitUntilStop(Command command, IPositionerDevice device, CancellationToken cancellationToken, SemaphoreSlim semaphore)
        {
            var task = UpdateWaitUntilStop(device.Name, cancellationToken);
            semaphore.Release();
            await task;
        }

        private async Task UpdateWaitUntilStop(string name, CancellationToken cancellationToken)
        {
            while (_deviceInfo[name].MoveStatus != 0)
            {
                //await Task.Delay(1, cancellationToken);
                await Task.Yield();
            }
        }

        //private async Task UpdateCommandMoveA(string name, float targetPosition, CancellationToken cancellationToken)
        //{
        //    int updateInterval = 1;
        //    var distanceToStop = () => 0.5f * _deviceInfo[name].Deceleration * Math.Pow((Math.Abs(_deviceInfo[name].CurrentSpeed) / _deviceInfo[name].Deceleration), 2);
        //    var directionToTarget = () => Math.Sign(targetPosition - _deviceInfo[name].CurrentPosition);
        //    var distanceToTarget = () => Math.Abs(targetPosition - _deviceInfo[name].CurrentPosition);
        //    var pointDifference = () => targetPosition - _deviceInfo[name].CurrentPosition;

        //    if (!float.IsFinite(targetPosition))
        //        throw new Exception("Non finite target position value provided");

        //    _deviceInfo[name].MoveStatus = 1;

        //    var movementPerInterval = () => (float)updateInterval / 1000 * _deviceInfo[name].CurrentSpeed;
        //    var accelerationPerInterval = () => (float)updateInterval / 1000 * _deviceInfo[name].Acceleration;
        //    var decelerationPerInterval = () => (float)updateInterval / 1000 * _deviceInfo[name].Deceleration;

        //    while (Math.Abs(pointDifference()) > Math.Abs(movementPerInterval()) || (Math.Abs(_deviceInfo[name].CurrentSpeed) > decelerationPerInterval()))
        //    {

        //        cancellationToken.ThrowIfCancellationRequested();

        //        float updatedSpeedValue;
        //        // check if moving to the target direction || not moving
        //        if (directionToTarget() == Math.Sign(_deviceInfo[name].CurrentSpeed) || _deviceInfo[name].CurrentSpeed == 0)
        //        {
        //            // check if we are in the range of stopping
        //            if (Math.Abs( pointDifference()) < distanceToStop())
        //            {

        //                if (Math.Abs(_deviceInfo[name].CurrentSpeed) < decelerationPerInterval())
        //                {
        //                    updatedSpeedValue = 0;
        //                    break;
        //                }
        //                // slowing down and approaching the target point.
        //                else
        //                    updatedSpeedValue = _deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(_deviceInfo[name].CurrentSpeed);
        //            }
        //            // we are good to go, no need to decelerate to a stop.
        //            // moving to the target direction.
        //            // we might still be going too fast though.
        //            else
        //            {
        //                // moving too fast than target speed. 
        //                if (Math.Abs(_deviceInfo[name].CurrentSpeed) > _deviceInfo[name].Speed)
        //                    updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(pointDifference())) < _deviceInfo[name].Speed
        //                        ? _deviceInfo[name].Speed * Math.Sign(pointDifference())
        //                        : _deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(pointDifference());

        //                // moving too slow than target speed.
        //                else
        //                    updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed + accelerationPerInterval() * Math.Sign(pointDifference())) > _deviceInfo[name].Speed
        //                        ? _deviceInfo[name].Speed * Math.Sign(pointDifference())
        //                        : _deviceInfo[name].CurrentSpeed + accelerationPerInterval() * Math.Sign(pointDifference());



        //            }
        //        }
        //        // moving to the wrong direction.
        //        else
        //        {
        //            updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed) - decelerationPerInterval() * Math.Sign(_deviceInfo[name].CurrentSpeed) > distanceToTarget()
        //                ? 0
        //                : _deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(_deviceInfo[name].CurrentSpeed);
        //        }

        //        _deviceInfo[name].CurrentSpeed = updatedSpeedValue;

        //        float updatedPositionValue = Math.Sign(pointDifference()) != Math.Sign(movementPerInterval())
        //            ? _deviceInfo[name].CurrentPosition + movementPerInterval()
        //            : distanceToTarget() < Math.Abs(movementPerInterval())
        //                ? targetPosition
        //                : _deviceInfo[name].CurrentPosition + movementPerInterval();

        //        _deviceInfo[name].CurrentPosition = float.IsFinite(updatedPositionValue) ? updatedPositionValue : 0;

        //        await Task.Delay(updateInterval, cancellationToken);
        //    }

        //    _deviceInfo[name].CurrentPosition = targetPosition;
        //    _deviceInfo[name].CurrentSpeed = 0;
        //    _deviceInfo[name].MoveStatus = 0;
        //}

        private async Task UpdateCommandMoveA(string name, float targetPosition, CancellationToken cancellationToken)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            var distanceToStop = () => 0.5f * _deviceInfo[name].Deceleration * Math.Pow((Math.Abs(_deviceInfo[name].CurrentSpeed) / _deviceInfo[name].Deceleration), 2);
            var directionToTarget = () => Math.Sign(targetPosition - _deviceInfo[name].CurrentPosition);
            var distanceToTarget = () => Math.Abs(targetPosition - _deviceInfo[name].CurrentPosition);
            var pointDifference = () => targetPosition - _deviceInfo[name].CurrentPosition;

            if (!float.IsFinite(targetPosition))
                throw new Exception("Non finite target position value provided");



            _deviceInfo[name].MoveStatus = 1;
            float movementPerInterval = 0 * _deviceInfo[name].CurrentSpeed;
            float accelerationPerInterval = 0 * _deviceInfo[name].Acceleration;
            float decelerationPerInterval =  0 * _deviceInfo[name].Deceleration;
            bool stopFlag = false;

            while ( Math.Abs( pointDifference() ) > Math.Abs( movementPerInterval ) || Math.Abs( _deviceInfo[name].CurrentSpeed ) > decelerationPerInterval )
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Calculate time elapsed since last update
                float timeElapsed = (float)stopwatch.Elapsed.TotalSeconds;
                stopwatch.Restart();

                movementPerInterval = timeElapsed * _deviceInfo[name].CurrentSpeed;
                accelerationPerInterval = timeElapsed * _deviceInfo[name].Acceleration;
                decelerationPerInterval = timeElapsed * _deviceInfo[name].Deceleration;

                float updatedSpeedValue;
                // check if moving to the target direction || not moving
                if (directionToTarget() == Math.Sign(_deviceInfo[name].CurrentSpeed) || _deviceInfo[name].CurrentSpeed == 0)
                {
                    var kakadistanceToStop = distanceToStop();
                    var kakadist = Math.Abs(pointDifference());


                    // check if we are in the range of stopping
                    if (Math.Abs(pointDifference()) < distanceToStop() || stopFlag)
                    {
                        stopFlag = true;
                        if (Math.Abs(_deviceInfo[name].CurrentSpeed) < decelerationPerInterval)
                        {
                            updatedSpeedValue = 0;
                            break;
                        }
                        // slowing down and approaching the target point.
                        else
                            updatedSpeedValue = _deviceInfo[name].CurrentSpeed - decelerationPerInterval * Math.Sign(_deviceInfo[name].CurrentSpeed);
                    }
                    // we are good to go, no need to decelerate to a stop.
                    // moving to the target direction.
                    // we might still be going too fast though.
                    else
                    {
                        // moving too fast than target speed. 
                        if (Math.Abs(_deviceInfo[name].CurrentSpeed) > _deviceInfo[name].Speed)
                            updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed - decelerationPerInterval * Math.Sign(pointDifference())) < _deviceInfo[name].Speed
                                ? _deviceInfo[name].Speed * Math.Sign(pointDifference())
                                : _deviceInfo[name].CurrentSpeed - decelerationPerInterval * Math.Sign(pointDifference());

                        // moving too slow than target speed.
                        else
                            updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed + accelerationPerInterval * Math.Sign(pointDifference())) > _deviceInfo[name].Speed
                                ? _deviceInfo[name].Speed * Math.Sign(pointDifference())
                                : _deviceInfo[name].CurrentSpeed + accelerationPerInterval * Math.Sign(pointDifference());



                    }
                }
                // moving to the wrong direction.
                else
                {
                    updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed) - decelerationPerInterval * Math.Sign(_deviceInfo[name].CurrentSpeed) > distanceToTarget()
                        ? 0
                        : _deviceInfo[name].CurrentSpeed - decelerationPerInterval * Math.Sign(_deviceInfo[name].CurrentSpeed);
                }

                _deviceInfo[name].CurrentSpeed = updatedSpeedValue;
                float updatedPositionValue;

                //float updatedPositionValue = Math.Sign(pointDifference()) != Math.Sign(movementPerInterval)
                //    ? _deviceInfo[name].CurrentPosition + movementPerInterval
                //    : distanceToTarget() < Math.Abs(movementPerInterval)
                //        ? targetPosition
                //        : _deviceInfo[name].CurrentPosition + movementPerInterval;


                if (Math.Sign(pointDifference()) != Math.Sign(movementPerInterval))
                    updatedPositionValue = _deviceInfo[name].CurrentPosition + movementPerInterval;
                else if (distanceToTarget() < Math.Abs(movementPerInterval))
                    updatedPositionValue = targetPosition;
                else
                    updatedPositionValue = _deviceInfo[name].CurrentPosition + movementPerInterval;

                //float updatedPositionValue = Math.Sign(pointDifference()) != Math.Sign(movementPerInterval)
                //    ? _deviceInfo[name].CurrentPosition + movementPerInterval
                //    : distanceToTarget() < Math.Abs(movementPerInterval)
                //        ? targetPosition
                //        : _deviceInfo[name].CurrentPosition + movementPerInterval;

                _deviceInfo[name].CurrentPosition = float.IsFinite(updatedPositionValue) ? updatedPositionValue : 0;


                await Task.Yield();  // Allow the task to yield control to other tasks.
            }

            _deviceInfo[name].CurrentPosition = targetPosition;
            _deviceInfo[name].CurrentSpeed = 0;
            _deviceInfo[name].MoveStatus = 0;
        }




        public override async Task UpdateStateAsync(ConcurrentQueue<string> log)
        {
            foreach (var positioner in Devices)
            {
                positioner.Value.CurrentPosition = _deviceInfo[positioner.Key].CurrentPosition;
                positioner.Value.CurrentSpeed = _deviceInfo[positioner.Key].CurrentSpeed;
                positioner.Value.Acceleration = _deviceInfo[positioner.Key].Acceleration;
                positioner.Value.Deceleration= _deviceInfo[positioner.Key].Deceleration;
                positioner.Value.Speed = _deviceInfo[positioner.Key].Speed;
                positioner.Value.MaxAcceleration = _deviceInfo[positioner.Key].MaxAcceleration;
                positioner.Value.MaxDeceleration = _deviceInfo[positioner.Key].MaxDeceleration;

                log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Updated state for device {positioner.Value.Name}, CurrentPos: {positioner.Value.CurrentPosition} CurrentSpeed: {positioner.Value.CurrentSpeed} Accel: {positioner.Value.Acceleration} Decel: {positioner.Value.Deceleration} Speed: {positioner.Value.Speed}  ");
            }
            //await Task.Delay(10);
        }

        public override IController GetCopy()
        {
            var controller = new VirtualPositionerController(Name);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value.GetCopy());
            }

            return controller;
        }

    }
}
