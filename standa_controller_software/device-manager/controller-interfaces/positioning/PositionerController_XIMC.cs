using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System.Collections.Concurrent;
using ximc;

namespace standa_controller_software.device_manager.controller_interfaces.positioning
{
    public class PositionerController_XIMC : BasePositionerController
    {
        private const uint MOVE_CMD_RUNNING = 0x80;
        private class DeviceInformation
        {
            public char name;
            public int id;
            public float maxAcceleration = 10000;
            public float maxDeceleration = 10000;
            public float maxSpeed;
            public calibration_t calibration_t;
            public status_calb_t statusCalibrated_t;
            public engine_settings_t engineSettings_t;
            public engine_settings_calb_t engineSettingsCalibrated_t;
            public status_t status_t;
            public device_information_t deviceInformation_t;
            public move_settings_calb_t moveSettings_t;
        }
        private Result _callResponse;
        private Result CallResponse
        {
            get => _callResponse;
            set
            {
                _callResponse = value;
                if (CallResponse != Result.ok)
                    throw new Exception("Error " + _callResponse.ToString());
            }
        }

        private ConcurrentDictionary<char, DeviceInformation> _deviceInfo = new ConcurrentDictionary<char, DeviceInformation>();

        public PositionerController_XIMC(string name) : base(name)
        {
        }

        public override void AddDevice(BaseDevice device)
        {
            base.AddDevice(device);

            if (device is BasePositionerDevice positioningDevice)
            {
                _deviceInfo.TryAdd(positioningDevice.Name, new DeviceInformation());
            }
        }
        public override Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore)
        {
            if (device is BasePositionerDevice positioningDevice && _deviceInfo.TryGetValue(positioningDevice.Name, out DeviceInformation deviceInfo))
            {
                deviceInfo.maxDeceleration = positioningDevice.MaxDeceleration;
                deviceInfo.maxAcceleration= positioningDevice.MaxAcceleration;
                deviceInfo.maxSpeed= positioningDevice.MaxSpeed;
                deviceInfo.name = positioningDevice.Name;

                const int probe_flags = (int)(Flags.ENUMERATE_PROBE | Flags.ENUMERATE_NETWORK);
                String enumerate_hints = "addr=192.168.1.1,172.16.2.3";
                var device_enumeration = API.enumerate_devices(probe_flags, enumerate_hints);
                int device_count = API.get_device_count(device_enumeration);
                string deviceName = string.Empty;
                for (int i = 0; i < device_count; i++)
                {
                    var foundName = API.get_device_name(device_enumeration, i);
                    if (foundName.Contains(positioningDevice.ID))
                    {
                        deviceName = foundName;
                        break;
                    }
                }
                if (deviceName == string.Empty)
                    throw new Exception($"Device with name: {positioningDevice.Name} and id: {positioningDevice.ID} was not found through controller interface");
                deviceInfo.id = API.open_device(deviceName);

                CallResponse = API.get_status(deviceInfo.id, out deviceInfo.status_t);
                CallResponse = API.get_device_information(deviceInfo.id, out deviceInfo.deviceInformation_t);
                CallResponse = API.get_engine_settings(deviceInfo.id, out deviceInfo.engineSettings_t);

                deviceInfo.calibration_t = new calibration_t();

                deviceInfo.calibration_t.A = positioningDevice.StepSize;
                deviceInfo.calibration_t.MicrostepMode = Math.Max(1, deviceInfo.engineSettings_t.MicrostepMode);
                CallResponse = API.get_status_calb(deviceInfo.id, out deviceInfo.statusCalibrated_t, ref deviceInfo.calibration_t);
                CallResponse = API.get_move_settings_calb(deviceInfo.id, out deviceInfo.moveSettings_t, ref deviceInfo.calibration_t);
                deviceInfo.moveSettings_t.Speed = Math.Min(positioningDevice.Speed, positioningDevice.MaxSpeed);
                deviceInfo.moveSettings_t.Accel = Math.Min(positioningDevice.Acceleration, positioningDevice.MaxAcceleration);
                deviceInfo.moveSettings_t.Decel = Math.Min(positioningDevice.Deceleration, positioningDevice.MaxDeceleration);
                CallResponse = API.set_move_settings_calb(deviceInfo.id, ref deviceInfo.moveSettings_t, ref deviceInfo.calibration_t);
            }
            
            return base.ConnectDevice(device, semaphore);
        }
        public override BaseController GetCopy()
        {
            var controller = new PositionerController_XIMC(Name);
            foreach (var device in Devices)
            {
                controller.AddDevice(device.Value.GetCopy());
                controller._deviceInfo[device.Key] = new DeviceInformation()
                {
                    name = this._deviceInfo[device.Key].name,
                    id = this._deviceInfo[device.Key].id,
                    maxAcceleration = this._deviceInfo[device.Key].maxAcceleration,
                    maxDeceleration = this._deviceInfo[device.Key].maxDeceleration,
                    maxSpeed = this._deviceInfo[device.Key].maxSpeed,
                    calibration_t = this._deviceInfo[device.Key].calibration_t,
                    statusCalibrated_t = this._deviceInfo[device.Key].statusCalibrated_t,
                    engineSettingsCalibrated_t = this._deviceInfo[device.Key].engineSettingsCalibrated_t,
                    engineSettings_t = this._deviceInfo[device.Key].engineSettings_t,
                    status_t = this._deviceInfo[device.Key].status_t,
                    deviceInformation_t = this._deviceInfo[device.Key].deviceInformation_t,
                    moveSettings_t = this._deviceInfo[device.Key].moveSettings_t,
                };
            }

            return controller;
        }

