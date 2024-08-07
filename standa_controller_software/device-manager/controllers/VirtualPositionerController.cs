using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace standa_controller_software.device_manager.controllers
{
    public class VirtualPositionerController : IPositionerController
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
        private Dictionary<string, DeviceInformation> _deviceInfo = new Dictionary<string, DeviceInformation>();
        // name | id
        private Dictionary<string, string> _deviceIDs;
        //---------------------------------------------------


        public string Name { get; set; }
        public Dictionary<string, IPositionerDevice> Devices { get; private set; } = new Dictionary<string, IPositionerDevice>();

        private ConcurrentDictionary<string, CancellationTokenSource> deviceCancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();

        private Dictionary<string, Func<Command, IPositionerDevice, CancellationToken, Task>> methodMap = new Dictionary<string, Func<Command, IPositionerDevice, CancellationToken, Task>>();



        public VirtualPositionerController(string name)
        {
            Name = name;
            methodMap["MoveAbsolute"] = MoveAbsoluteCall;
            methodMap["UpdateStates"] = UpdateStatesCall;
        }

        public void AddDevice(IDevice device)
        {
            if (device is IPositionerDevice positioningDevice)
            {
                Devices.Add(positioningDevice.Name, positioningDevice);
                _deviceInfo.Add(positioningDevice.Name, new DeviceInformation());
            }
            else
                throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");
        }

        public async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            if (Devices.TryGetValue(command.TargetDevice, out IPositionerDevice device))
            {
                log.Enqueue($"{DateTime.Now}: Executing {command.Action} command on device {device.Name}");

                var tokenSource = new CancellationTokenSource();

                if (deviceCancellationTokens.ContainsKey(device.Name))
                {
                    deviceCancellationTokens[device.Name].Cancel();
                    deviceCancellationTokens[device.Name] = tokenSource;
                }
                else
                {
                    deviceCancellationTokens.TryAdd(device.Name, tokenSource);
                }

                if (methodMap.TryGetValue(command.Action, out var method))
                {
                    if(command.Await)
                        await method(command, device, tokenSource.Token);
                    else
                        method(command, device, tokenSource.Token);
                }
                else
                {
                    throw new InvalidOperationException("Invalid action");
                }

                //if (command.Action == "MoveAbsolute")
                //{
                //    //int targetPosition = int.Parse((string)command.Parameters[0]);
                //    float targetPosition = (float)(command.Parameters[0]);

                //    await UpdateCommandMoveA(device.Name, targetPosition, tokenSource.Token);
                //}

                log.Enqueue($"{DateTime.Now}: Completed {command.Action} command on device {device.Name}, New Position: {device.Position}");
            }
            else
            {
                log.Enqueue($"{DateTime.Now}: Device {command.TargetDevice} not found in controller {command.TargetController}");
            }
        }

        private async Task MoveAbsoluteCall(Command command, IPositionerDevice device, CancellationToken cancellationToken)
        {
            float targetPosition = (float)(command.Parameters[0]);

            await UpdateCommandMoveA(device.Name, targetPosition, cancellationToken);
        }
        private async Task UpdateStatesCall(Command command, IPositionerDevice device, CancellationToken cancellationToken)
        {
            foreach (var positioner in Devices)
            {
                positioner.Value.Position = _deviceInfo[positioner.Key].CurrentPosition;
                positioner.Value.Speed = _deviceInfo[positioner.Key].CurrentSpeed;
            }
            await Task.Delay(50);
        }
        private async Task UpdateCommandMoveA(string name, float targetPosition, CancellationToken cancellationToken)
        {
            int updateInterval = 2;
            var distanceToStop = () => 0.5f * _deviceInfo[name].Deceleration * Math.Pow((Math.Abs(_deviceInfo[name].CurrentSpeed) / _deviceInfo[name].Deceleration), 2);
            var directionToTarget = () => Math.Sign(targetPosition - _deviceInfo[name].CurrentPosition);
            var distanceToTarget = () => Math.Abs(targetPosition - _deviceInfo[name].CurrentPosition);
            var pointDifference = () => targetPosition - _deviceInfo[name].CurrentPosition;

            //Mimicking the MTF8 transloation stage.
            //Returns when velocity is zero and reached position.

            if (!float.IsFinite(targetPosition))
                throw new Exception("Non finite target position value provided");

            _deviceInfo[name].MoveStatus = 1;

            
            var movementPerInterval = () => (float)updateInterval / 1000 * _deviceInfo[name].CurrentSpeed; // Simulate movement speed, adjust as needed.                                                               //movementPerInterval = Math.Min(movementPerInterval, 10);
            var accelerationPerInterval = () => (float)updateInterval / 1000 * _deviceInfo[name].Acceleration; // Simulate movement speed, adjust as needed.                                                               //movementPerInterval = Math.Min(movementPerInterval, 10);
            var decelerationPerInterval = () => (float)updateInterval / 1000 * _deviceInfo[name].Deceleration; // Simulate movement speed, adjust as needed.                                                               //movementPerInterval = Math.Min(movementPerInterval, 10);


            // Axis should move until it reaches the targetpoint with velocity of 0. UpdatePositionAsync is responsible for quiting earlier.
            //0.1um is given as a rounding error accumulator.
            while (pointDifference() > movementPerInterval()
                || (Math.Abs(_deviceInfo[name].CurrentSpeed) > decelerationPerInterval()))
            {
                cancellationToken.ThrowIfCancellationRequested();

                float updatedSpeedValue;

                // if axis is moving towards the target point. Or is at a stop.
                if (directionToTarget() == Math.Sign(_deviceInfo[name].CurrentSpeed) || _deviceInfo[name].CurrentSpeed == 0)
                {

                    // Check for whether the distance to the target is less than one needed for a complete stop.
                    if (pointDifference() - movementPerInterval() < distanceToStop())
                    {
                        if (Math.Abs(_deviceInfo[name].CurrentSpeed)< decelerationPerInterval())
                            updatedSpeedValue = 0;
                        else
                            updatedSpeedValue = _deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(_deviceInfo[name].CurrentSpeed);
                    }
                    else
                    {
                        // Let's check whether it needs to accelerate or decelerate.
                        if (Math.Abs(_deviceInfo[name].CurrentSpeed) < _deviceInfo[name].Speed)
                        {
                            if (Math.Abs( _deviceInfo[name].Speed - Math.Abs(_deviceInfo[name].CurrentSpeed) ) < accelerationPerInterval())
                                updatedSpeedValue = _deviceInfo[name].Speed * Math.Sign(_deviceInfo[name].Speed);
                            else
                                updatedSpeedValue = _deviceInfo[name].CurrentSpeed + accelerationPerInterval() * Math.Sign(_deviceInfo[name].Speed);
                        }
                        else
                        {
                            if (Math.Abs(_deviceInfo[name].Speed - Math.Abs(_deviceInfo[name].CurrentSpeed)) < decelerationPerInterval())
                                updatedSpeedValue = _deviceInfo[name].Speed * Math.Sign(_deviceInfo[name].Speed);
                            else
                                updatedSpeedValue = _deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(_deviceInfo[name].Speed);
                        }
                    }
                }

                // else if the axis is moving to the opposite direction than target point.
                // will always be the case of deceleration.
                // will have to cover the case of overshooting the target point though in position update.

                // to-do: after changing direction axis should accelerate. Right now I just look at the deceleration for the updated speed value.
                else
                {
                    // Lets avoid the jiggle around the target point right here.
                    if (Math.Abs( _deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(_deviceInfo[name].CurrentSpeed) ) > distanceToTarget())
                        break;
                    
                    if ( decelerationPerInterval() - Math.Abs(_deviceInfo[name].CurrentSpeed) > _deviceInfo[name].Speed)
                        updatedSpeedValue = _deviceInfo[name].Speed * directionToTarget();
                    else
                        updatedSpeedValue = _deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(_deviceInfo[name].CurrentSpeed);
                }
                
                _deviceInfo[name].CurrentSpeed = updatedSpeedValue;


                // Need to check wherther acceleration per step will be more than decelerationPerStep po pajudejimo.



                float updatedPositionValue;
                //should account for when moving towards opposite direction.
                // Eg.-> movmentPerInterval <0, target = 100, currentpos = 0.

                if (Math.Sign(pointDifference()) != Math.Sign(movementPerInterval()))
                {
                    updatedPositionValue = _deviceInfo[name].CurrentPosition + movementPerInterval();
                }
                else
                {
                    if (distanceToTarget() < Math.Abs(movementPerInterval()))
                    {
                        break;
                    }
                    else
                    {
                        updatedPositionValue = _deviceInfo[name].CurrentPosition + movementPerInterval();
                    }
                }

                _deviceInfo[name].CurrentPosition = float.IsFinite(updatedPositionValue) ? updatedPositionValue : 0;

                await Task.Delay(updateInterval, cancellationToken);
            }

            _deviceInfo[name].CurrentPosition = targetPosition;
            _deviceInfo[name].CurrentSpeed = 0;
            _deviceInfo[name].MoveStatus = 0;
        }

        public async Task UpdateStateAsync(ConcurrentQueue<string> log)
        {
            foreach (var positioner in Devices)
            {
                positioner.Value.Position = _deviceInfo[positioner.Key].CurrentPosition;
                positioner.Value.Speed = _deviceInfo[positioner.Key].CurrentSpeed;
                log.Enqueue($"{DateTime.Now}: Updated state for device {positioner.Value.Name}, Position: {positioner.Value.Position}");

            }
            await Task.Delay(50);
        }

        public List<IDevice> GetDevices()
        {
            return Devices.Values.Cast<IDevice>().ToList();
        }
    }
}
