using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager.devices;
using System.Collections.Concurrent;

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
                get => _acceleration;
                set => _acceleration = value;
            }

            public float Deceleration
            {
                get => _deceleration;
                set => _deceleration = value;
            }

            public float Speed
            {
                get => _speed;
                set => _speed = value;
            }

            public float CurrentPosition { get; set; } = 0;
            public uint MoveStatus { get; set; } = 0;
            public char Name { get; set; }
            public float CurrentSpeed { get; set; } = 0;
            public float MaxSpeed { get; set; } = 1000;
        }

        // Device information indexed by char name
        private ConcurrentDictionary<char, DeviceInformation> _deviceInfo = new ConcurrentDictionary<char, DeviceInformation>();

        // Structure to hold SyncInAction commands
        private struct SyncInAction
        {
            public float TargetPosition;
            public float AllocatedTime;
        }

        // Buffer to hold SyncInAction commands per device
        private ConcurrentDictionary<char, ConcurrentQueue<SyncInAction>> _buffer = new ConcurrentDictionary<char, ConcurrentQueue<SyncInAction>>();

        //---------------------------------------------------

        // Track running move tasks per device
        private ConcurrentDictionary<char, Task> _runningMoveTasks = new ConcurrentDictionary<char, Task>();

        // Cancellation tokens per device
        private ConcurrentDictionary<char, CancellationTokenSource> _deviceCancellationTokens = new ConcurrentDictionary<char, CancellationTokenSource>();

        // Per-device locks for synchronization
        private ConcurrentDictionary<char, SemaphoreSlim> _deviceLocks = new ConcurrentDictionary<char, SemaphoreSlim>();

        // Events
        public event Action<char>? OnSyncOut;
        public event Func<char, Task> OnSyncIn;

        // Constructor
        public PositionerController_Sim(string name, ILoggerFactory loggerFactory) : base(name, loggerFactory)
        {
            _logger = _loggerFactory.CreateLogger<PositionerController_Sim>();
            // Subscribe to OnSyncIn event with asynchronous handler
            OnSyncIn += async (char deviceName) => await OnSyncInAction(deviceName);
        }

        /// <summary>
        /// Asynchronous handler for OnSyncIn event.
        /// Ensures that only one UpdateCommandMoveA runs per device.
        /// </summary>
        private async Task OnSyncInAction(char deviceName)
        {
            // Acquire per-device lock
            var deviceLock = _deviceLocks.GetOrAdd(deviceName, _ => new SemaphoreSlim(1, 1));

            await deviceLock.WaitAsync();
            try
            {
                // Check if there are any buffered SyncInAction commands
                if (!_buffer.ContainsKey(deviceName) || _buffer[deviceName].IsEmpty)
                {
                    _logger.LogInformation($"OnSyncInAction. Received SyncIn signal for '{deviceName}', but no buffered commands exist.");
                    return;
                }

                // Dequeue the next SyncInAction command
                if (_buffer[deviceName].TryDequeue(out var syncInAction))
                {
                    // Recalculate target speed based on allocated time and distance
                    var distance = Math.Abs(syncInAction.TargetPosition - _deviceInfo[deviceName].CurrentPosition);
                    var recalculatedTargetSpeed = syncInAction.AllocatedTime > 0f
                        ? distance / syncInAction.AllocatedTime
                        : 0f;

                    _deviceInfo[deviceName].Speed = recalculatedTargetSpeed;
                    float targetPosition = syncInAction.TargetPosition;

                    _logger.LogInformation($"OnSyncInAction. PreProcessing SyncInAction for '{deviceName}': TargetPosition={targetPosition}, AllocatedTime={syncInAction.AllocatedTime}");

                    // Manage cancellation tokens
                    if (_deviceCancellationTokens.TryGetValue(deviceName, out var existingCts))
                    {
                        // Cancel the existing movement task
                        existingCts.Cancel();
                        _logger.LogInformation($"OnSyncInAction. Canceled _deviceCancellationTokens for '{deviceName}'.");

                        // Await the completion of the existing task
                        if (_runningMoveTasks.TryGetValue(deviceName, out var existingTask))
                        {
                            _logger.LogInformation($"OnSyncInAction. Running task for '{deviceName}' encountered");

                            try
                            {
                                await existingTask;
                                _logger.LogInformation($"OnSyncInAction. Running task for '{deviceName}' awaited");

                            }
                            catch (OperationCanceledException)
                            {
                                _logger.LogInformation($"OnSyncInAction. Existing movement task for '{deviceName}' was canceled.");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation($"OnSyncInAction. Error in existing movement task for '{deviceName}': {ex.Message}");
                            }
                        }

                        // Dispose of the old CancellationTokenSource and create a new one
                        existingCts.Dispose();
                        _deviceCancellationTokens[deviceName] = new CancellationTokenSource();
                        _logger.LogInformation($"OnSyncInAction. Dispose of the old CancellationTokenSource and create a new one");

                    }
                    else
                    {
                        // No existing CancellationTokenSource, create a new one
                        _logger.LogInformation($"OnSyncInAction. No existing CancellationTokenSource for '{deviceName}', creating a new one.");

                        _deviceCancellationTokens[deviceName] = new CancellationTokenSource();
                    }

                    // Start the UpdateCommandMoveA task and track it
                    _logger.LogInformation($"OnSyncInAction. Start the UpdateCommandMoveA task and add to the _runningMoveTasks.");

                    var moveTask = UpdateCommandMoveA(deviceName, targetPosition, _deviceCancellationTokens[deviceName].Token);
                    _runningMoveTasks[deviceName] = moveTask;
                }
            }
            finally
            {
                // Release the per-device lock
                deviceLock.Release();
                _logger.LogInformation($"OnSyncInAction. End.");
            }
        }

        /// <summary>
        /// Adds a new device to the controller.
        /// Initializes device information.
        /// </summary>
        /// 
        protected override Task Home(Command command, SemaphoreSlim semaphore)
        {
            foreach (var (deviceName, device) in Devices)
            {
                _deviceInfo[deviceName].CurrentPosition = 0;
            }

            return Task.CompletedTask;
        }
        public override void AddDevice(BaseDevice device)
        {
            base.AddDevice(device);

            if (device is BasePositionerDevice positioningDevice)
            {
                char deviceName = positioningDevice.Name; // Assuming Name is a single char

                var deviceInfo = new DeviceInformation
                {
                    CurrentPosition = positioningDevice.CurrentPosition,
                    CurrentSpeed = positioningDevice.CurrentSpeed,
                    MaxSpeed = positioningDevice.MaxSpeed,
                    Speed = positioningDevice.Speed,
                    Acceleration = positioningDevice.Acceleration,
                    Deceleration = positioningDevice.Deceleration,
                    Name = positioningDevice.Name
                };

                if (_deviceInfo.TryAdd(deviceName, deviceInfo))
                {
                    _logger.LogInformation($" Added device '{deviceName}' with initial settings.");
                }
                else
                {
                    _logger.LogInformation($" Device '{deviceName}' already exists.");
                }
            }
        }

        /// <summary>
        /// Invokes the OnSyncIn event for a given device.
        /// </summary>
        public void InvokeSyncIn(char deviceName)
        {
            _logger.LogInformation($" InvokeSyncIn called for '{deviceName}'.");

            // Fire and forget asynchronous event handlers
            var handlers = OnSyncIn?.GetInvocationList().Cast<Func<char, Task>>().ToList();
            if (handlers != null && handlers.Count > 0)
            {
                foreach (var handler in handlers)
                {
                    _ = Task.Run(() => handler.Invoke(deviceName));
                }
            }
        }

        /// <summary>
        /// Forces all devices to stop their movements.
        /// Cancels all running tasks and clears the buffer.
        /// </summary>
        public override async Task ForceStop()
        {
            _logger.LogInformation($" ForceStop initiated.");

            // Cancel all cancellation tokens
            foreach (var (deviceName, cts) in _deviceCancellationTokens)
            {
                cts.Cancel();
                _deviceInfo[deviceName].MoveStatus = 0;
                _deviceInfo[deviceName].CurrentSpeed = 0;
            }

            // Await all running movement tasks
            foreach (var kvp in _runningMoveTasks)
            {
                try
                {
                    await kvp.Value;
                }
                catch (OperationCanceledException)
                {
                    // Expected, do nothing
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($" Error in ForceStop for '{kvp.Key}': {ex.Message}");
                }
            }

            // Clear all buffers
            _buffer.Clear();

            _logger.LogInformation($" ForceStop completed.");
        }

        /// <summary>
        /// Moves devices to absolute positions based on the provided command.
        /// </summary>
        protected override async Task MoveAbsolute(Command command, SemaphoreSlim semaphore)
        {
            _logger.LogInformation($" MoveAbsolute initiated.");

            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            var movementParameters = command.Parameters as MoveAbsoluteParameters;
            
            if (movementParameters is null)
                throw new Exception("Wrong parameter set provided for Move Absolute command.");

            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                char deviceName = device.Name; // Assuming Name is a single char

                // Acquire per-device lock
                var deviceLock = _deviceLocks.GetOrAdd(deviceName, _ => new SemaphoreSlim(1, 1));

                await deviceLock.WaitAsync();
                try
                {
                    // Manage cancellation tokens
                    if (_deviceCancellationTokens.TryGetValue(deviceName, out var existingCts))
                    {
                        // Cancel the existing task
                        existingCts.Cancel();

                        // Await the existing task to ensure it has completed
                        if (_runningMoveTasks.TryGetValue(deviceName, out var existingTask))
                        {
                            try
                            {
                                await existingTask;
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.LogInformation($" Existing movement task for '{deviceName}' was canceled.");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation($" Error in existing movement task for '{deviceName}': {ex.Message}");
                            }
                        }

                        // Dispose of the old CancellationTokenSource and create a new one
                        existingCts.Dispose();
                        _deviceCancellationTokens[deviceName] = new CancellationTokenSource();
                    }
                    else
                    {
                        // No existing CancellationTokenSource, create a new one
                        _deviceCancellationTokens[deviceName] = new CancellationTokenSource();
                    }

                    // Extract target position from command parameters
                    float targetPosition = movementParameters.PositionerInfo[deviceName].TargetPosition;

                    _deviceInfo[deviceName].MoveStatus = 1;
                    _logger.LogInformation($" MoveAbsolute called on '{deviceName}' with TargetPosition={targetPosition}.");

                    // Start the UpdateCommandMoveA task and track it
                    var moveTask = UpdateCommandMoveA(deviceName, targetPosition, _deviceCancellationTokens[deviceName].Token);
                    _runningMoveTasks[deviceName] = moveTask;
                }
                finally
                {
                    // Release the per-device lock
                    deviceLock.Release();
                }
            }

            // Prepare wait until stop conditions
            var waitUntilPositions = new Dictionary<char, float?>();
            var directions = new Dictionary<char, bool>();

            foreach (var (deviceName, movementInfo) in movementParameters.PositionerInfo)
            {
                waitUntilPositions[deviceName] = movementInfo.WaitUntilPosition;
                directions[deviceName] = movementInfo.Direction;
            }

            // Wait until all devices have stopped moving
            await WaitUntilStopAsync(waitUntilPositions, directions, semaphore);

            _logger.LogInformation($" MoveAbsolute completed.");
        }

        /// <summary>
        /// Checks if a device is stationary.
        /// </summary>
        protected override async Task<bool> IsDeviceStationary(BasePositionerDevice device)
        {
            char deviceName = device.Name; // Assuming Name is a single char
            bool isStationary = _deviceInfo[deviceName].MoveStatus == 0;
            device.CurrentPosition = _deviceInfo[deviceName].CurrentPosition;
            await Task.Delay(1);

            return isStationary;
        }

        /// <summary>
        /// Updates movement settings for devices based on the provided command.
        /// </summary>
        protected override async Task UpdateMoveSettings(Command command, SemaphoreSlim semaphore)
        {
            _logger.LogInformation($" UpdateMoveSettings initiated.");

            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            var movementParams = command.Parameters as UpdateMovementSettingsParameters;

            if (movementParams == null)
                throw new Exception("Wrong parameeter set provided for Update Move Settings command.");

            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                char deviceName = device.Name; // Assuming Name is a single char
                float speedValue = movementParams.MovementSettingsInformation[deviceName].TargetSpeed;
                float accelValue = movementParams.MovementSettingsInformation[deviceName].TargetAcceleration;
                float decelValue = movementParams.MovementSettingsInformation[deviceName].TargetDeceleration;

                await UpdateMovementSettings(deviceName, speedValue, accelValue, decelValue);

                _logger.LogInformation($" Updated movement settings for '{deviceName}': Speed={speedValue}, Acceleration={accelValue}, Deceleration={decelValue}.");
            }

            _logger.LogInformation($" UpdateMoveSettings completed.");
        }

        /// <summary>
        /// Updates the internal state of devices asynchronously.
        /// </summary>
        protected override async Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            foreach (var positioner in Devices)
            {
                char deviceName = positioner.Key; // Assuming key is a single char
                var deviceInfo = _deviceInfo[deviceName];

                positioner.Value.CurrentPosition = deviceInfo.CurrentPosition;
                positioner.Value.CurrentSpeed = deviceInfo.CurrentSpeed;
                positioner.Value.Acceleration = deviceInfo.Acceleration;
                positioner.Value.Deceleration = deviceInfo.Deceleration;
                positioner.Value.Speed = deviceInfo.Speed;

                
                _logger.LogDebug($" Updated state for device '{deviceName}': " +
                            $"CurrentPos={positioner.Value.CurrentPosition}, " +
                            $"CurrentSpeed={positioner.Value.CurrentSpeed}, " +
                            $"Accel={positioner.Value.Acceleration}, " +
                            $"Decel={positioner.Value.Deceleration}, " +
                            $"Speed={positioner.Value.Speed}.");
            }

            await Task.Delay(1); // Simulate asynchronous operation
        }

        /// <summary>
        /// Stops all device movements based on the provided command.
        /// </summary>
        protected override async Task Stop(Command command, SemaphoreSlim semaphore)
        {
            _logger.LogInformation($" Stop initiated.");

            // Clear the buffer
            _buffer = new ConcurrentDictionary<char, ConcurrentQueue<SyncInAction>>();

            // Cancel all cancellation tokens
            foreach (var cts in _deviceCancellationTokens.Values)
            {
                cts.Cancel();
            }

            // Await all running movement tasks
            foreach (var kvp in _runningMoveTasks)
            {
                try
                {
                    await kvp.Value;
                }
                catch (OperationCanceledException)
                {
                    // Expected, do nothing
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($" Error in Stop for '{kvp.Key}': {ex.Message}");
                }
            }

            _logger.LogInformation($" Stop completed.");
        }

        /// <summary>
        /// Connects a device to the controller, initializing its settings.
        /// </summary>
        protected override void ConnectDevice_implementation(BaseDevice device)
        {
            if (device is BasePositionerDevice positioningDevice)
            {
                char deviceName = positioningDevice.Name; // Assuming Name is a single char

                if (_deviceInfo.TryGetValue(deviceName, out var deviceInfo))
                {
                    deviceInfo.MaxSpeed = positioningDevice.MaxSpeed;
                    positioningDevice.Speed = positioningDevice.DefaultSpeed;
                    positioningDevice.Acceleration = positioningDevice.MaxAcceleration;
                    positioningDevice.Deceleration = positioningDevice.MaxDeceleration;

                    deviceInfo.Speed = Math.Min(positioningDevice.Speed, positioningDevice.MaxSpeed);
                    deviceInfo.Acceleration = Math.Min(positioningDevice.Acceleration, positioningDevice.MaxAcceleration);
                    deviceInfo.Deceleration = Math.Min(positioningDevice.Deceleration, positioningDevice.MaxDeceleration);

                    _logger.LogInformation($" Connected device '{deviceName}' with settings: " +
                                $"Speed={deviceInfo.Speed}, " +
                                $"Acceleration={deviceInfo.Acceleration}, " +
                                $"Deceleration={deviceInfo.Deceleration}, " +
                                $"MaxSpeed={deviceInfo.MaxSpeed}.");
                }
                else
                {
                    _logger.LogInformation($" Attempted to connect device '{deviceName}', but it does not exist in _deviceInfo.");
                }
            }
        }

        /// <summary>
        /// Retrieves the free space available in the buffer for new SyncInAction commands.
        /// </summary>
        protected override Task<int> GetBufferFreeSpace(Command command, SemaphoreSlim semaphore)
        {
            return Task.Run(() =>
            {
                const int maxItemSize = 20; // Maximum buffer size per device
                int freeSpace = int.MaxValue;

                foreach (var deviceName in command.TargetDevices)
                {
                    if (_buffer.TryGetValue(deviceName, out var queue))
                    {
                        freeSpace = Math.Min(maxItemSize - queue.Count(), freeSpace);
                    }
                    // theres no buffered items for the device
                    else
                    {
                        freeSpace = Math.Min(maxItemSize , freeSpace);
                    }
                }

                _logger.LogInformation($" Buffer free space for devices {string.Join(",", command.TargetDevices)}: {freeSpace}.");
                return freeSpace;
            });
        }

        /// <summary>
        /// Adds SyncInAction commands to the buffer based on the provided command.
        /// </summary>
        protected override Task AddSyncInAction(Command command, SemaphoreSlim semaphore)
        {
            var deviceNames = command.TargetDevices;
            var parameters = command.Parameters as AddSyncInActionParameters;

            if (parameters is null)
                throw new Exception("Wrong parameter set provided for Add Sync In Action command.");

            for (int i = 0; i < deviceNames.Length; i++)
            {
                char deviceName = deviceNames[i];
                float targetPosition = parameters.MovementInformation[deviceName].Position;
                float allocatedTime = parameters.MovementInformation[deviceName].Time;

                var syncInAction = new SyncInAction
                {
                    TargetPosition = targetPosition,
                    AllocatedTime = allocatedTime
                };

                var queue = _buffer.GetOrAdd(deviceName, _ => new ConcurrentQueue<SyncInAction>());
                const int maxBufferSizePerDevice = 20;

                if (queue.Count < maxBufferSizePerDevice)
                {
                    queue.Enqueue(syncInAction);
                    _logger.LogInformation($" Added SyncInAction for '{deviceName}': TargetPosition={targetPosition}, AllocatedTime={allocatedTime}.");
                }
                else
                {
                    _logger.LogInformation($" Buffer full for '{deviceName}'. SyncInAction discarded.");
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Calculates the target speed based on total time, distance, acceleration, and deceleration.
        /// </summary>
        private static double CalculateTargetSpeed(double totalTime, double totalDistance, double acceleration, double deceleration)
        {
            // Quadratic equation coefficients
            double A = 1;
            double B = -((acceleration + deceleration) * totalTime) / 2;
            double C = acceleration * deceleration * totalDistance;

            // Calculate discriminant
            double discriminant = B * B - 4 * A * C;

            if (discriminant < 0)
            {
                Console.WriteLine("No real solutions. Check your input values.");
                return 0;
            }

            // Calculate both possible speeds
            double v_target1 = (-B + Math.Sqrt(discriminant)) / (2 * A);
            double v_target2 = (-B - Math.Sqrt(discriminant)) / (2 * A);

            // Return the positive, realistic target speed
            return Math.Max(v_target1, v_target2);
        }

        /// <summary>
        /// Updates movement settings for a specific device.
        /// </summary>
        private Task UpdateMovementSettings(char deviceName, float speedValue, float accelValue, float decelValue)
        {
            if (_deviceInfo.TryGetValue(deviceName, out var deviceInfo))
            {
                var device = Devices[deviceName];
               
                deviceInfo.Speed = Math.Min(speedValue, device.MaxSpeed);
                deviceInfo.Acceleration = Math.Min(accelValue, device.MaxAcceleration);
                deviceInfo.Deceleration = Math.Min(decelValue, device.MaxDeceleration);

                _logger.LogInformation($" Updated movement settings for '{deviceName}': Speed={speedValue}, Acceleration={accelValue}, Deceleration={decelValue}.");
            }
            else
            {
                _logger.LogInformation($" Attempted to update settings for unknown device '{deviceName}'.");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Waits asynchronously until all specified devices have stopped moving.
        /// </summary>
        private async Task WaitUntilStopAsync(Dictionary<char, float?> waitUntilPositions, Dictionary<char, bool> directions, SemaphoreSlim semaphore)
        {
            var waitTasks = new List<Task>();

            foreach (var deviceName in waitUntilPositions.Keys)
            {
                waitTasks.Add(Task.Run(async () =>
                {
                    while (true)
                    {
                        // Check if movement is still in progress
                        bool isMoving = _deviceInfo[deviceName].MoveStatus != 0;
                        float currentPosition = _deviceInfo[deviceName].CurrentPosition;
                        float? targetWaitPosition = waitUntilPositions[deviceName];
                        bool direction = directions[deviceName];

                        bool conditionMet = targetWaitPosition == null
                            ? !isMoving
                            : (direction ? currentPosition >= targetWaitPosition.Value : currentPosition <= targetWaitPosition.Value);

                        if (conditionMet)
                        {
                            break;
                        }

                        await Task.Delay(10);
                    }
                }));
            }

            try
            {
                await Task.WhenAll(waitTasks);
                _logger.LogInformation($" All devices have stopped moving as per WaitUntilStopAsync.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($" Error in WaitUntilStopAsync: {ex.Message}");
                throw new Exception("An error occurred while waiting for devices to stop moving.", ex);
            }
        }

        /// <summary>
        /// Handles the movement of a device towards a target position.
        /// Ensures that only one instance runs per device and handles cancellation.
        /// </summary>
        private async Task UpdateCommandMoveA(char deviceName, float targetPosition, CancellationToken cancellationToken)
        {
            if (!_deviceInfo.TryGetValue(deviceName, out var deviceInfo))
            {
                _logger.LogInformation($" UpdateCommandMoveA called for unknown device '{deviceName}'.");
                return;
            }

            _logger.LogInformation($" UpdateCommandMoveA started for '{deviceName}' towards Position={targetPosition}.");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Helper functions
            float DistanceToStop() =>
                0.5f * deviceInfo.Deceleration * MathF.Pow(deviceInfo.CurrentSpeed / deviceInfo.Deceleration, 2);

            float DirectionToTarget() =>
                MathF.Sign(targetPosition - deviceInfo.CurrentPosition);

            float DistanceToTarget() =>
                MathF.Abs(targetPosition - deviceInfo.CurrentPosition);

            float PointDifference() =>
                targetPosition - deviceInfo.CurrentPosition;

            if (!float.IsFinite(targetPosition))
            {
                _logger.LogInformation($" Invalid target position for '{deviceName}': {targetPosition}.");
                throw new ArgumentException("Non-finite target position value provided.", nameof(targetPosition));
            }

            deviceInfo.MoveStatus = 1; // Indicate movement is in progress
            bool stopFlag = false;

            try
            {
                while (MathF.Abs(PointDifference()) > 0 || MathF.Abs(deviceInfo.CurrentSpeed) > 0) // Thresholds to prevent infinite loop
                {
                    // Frequent cancellation checks
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation($" UpdateCommandMoveA canceled for '{deviceName}'.");
                        return; // Exit early without final state updates
                    }

                    // Calculate time elapsed since last update
                    float timeElapsed = (float)stopwatch.Elapsed.TotalSeconds;
                    stopwatch.Restart();

                    // Calculate movement and acceleration/deceleration per interval
                    float movementPerInterval = timeElapsed * deviceInfo.CurrentSpeed;
                    float accelerationPerInterval = timeElapsed * deviceInfo.Acceleration;
                    float decelerationPerInterval = timeElapsed * deviceInfo.Deceleration;

                    float updatedSpeedValue;

                    // Determine direction and speed adjustments
                    if (DirectionToTarget() == MathF.Sign(deviceInfo.CurrentSpeed) || deviceInfo.CurrentSpeed == 0f)
                    {
                        float distToStop = DistanceToStop();
                        float distToTarget = DistanceToTarget();

                        // check if we are in the range of stopping
                        if (distToTarget < distToStop || stopFlag)
                        {
                            stopFlag = true;
                            if (MathF.Abs(deviceInfo.CurrentSpeed) < decelerationPerInterval)
                            {
                                updatedSpeedValue = 0f;
                                break; // Exit the loop to finalize
                            }
                            else
                            {
                                // Decelerate
                                updatedSpeedValue = deviceInfo.CurrentSpeed - decelerationPerInterval * MathF.Sign(deviceInfo.CurrentSpeed);
                            }
                        }
                        // we are good to go, no need to decelerate to a stop.
                        // moving to the target direction.
                        // we might still be going too fast though.
                        else
                        {
                            // moving too faster target speed. 
                            if (MathF.Abs(deviceInfo.CurrentSpeed) > deviceInfo.Speed)
                            {
                                // Decelerate to target speed
                                float potentialSpeed = deviceInfo.CurrentSpeed - decelerationPerInterval * MathF.Sign(PointDifference());
                                updatedSpeedValue = MathF.Abs(potentialSpeed) < deviceInfo.Speed
                                    ? deviceInfo.Speed * MathF.Sign(PointDifference())
                                    : potentialSpeed;
                            }
                            else
                            {
                                // moving slower than target speed.
                                float potentialSpeed = deviceInfo.CurrentSpeed + accelerationPerInterval * MathF.Sign(PointDifference());
                                updatedSpeedValue = MathF.Abs(potentialSpeed) > deviceInfo.Speed
                                    ? deviceInfo.Speed * MathF.Sign(PointDifference())
                                    : potentialSpeed;
                            }
                        }
                    }
                    else
                    {
                        // Reverse direction
                        float potentialSpeed = deviceInfo.CurrentSpeed - decelerationPerInterval * MathF.Sign(deviceInfo.CurrentSpeed);
                        updatedSpeedValue = MathF.Abs(potentialSpeed) > DistanceToTarget()
                            ? 0f
                            : potentialSpeed;
                    }

                    // Update speed
                    deviceInfo.CurrentSpeed = updatedSpeedValue;

                    // Update position
                    float updatedPositionValue;
                    if (MathF.Sign(PointDifference()) != MathF.Sign(movementPerInterval))
                    {
                        updatedPositionValue = deviceInfo.CurrentPosition + movementPerInterval;
                    }
                    else if (DistanceToTarget() < MathF.Abs(movementPerInterval) && decelerationPerInterval > updatedSpeedValue)
                    {
                        updatedPositionValue = targetPosition;
                        break;
                    }
                    else
                    {
                        updatedPositionValue = deviceInfo.CurrentPosition + movementPerInterval;
                    }

                    deviceInfo.CurrentPosition = float.IsFinite(updatedPositionValue) ? updatedPositionValue : 0f;

                    // Update the device's current position
                    Devices[deviceName].CurrentPosition = deviceInfo.CurrentPosition;

                    // Optional: Update device speed if needed
                    Devices[deviceName].CurrentSpeed = deviceInfo.CurrentSpeed;

                    // Small delay to prevent tight loop; adjust as needed for responsiveness
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }

                // Final state updates only if not canceled
                if (!cancellationToken.IsCancellationRequested)
                {
                    deviceInfo.CurrentPosition = targetPosition;
                    deviceInfo.CurrentSpeed = 0f;
                    deviceInfo.MoveStatus = 0;

                    OnSyncOut?.Invoke(deviceName);
                    _logger.LogInformation($" UpdateCommandMoveA completed successfully for '{deviceName}'.");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($" UpdateCommandMoveA operation canceled for '{deviceName}'.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($" UpdateCommandMoveA encountered an error for '{deviceName}': {ex.Message}");
                throw;
            }
        }
    }
}
