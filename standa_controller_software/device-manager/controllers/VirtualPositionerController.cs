using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
            //methodMap["UpdateStates"] = UpdateStatesCall;
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
                log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Executing {command.Action} command on device {device.Name}");

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
                    if (command.Await)
                        await method(command, device, tokenSource.Token);
                    else
                        _ = method(command, device, tokenSource.Token); // Start method without awaiting
                }
                else
                {
                    throw new InvalidOperationException("Invalid action");
                }

                log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Completed {command.Action} command on device {device.Name}, New Position: {device.Position}");
            }
            else
            {
                log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Device {command.TargetDevice} not found in controller {command.TargetController}");
            }
        }

        private async Task MoveAbsoluteCall(Command command, IPositionerDevice device, CancellationToken cancellationToken)
        {
            float targetPosition = (float)(command.Parameters[0]);

            await UpdateCommandMoveA(device.Name, targetPosition, cancellationToken);
        }

        private async Task UpdateCommandMoveA(string name, float targetPosition, CancellationToken cancellationToken)
        {
            int updateInterval = 10;
            var distanceToStop = () => 0.5f * _deviceInfo[name].Deceleration * Math.Pow((Math.Abs(_deviceInfo[name].CurrentSpeed) / _deviceInfo[name].Deceleration), 2);
            var directionToTarget = () => Math.Sign(targetPosition - _deviceInfo[name].CurrentPosition);
            var distanceToTarget = () => Math.Abs(targetPosition - _deviceInfo[name].CurrentPosition);
            var pointDifference = () => targetPosition - _deviceInfo[name].CurrentPosition;

            if (!float.IsFinite(targetPosition))
                throw new Exception("Non finite target position value provided");

            _deviceInfo[name].MoveStatus = 1;

            var movementPerInterval = () => (float)updateInterval / 1000 * _deviceInfo[name].CurrentSpeed;
            var accelerationPerInterval = () => (float)updateInterval / 1000 * _deviceInfo[name].Acceleration;
            var decelerationPerInterval = () => (float)updateInterval / 1000 * _deviceInfo[name].Deceleration;

            var kpointDifference = pointDifference();
            var kmovementperinterval = movementPerInterval();
            var kdecelerationPerInterval = decelerationPerInterval();

            var kkk = 0;

            while (Math.Abs(pointDifference()) > Math.Abs(movementPerInterval()) || (Math.Abs(_deviceInfo[name].CurrentSpeed) > decelerationPerInterval()))
            {

                kpointDifference = pointDifference();
                kmovementperinterval = movementPerInterval();
                kdecelerationPerInterval = decelerationPerInterval();

                cancellationToken.ThrowIfCancellationRequested();

                float updatedSpeedValue;


                // check if moving to the target direction || not moving
                if (directionToTarget() == Math.Sign(_deviceInfo[name].CurrentSpeed) || _deviceInfo[name].CurrentSpeed == 0)
                {
                    // 
                    if (Math.Abs( pointDifference() - movementPerInterval() ) < distanceToStop())
                    {
                        updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed) < decelerationPerInterval()
                            ? 0
                            : _deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(_deviceInfo[name].CurrentSpeed);
                    }
                    else
                    {
                        if (Math.Abs(_deviceInfo[name].CurrentSpeed) < _deviceInfo[name].Speed)
                            updatedSpeedValue = _deviceInfo[name].CurrentSpeed + accelerationPerInterval() * Math.Sign(pointDifference());
                        else
                            updatedSpeedValue = _deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(pointDifference());
                    }
                }
                else
                {
                    updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed) - decelerationPerInterval() * Math.Sign(_deviceInfo[name].CurrentSpeed) > distanceToTarget()
                        ? 0
                        : _deviceInfo[name].CurrentSpeed - decelerationPerInterval() * Math.Sign(_deviceInfo[name].CurrentSpeed);
                }

                _deviceInfo[name].CurrentSpeed = updatedSpeedValue;

                float updatedPositionValue = Math.Sign(pointDifference()) != Math.Sign(movementPerInterval())
                    ? _deviceInfo[name].CurrentPosition + movementPerInterval()
                    : distanceToTarget() < Math.Abs(movementPerInterval())
                        ? targetPosition
                        : _deviceInfo[name].CurrentPosition + movementPerInterval();

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
                log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Updated state for device {positioner.Value.Name}, Position: {positioner.Value.Position}");
            }
            await Task.Delay(10);
        }

        public List<IDevice> GetDevices()
        {
            return Devices.Values.Cast<IDevice>().ToList();
        }
    }
}
