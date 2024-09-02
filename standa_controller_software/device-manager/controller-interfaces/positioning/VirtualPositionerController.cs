using standa_controller_software.command_manager;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml.Linq;

namespace standa_controller_software.device_manager.controller_interfaces.positioning
{
    public partial class VirtualPositionerController : BasePositionerController
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
            public uint MoveStatus { get; set; } = 0;
            public string Name { get; set; }
            public float CurrentSpeed { get; set; } = 0;
            public float MaxAcceleration { get; set; } = 10000;
            public float MaxDeceleration { get; set; } = 10000;
            public float MaxSpeed { get; set; } = 1000;
        }
        private ConcurrentDictionary<char, DeviceInformation> _deviceInfo = new ConcurrentDictionary<char, DeviceInformation>();
        //---------------------------------------------------


        public VirtualPositionerController(string name) : base(name)
        {

        }
        public override void AddDevice(BaseDevice device)
        {
            base.AddDevice(device);

            if (device is BasePositionerDevice positioningDevice)
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

        protected override async Task MoveAbsolute(Command command, List<BasePositionerDevice> devices, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: move start");

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (deviceCancellationTokens[device.Name].IsCancellationRequested)
                {
                    deviceCancellationTokens[device.Name].Dispose();
                    deviceCancellationTokens[device.Name] = new CancellationTokenSource();
                }
                else
                {
                    deviceCancellationTokens[device.Name].Cancel();
                    deviceCancellationTokens[device.Name] = new CancellationTokenSource();
                }

                float targetPosition = (float)(command.Parameters[i][0]);

                _deviceInfo[device.Name].MoveStatus = 1;
                var task = UpdateCommandMoveA(device.Name, targetPosition, deviceCancellationTokens[device.Name].Token);
                tasks.Add(task);
            }
            //semaphore.Release();
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: move waiting");


            await Task.WhenAll(tasks);
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: move end");


        }

        protected override async Task UpdateMoveSettings(Command command, List<BasePositionerDevice> devices, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: upd start");


            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                float speedValue = (float)(command.Parameters[i][0]);
                float accelValue = (float)(command.Parameters[i][1]);
                float decelValue = (float)(command.Parameters[i][2]);

                var task = UpdateMovementSettings(device.Name, speedValue, accelValue, decelValue);
                await task;
            }
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: upd end");


            //semaphore.Release();
        }

        private Task UpdateMovementSettings(char name, float speedValue, float accelValue, float decelValue)
        {
            _deviceInfo[name].Speed = speedValue;
            _deviceInfo[name].Acceleration = accelValue;
            _deviceInfo[name].Deceleration = decelValue;

            return Task.CompletedTask;
        }

        protected override async Task WaitUntilStop(Command command, List<BasePositionerDevice> devices, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: wait start");


            var queuedItems = new List<Func<Task<bool>>>();
            
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (command.Parameters[i] == null || command.Parameters[i].Length == 0)
                {
                    queuedItems.Add
                        (
                            async () => 
                            {
                                await Task.Delay(1);
                                bool boolCheck = _deviceInfo[device.Name].MoveStatus != 0;
                                var currentPosition = _deviceInfo[device.Name].CurrentPosition;
                                device.CurrentPosition = currentPosition;
                                return boolCheck;
                            }
                        ) ;
                }
                else
                {
                    float targetPosition = (float)(command.Parameters[i][0]);
                    bool direction = (bool)(command.Parameters[i][1]);
                    queuedItems.Add
                        (
                            async () =>
                            {
                                var moveStatus = _deviceInfo[device.Name].MoveStatus != 0;
                                var currentPosition = _deviceInfo[device.Name].CurrentPosition;
                                device.CurrentPosition = currentPosition;


                                //// Testing Remove afterwards
                                var currentPositionX = _deviceInfo['x'].CurrentPosition;
                                Devices['x'].CurrentPosition = currentPositionX;
                                var currentPositionY = _deviceInfo['y'].CurrentPosition;
                                Devices['y'].CurrentPosition = currentPositionY;

                                var boolCheck = moveStatus && (direction ? currentPosition < targetPosition: currentPosition > targetPosition) ;
                                await Task.Delay(1);

                                return boolCheck;
                            }
                        );
                }
            }
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: wait waiting");


            try
            {
                while (queuedItems.Count > 0)
                {
                    var itemsToRemove = new List<Func<Task<bool>>>();

                    foreach (var queuedItem in queuedItems)
                    {
                        if (!(await queuedItem.Invoke()))
                        {
                            itemsToRemove.Add(queuedItem);
                        }
                    }

                    foreach (var item in itemsToRemove)
                    {
                        queuedItems.Remove(item);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Smth wrong with wait until in Virtual Positioner");
            }

            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: wait end");

        }



        protected override async Task WaitUntilStopPolar(Command command, List<BasePositionerDevice> devices, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: wait start");
            // only works for a device pair.


            try 
            { 
                if (command.Parameters[0] == null || command.Parameters[0].Length == 0)
                {
                    await Task.Delay(2);

                    var moveStatusX = _deviceInfo[devices[0].Name].MoveStatus != 0;
                    var moveStatusY = _deviceInfo[devices[1].Name].MoveStatus != 0;

                    bool boolCheck = moveStatusX && moveStatusY;
                }
                else
                {
                    await Task.Delay(2);

                    var targetAngle = (float)command.Parameters[0][0];
                    var direction = (bool)command.Parameters[0][1];
                    var centerX = (float)command.Parameters[0][2];
                    var centerY = (float)command.Parameters[1][2];
                    bool boolCheck = true;

                    var moveStatusX = _deviceInfo[devices[0].Name].MoveStatus != 0;
                    var moveStatusY = _deviceInfo[devices[1].Name].MoveStatus != 0;

                    var currentPositionX = _deviceInfo[devices[0].Name].CurrentPosition;
                    Devices[devices[0].Name].CurrentPosition = currentPositionX;
                    var currentPositionY = _deviceInfo[devices[1].Name].CurrentPosition;
                    Devices[devices[1].Name].CurrentPosition = currentPositionY;

                    double deltaX = currentPositionX - centerX;
                    double deltaY = currentPositionY - centerY;
                    var angleRadians = Math.Atan2((currentPositionY - centerY), (currentPositionX - centerX));

                    if (direction)
                    {
                        // target angle is ahead one revolution
                        if (angleRadians > targetAngle)
                        {
                            targetAngle += (float)Math.PI * 2;
                        }
                    } 
                    else
                    {
                        if (angleRadians < targetAngle)
                        {
                            targetAngle -= (float)Math.PI * 2;
                        }
                    }

                    var angleRadians_prev = angleRadians;

                    boolCheck = moveStatusX && moveStatusY && (direction ? angleRadians < targetAngle : angleRadians > targetAngle);


                    while (boolCheck)
                    {
                        await Task.Delay(1);

                        moveStatusX = _deviceInfo[devices[0].Name].MoveStatus != 0;
                        moveStatusY = _deviceInfo[devices[1].Name].MoveStatus != 0;

                        currentPositionX = _deviceInfo[devices[0].Name].CurrentPosition;
                        Devices[devices[0].Name].CurrentPosition = currentPositionX;

                        currentPositionY = _deviceInfo[devices[1].Name].CurrentPosition;
                        Devices[devices[1].Name].CurrentPosition = currentPositionY;

                        angleRadians = Math.Atan2((currentPositionY - centerY), (currentPositionX - centerX));
                        
                        if (direction)
                        {
                            // target angle is ahead one revolution
                            if (angleRadians < angleRadians_prev)
                            {
                                targetAngle -= (float)Math.PI * 2;
                            }
                        }
                        else
                        {
                            if (angleRadians > angleRadians_prev)
                            {
                                targetAngle += (float)Math.PI * 2;
                            }
                        }

                        angleRadians_prev = angleRadians;

                        boolCheck = moveStatusX && moveStatusY && (direction ? angleRadians < targetAngle : angleRadians > targetAngle);
                    }
                   
                }
            }
            catch
            {
                throw;
            }

            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: wait end");

        }

        private async Task UpdateCommandMoveA(char name, float targetPosition, CancellationToken cancellationToken)
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
            float decelerationPerInterval = 0 * _deviceInfo[name].Deceleration;
            bool stopFlag = false;

            while (Math.Abs(pointDifference()) > Math.Abs(movementPerInterval) || Math.Abs(_deviceInfo[name].CurrentSpeed) > decelerationPerInterval)
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
                cancellationToken.ThrowIfCancellationRequested();

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


                //await Task.Delay(1, cancellationToken);  // Allow the task to yield control to other tasks.
                await Task.Yield();
            }

            _deviceInfo[name].CurrentPosition = targetPosition;
            _deviceInfo[name].CurrentSpeed = 0;
            _deviceInfo[name].MoveStatus = 0;
        }

        public override async Task UpdateStatesAsync(ConcurrentQueue<string> log)
        {
            foreach (var positioner in Devices)
            {
                positioner.Value.CurrentPosition = _deviceInfo[positioner.Key].CurrentPosition;
                positioner.Value.CurrentSpeed = _deviceInfo[positioner.Key].CurrentSpeed;
                positioner.Value.Acceleration = _deviceInfo[positioner.Key].Acceleration;
                positioner.Value.Deceleration = _deviceInfo[positioner.Key].Deceleration;
                positioner.Value.Speed = _deviceInfo[positioner.Key].Speed;
                positioner.Value.MaxAcceleration = _deviceInfo[positioner.Key].MaxAcceleration;
                positioner.Value.MaxDeceleration = _deviceInfo[positioner.Key].MaxDeceleration;

                // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Updated state for device {positioner.Value.Name}, CurrentPos: {positioner.Value.CurrentPosition} CurrentSpeed: {positioner.Value.CurrentSpeed} Accel: {positioner.Value.Acceleration} Decel: {positioner.Value.Deceleration} Speed: {positioner.Value.Speed}  ");
            }
            //await Task.Delay(10);
        }

        public override BaseController GetCopy()
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