        public override Task UpdateStatesAsync(ConcurrentQueue<string> log)
        {
            foreach (var positioner in Devices)
            {
                if(positioner.Value.IsConnected)
                {
                    var deviceInfo = _deviceInfo[positioner.Key];
                    CallResponse = API.get_status_calb(deviceInfo.id, out deviceInfo.statusCalibrated_t, ref deviceInfo.calibration_t);

                    positioner.Value.CurrentPosition = deviceInfo.statusCalibrated_t.CurPosition;
                    positioner.Value.CurrentSpeed = deviceInfo.statusCalibrated_t.CurSpeed;

                    CallResponse = API.get_move_settings_calb(deviceInfo.id, out deviceInfo.moveSettings_t, ref deviceInfo.calibration_t);

                    positioner.Value.Acceleration = deviceInfo.moveSettings_t.Accel;
                    positioner.Value.Deceleration = deviceInfo.moveSettings_t.Decel;
                    positioner.Value.Speed = deviceInfo.moveSettings_t.Speed;
                    positioner.Value.MaxAcceleration = _deviceInfo[positioner.Key].maxAcceleration;
                    positioner.Value.MaxDeceleration = _deviceInfo[positioner.Key].maxDeceleration;
                    positioner.Value.MaxSpeed = _deviceInfo[positioner.Key].maxSpeed;
                    log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Updated state for device {positioner.Value.Name}, CurrentPos: {positioner.Value.CurrentPosition} CurrentSpeed: {positioner.Value.CurrentSpeed} Accel: {positioner.Value.Acceleration} Decel: {positioner.Value.Deceleration} Speed: {positioner.Value.Speed}  ");
                }
            }
            return Task.CompletedTask;
        }

        protected override Task UpdateMoveSettings(Command command, List<BasePositionerDevice> devices, Dictionary<char, CancellationToken> cancellationTokens, SemaphoreSlim semaphore)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                float speedValue = (float)(command.Parameters[i][0]);
                float accelValue = (float)(command.Parameters[i][1]);
                float decelValue = (float)(command.Parameters[i][2]);

                _deviceInfo[device.Name].moveSettings_t.Speed = speedValue;
                _deviceInfo[device.Name].moveSettings_t.Accel = accelValue;
                _deviceInfo[device.Name].moveSettings_t.Decel = decelValue;

                CallResponse = API.set_move_settings_calb(_deviceInfo[device.Name].id, ref _deviceInfo[device.Name].moveSettings_t, ref _deviceInfo[device.Name].calibration_t);
            }
            semaphore.Release();
            return Task.CompletedTask;
        }

        protected override async Task WaitUntilStop(Command command, List<BasePositionerDevice> devices, Dictionary<char, CancellationToken> cancellationTokens, SemaphoreSlim semaphore)
        {
            var queuedItems = new List<Func<Task<bool>>>();

            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (command.Parameters[i].Length == 0)
                {
                    queuedItems.Add
                        (
                            async () =>
                            {
                                var deviceInfo = _deviceInfo[device.Name];
                                CallResponse = API.get_status_calb(deviceInfo.id, out deviceInfo.statusCalibrated_t, ref deviceInfo.calibration_t);
                                device.CurrentPosition = deviceInfo.statusCalibrated_t.CurPosition;
                                bool boolCheck = (deviceInfo.statusCalibrated_t.MvCmdSts & MOVE_CMD_RUNNING) != 0;
                                return boolCheck;
                            }
                        );
                }
                else
                {
                    float targetPosition = (float)(command.Parameters[i][0]);
                    bool direction = (bool)(command.Parameters[i][1]);
                    queuedItems.Add
                        (
                            async () =>
                            {
                                var deviceInfo = _deviceInfo[device.Name];
                                CallResponse = API.get_status_calb(deviceInfo.id, out deviceInfo.statusCalibrated_t, ref deviceInfo.calibration_t);
                                var moveStatus = (deviceInfo.statusCalibrated_t.MvCmdSts & MOVE_CMD_RUNNING) != 0;
                                var currentPosition = deviceInfo.statusCalibrated_t.CurPosition; ;
                                device.CurrentPosition = currentPosition;

                                var boolCheck = moveStatus && (direction ? currentPosition < targetPosition : currentPosition > targetPosition);
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

                    //await Task.Delay(1); // A slight delay to prevent a tight loop; adjust as needed
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Smth wrong with wait until in Virtual Positioner");
            }
            finally
            {
                semaphore.Release();
            }
        }

        protected override Task MoveAbsolute(Command command, List<BasePositionerDevice> devices, Dictionary<char, CancellationToken> cancellationTokens, SemaphoreSlim semaphore)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                float targetPosition = (float)(command.Parameters[i][0]);

                API.command_move_calb(_deviceInfo[device.Name].id, targetPosition, ref _deviceInfo[device.Name].calibration_t);
            }

            semaphore.Release();
            return Task.CompletedTask;
        }
    }
}
