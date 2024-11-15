using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.command_manager.command_parameter_library.Common;
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
using ximcWrapper;

namespace standa_controller_software.device_manager.controller_interfaces.positioning
{
    public partial class PositionerController_Sim : BasePositionerController 
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
                set { _acceleration = value; }
            }
            public float Deceleration
            {
                get { return _deceleration; }
                set { _deceleration = value; }
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
            public float MaxSpeed { get; set; } = 1000;
        }
        private ConcurrentDictionary<char, DeviceInformation> _deviceInfo = new ConcurrentDictionary<char, DeviceInformation>();
        private struct SyncInAction
        {
            public float TargetPosition;
            public float AllocatedTime;
        }
        private Dictionary<char, Queue<SyncInAction>> _buffer = new Dictionary<char, Queue<SyncInAction>>();
        //---------------------------------------------------

        public event Action<char> OnSyncOut;
        public event  Action<char> OnSyncIn;

        public PositionerController_Sim(string name, ConcurrentQueue<string> log) : base(name, log)
        {


            OnSyncIn += (char name) => OnSyncInAction(name).GetAwaiter().GetResult();
        }

        private async Task OnSyncInAction(char name)
        {
            if (!_buffer.ContainsKey(name) || _buffer[name].Count < 1)
                _log?.Enqueue("Got sync in signal, when no buffered items exist.");

            var parameters = _buffer[name].Dequeue();

            var distance = Math.Abs(parameters.TargetPosition - _deviceInfo[name].CurrentPosition);
            var recalculatedTargetSpeed = parameters.AllocatedTime > 0f
                ? distance / parameters.AllocatedTime
                : 0f;
            //var recalculatedTargetSpeed = CalculateTargetSpeed(parameters.AllocatedTime, distance, _deviceInfo[name].Acceleration, _deviceInfo[name].Deceleration);

            _deviceInfo[name].Speed = (float)recalculatedTargetSpeed;

            float targetPosition = parameters.TargetPosition;
            

            var device = Devices[name];
            if (!deviceCancellationTokens.ContainsKey(device.Name))
            {
                deviceCancellationTokens[device.Name] = new CancellationTokenSource();
            }
            else if (deviceCancellationTokens[device.Name].IsCancellationRequested)
            {
                deviceCancellationTokens[device.Name].Dispose();
                deviceCancellationTokens[device.Name] = new CancellationTokenSource();
            }
            else
            {
                deviceCancellationTokens[device.Name].Cancel();
                deviceCancellationTokens[device.Name] = new CancellationTokenSource();
            }


            _deviceInfo[device.Name].MoveStatus = 1;
            _log?.Enqueue($"SyncInAction called on {name}");

            _ = UpdateCommandMoveA(device.Name, targetPosition, deviceCancellationTokens[device.Name].Token);
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
                    MaxSpeed = positioningDevice.MaxSpeed,
                    Speed = positioningDevice.Speed,
                    Acceleration = positioningDevice.Acceleration,
                    Deceleration = positioningDevice.Deceleration,
                });
            }
        }
        public override BaseController GetVirtualCopy()
        {
            var controller = new PositionerController_Sim(Name, _log);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value.GetCopy());
            }
            controller.MasterController = this.MasterController;
            return controller;
        }
        public void InvokeSyncIn(char deviceName)
        {
            OnSyncIn?.Invoke(deviceName);
        }




        protected override async Task MoveAbsolute(Command command, SemaphoreSlim semaphore)
        {
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: move start");
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            var movementParameters = command.Parameters as MoveAbsoluteParameters;

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                if (!deviceCancellationTokens.ContainsKey(device.Name))
                {
                    deviceCancellationTokens[device.Name] = new CancellationTokenSource();
                }
                else if (deviceCancellationTokens[device.Name].IsCancellationRequested)
                {
                    deviceCancellationTokens[device.Name].Dispose();
                    deviceCancellationTokens[device.Name] = new CancellationTokenSource();
                }
                else
                {
                    deviceCancellationTokens[device.Name].Cancel();
                    deviceCancellationTokens[device.Name] = new CancellationTokenSource();
                }

                float targetPosition = movementParameters.PositionerInfo[device.Name].TargetPosition;

                _deviceInfo[device.Name].MoveStatus = 1;
                var task = UpdateCommandMoveA(device.Name, targetPosition, deviceCancellationTokens[device.Name].Token);
                tasks.Add(task);
            }
            //semaphore.Release();
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: move waiting");

            var waitUntilPositions = new Dictionary<char, float?>();
            var directions = new Dictionary<char, bool>();
            foreach (var (deviceName, movementInfo) in movementParameters.PositionerInfo)
            {
                waitUntilPositions[deviceName] = movementInfo.WaitUntilPosition;
                directions[deviceName] = movementInfo.Direction;
            }

            _ = Task.WhenAll(tasks);
            await WaitUntilStopAsync(waitUntilPositions, directions, semaphore);
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: move end");


        }
        protected override async Task UpdateMoveSettings(Command command, SemaphoreSlim semaphore)
        {
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: upd start");
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            var movementParams = command.Parameters as UpdateMovementSettingsParameters;


            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                float speedValue = movementParams.MovementSettingsInformation[device.Name].TargetSpeed;
                float accelValue = movementParams.MovementSettingsInformation[device.Name].TargetAcceleration;
                float decelValue = movementParams.MovementSettingsInformation[device.Name].TargetDeceleration;
                
                var task = UpdateMovementSettings(device.Name, speedValue, accelValue, decelValue);
                await task;
            }
        }
        protected override async Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            foreach (var positioner in Devices)
            {
                positioner.Value.CurrentPosition = _deviceInfo[positioner.Key].CurrentPosition;
                positioner.Value.CurrentSpeed = _deviceInfo[positioner.Key].CurrentSpeed;
                positioner.Value.Acceleration = _deviceInfo[positioner.Key].Acceleration;
                positioner.Value.Deceleration = _deviceInfo[positioner.Key].Deceleration;
                positioner.Value.Speed = _deviceInfo[positioner.Key].Speed;

                _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Updated state for device {positioner.Value.Name}, CurrentPos: {positioner.Value.CurrentPosition} CurrentSpeed: {positioner.Value.CurrentSpeed} Accel: {positioner.Value.Acceleration} Decel: {positioner.Value.Deceleration} Speed: {positioner.Value.Speed}  ");
            }
            await Task.Delay(1);
        }
        protected override Task Stop(Command command, SemaphoreSlim semaphore)
        {
            _buffer.Clear();

            foreach(var (deviceName, device) in Devices)
            {
                if (deviceCancellationTokens.ContainsKey(deviceName))
                {
                    deviceCancellationTokens[deviceName].Cancel();
                }
            }

            return Task.CompletedTask;
        }
        protected override void ConnectDevice_implementation(BaseDevice device)
        {
            if (device is BasePositionerDevice positioningDevice && _deviceInfo.TryGetValue(positioningDevice.Name, out DeviceInformation deviceInfo))
            {
                deviceInfo.MaxSpeed = positioningDevice.MaxSpeed;
                positioningDevice.Speed = positioningDevice.DefaultSpeed;
                positioningDevice.Acceleration = positioningDevice.MaxAcceleration;
                positioningDevice.Deceleration = positioningDevice.MaxDeceleration;

                deviceInfo.Speed = Math.Min(positioningDevice.Speed, positioningDevice.MaxSpeed);
                deviceInfo.Acceleration = Math.Min(positioningDevice.Acceleration, positioningDevice.MaxAcceleration);
                deviceInfo.Deceleration = Math.Min(positioningDevice.Deceleration, positioningDevice.MaxDeceleration);
            }
        }

        protected override Task<int> GetBufferFreeSpace(Command command, SemaphoreSlim semaphore)
        {
            return Task.Run(() =>
            {
                var maxItemSize = 20;
                var currentSize = _buffer.Count;

                return maxItemSize - currentSize;
            });
        }
        protected override Task AddSyncInAction(Command command, SemaphoreSlim semaphore)
        {
            var deviceNames = command.TargetDevices;
            var parameters = command.Parameters as AddSyncInActionParameters;
            for (int i = 0; i < deviceNames.Length; i++)
            {
                var deviceName = deviceNames[i];
                var targetPosition = parameters.MovementInformation[deviceName].Position;
                var allocatedTime = parameters.MovementInformation[deviceName].Time;

                var syncInAction = new SyncInAction()
                {
                    TargetPosition = targetPosition,
                    AllocatedTime = allocatedTime
                };
                if (!_buffer.ContainsKey(deviceName))
                {
                    _buffer[deviceName] = new Queue<SyncInAction>();
                }
                _buffer[deviceName].Enqueue(syncInAction);
            }
            return Task.CompletedTask;
        }

        private static double CalculateTargetSpeed(double totalTime, double totalDistance, double acceleration, double deceleration)
        {
            // Quadratic coefficients
            double A = 1;
            double B = -((acceleration + deceleration) * totalTime) / 2;
            double C = acceleration * deceleration * totalDistance;

            // Calculate discriminant
            double discriminant = B * B - 4 * A * C;

            // Check for non-negative discriminant
            if (discriminant < 0)
            {
                Console.WriteLine("No real solutions, check your input values.");
                return 0;
            }

            // Calculate both possible speeds (only one will be physically meaningful)
            double v_target1 = (-B + Math.Sqrt(discriminant)) / (2 * A);
            double v_target2 = (-B - Math.Sqrt(discriminant)) / (2 * A);

            // Return the positive, realistic target speed
            return Math.Max(v_target1, v_target2);
        }
        private Task UpdateMovementSettings(char name, float speedValue, float accelValue, float decelValue)
        {
            _deviceInfo[name].Speed = Math.Min(speedValue, Devices[name].MaxSpeed); ;
            _deviceInfo[name].Acceleration = Math.Min(accelValue, Devices[name].MaxAcceleration);
            _deviceInfo[name].Deceleration = Math.Min(decelValue, Devices[name].MaxDeceleration);

            return Task.CompletedTask;
        }
        private async Task WaitUntilStopAsync(Dictionary<char, float?> waitUntilPositions, Dictionary<char, bool> directions, SemaphoreSlim semaphore)
        {
            //var devices = waitUntilPositions.Keys.Select(deviceName => Devices[deviceName]).ToArray();
            var queuedItems = new List<Func<Task<bool>>>();

            foreach(var deviceName in waitUntilPositions.Keys)
            {
                var device = Devices[deviceName];
                if (waitUntilPositions[deviceName] == null)
                {
                    queuedItems.Add
                        (
                            async () =>
                            {
                                await Task.Delay(10);
                                bool boolCheck = _deviceInfo[device.Name].MoveStatus != 0;
                                var currentPosition = _deviceInfo[device.Name].CurrentPosition;
                                device.CurrentPosition = currentPosition;
                                device.CurrentSpeed = _deviceInfo[device.Name].CurrentSpeed;
                                return boolCheck;
                            }
                        );
                }
                else
                {
                    float targetPosition = (float)(waitUntilPositions[deviceName]);
                    bool direction = (bool)(directions[deviceName]);
                    queuedItems.Add
                        (
                            async () =>
                            {
                                var moveStatus = _deviceInfo[device.Name].MoveStatus != 0;
                                var currentPosition = _deviceInfo[device.Name].CurrentPosition;
                                device.CurrentSpeed = _deviceInfo[device.Name].CurrentSpeed;
                                device.CurrentPosition = currentPosition;


                                ////// Testing Remove afterwards
                                //var currentPositionX = _deviceInfo['x'].CurrentPosition;
                                //Devices['x'].CurrentPosition = currentPositionX;
                                //var currentPositionY = _deviceInfo['y'].CurrentPosition;
                                //Devices['y'].CurrentPosition = currentPositionY;

                                var boolCheck = moveStatus && (direction ? currentPosition < targetPosition : currentPosition > targetPosition);
                                await Task.Delay(10);

                                return boolCheck;
                            }
                        );
                }
            }


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


        }
        private async Task UpdateCommandMoveA(char name, float targetPosition, CancellationToken cancellationToken)
        {

            var targetSpeed = _deviceInfo[name].Speed;
            var targetAccel = _deviceInfo[name].Acceleration;
            var targetDecel = _deviceInfo[name].Deceleration;
            

            _log?.Enqueue($"UpdateCommandMoveA called on {name}");

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            var distanceToStop = () => 0.5f * targetDecel * Math.Pow((Math.Abs(_deviceInfo[name].CurrentSpeed) / targetDecel), 2);
            var directionToTarget = () => Math.Sign(targetPosition - _deviceInfo[name].CurrentPosition);
            var distanceToTarget = () => Math.Abs(targetPosition - _deviceInfo[name].CurrentPosition);
            var pointDifference = () => targetPosition - _deviceInfo[name].CurrentPosition;

            if (!float.IsFinite(targetPosition))
                throw new Exception("Non finite target position value provided");



            _deviceInfo[name].MoveStatus = 1;
            float movementPerInterval = 0 * _deviceInfo[name].CurrentSpeed;
            float accelerationPerInterval = 0 * targetAccel;
            float decelerationPerInterval = 0 * targetDecel;
            bool stopFlag = false;

            while (Math.Abs(pointDifference()) > Math.Abs(movementPerInterval) || Math.Abs(_deviceInfo[name].CurrentSpeed) > decelerationPerInterval)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Calculate time elapsed since last update
                float timeElapsed = (float)stopwatch.Elapsed.TotalSeconds;
                stopwatch.Restart();

                movementPerInterval = timeElapsed * _deviceInfo[name].CurrentSpeed;
                accelerationPerInterval = timeElapsed * targetAccel;
                decelerationPerInterval = timeElapsed * targetDecel;

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
                        if (Math.Abs(_deviceInfo[name].CurrentSpeed) > targetSpeed)
                            updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed - decelerationPerInterval * Math.Sign(pointDifference())) < targetSpeed
                                ? targetSpeed * Math.Sign(pointDifference())
                                : _deviceInfo[name].CurrentSpeed - decelerationPerInterval * Math.Sign(pointDifference());

                        // moving too slow than target speed.
                        else
                            updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed + accelerationPerInterval * Math.Sign(pointDifference())) > targetSpeed
                                ? targetSpeed * Math.Sign(pointDifference())
                                : _deviceInfo[name].CurrentSpeed + accelerationPerInterval * Math.Sign(pointDifference());



                    }
                }
                else
                {
                    updatedSpeedValue = Math.Abs(_deviceInfo[name].CurrentSpeed) - decelerationPerInterval * Math.Sign(_deviceInfo[name].CurrentSpeed) > distanceToTarget()
                        ? 0
                        : _deviceInfo[name].CurrentSpeed - decelerationPerInterval * Math.Sign(_deviceInfo[name].CurrentSpeed);
                }
                cancellationToken.ThrowIfCancellationRequested();

                _deviceInfo[name].CurrentSpeed = updatedSpeedValue;
                float updatedPositionValue;
                if (Math.Sign(pointDifference()) != Math.Sign(movementPerInterval))
                    updatedPositionValue = _deviceInfo[name].CurrentPosition + movementPerInterval;
                else if (distanceToTarget() < Math.Abs(movementPerInterval))
                    updatedPositionValue = targetPosition;
                else
                    updatedPositionValue = _deviceInfo[name].CurrentPosition + movementPerInterval;

                _deviceInfo[name].CurrentPosition = float.IsFinite(updatedPositionValue) ? updatedPositionValue : 0;

                await Task.Yield();
            }

            _deviceInfo[name].CurrentPosition = targetPosition;
            _deviceInfo[name].CurrentSpeed = 0;
            _deviceInfo[name].MoveStatus = 0;

            OnSyncOut?.Invoke(name);
        }
    }
}
