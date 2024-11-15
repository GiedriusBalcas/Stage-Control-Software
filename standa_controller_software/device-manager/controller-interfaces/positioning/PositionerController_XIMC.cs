using OpenTK.Audio.OpenAL;
using OpenTK.Compute.OpenCL;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.command_manager.command_parameter_library.Positioners;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;
using ximcWrapper;

namespace standa_controller_software.device_manager.controller_interfaces.positioning
{
    public class PositionerController_XIMC : BasePositionerController
    {
        //----------Virtual axes private data---------------
        private const uint MOVE_CMD_RUNNING = 0x80;
        private class DeviceInformation
        {
            public char name;
            public int id;
            public float maxAcceleration;
            public float maxDeceleration;
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

        //------------------------------------------------------------------------



        private static API.LoggingCallback callback;
        private void MyLog(API.LogLevel loglevel, string message, IntPtr user_data)
        {
            _log?.Enqueue(message);
        }
        
        public PositionerController_XIMC(string name, ConcurrentQueue<string> log) : base(name, log)
        {
            _methodMap[CommandDefinitions.AddSyncInAction] = new MethodInformation()
            {
                MethodHandle = AddSyncInAction,
                State = MethodState.Free,
            };


            callback = new API.LoggingCallback(MyLog);
            API.set_logging_callback(callback, IntPtr.Zero);

        }
        public override void AddDevice(BaseDevice device)
        {
            base.AddDevice(device);

            if (device is BasePositionerDevice positioningDevice)
            {
                _deviceInfo.TryAdd(positioningDevice.Name, new DeviceInformation());
            }
        }
        public override BaseController GetVirtualCopy()
        {
            var controller = new PositionerController_XIMC(Name, _log);
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
        
        protected override void ConnectDevice_implementation(BaseDevice device)
        {
            if (device is BasePositionerDevice positioningDevice && _deviceInfo.TryGetValue(positioningDevice.Name, out DeviceInformation deviceInfo))
            {
                deviceInfo.maxDeceleration = positioningDevice.MaxDeceleration;
                deviceInfo.maxAcceleration = positioningDevice.MaxAcceleration;
                deviceInfo.maxSpeed = positioningDevice.MaxSpeed;
                deviceInfo.name = positioningDevice.Name;

                API.set_bindy_key("keyfile.sqlite");
                IntPtr device_enumeration;
                const int probe_flags = (int)(Flags.ENUMERATE_PROBE | Flags.ENUMERATE_NETWORK);

                String enumerate_hints = "addr=192.168.1.1,172.16.2.3";

                device_enumeration = API.enumerate_devices(probe_flags, enumerate_hints);
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

                positioningDevice.Speed = positioningDevice.DefaultSpeed;
                positioningDevice.Acceleration = positioningDevice.MaxAcceleration;
                positioningDevice.Deceleration = positioningDevice.MaxDeceleration;

                deviceInfo.moveSettings_t.Speed = Math.Min(positioningDevice.Speed, positioningDevice.MaxSpeed);
                deviceInfo.moveSettings_t.Accel = Math.Min(positioningDevice.Acceleration, positioningDevice.MaxAcceleration);
                deviceInfo.moveSettings_t.Decel = Math.Min(positioningDevice.Deceleration, positioningDevice.MaxDeceleration);

                CallResponse = API.set_move_settings_calb(deviceInfo.id, ref deviceInfo.moveSettings_t, ref deviceInfo.calibration_t);
            }
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
                _deviceInfo[device.Name].moveSettings_t.Speed = speedValue;
                _deviceInfo[device.Name].moveSettings_t.Accel = accelValue;
                _deviceInfo[device.Name].moveSettings_t.Decel = decelValue;

                if(accelValue != 0 && decelValue != 0 && speedValue != 0)
                {
                    CallResponse = API.set_move_settings_calb(_deviceInfo[device.Name].id, ref _deviceInfo[device.Name].moveSettings_t, ref _deviceInfo[device.Name].calibration_t);
                    _log.Enqueue($"ximc: updated move settings on {device.Name}. Speed: {_deviceInfo[device.Name].moveSettings_t.Speed};   Accel: {_deviceInfo[device.Name].moveSettings_t.Accel};  Decel: {_deviceInfo[device.Name].moveSettings_t.Decel}");
                }
                else
                {
                    _log.Enqueue($"ximc: ---------FAILED TO UPDATE move settings on {device.Name}. Speed: {_deviceInfo[device.Name].moveSettings_t.Speed};   Accel: {_deviceInfo[device.Name].moveSettings_t.Accel};  Decel: {_deviceInfo[device.Name].moveSettings_t.Decel}");

                }
            }
            return Task.CompletedTask;
        }
        protected override async Task MoveAbsolute(Command command, SemaphoreSlim semaphore)
        {
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: move start");
            var devices = command.TargetDevices.Select(deviceName => Devices[deviceName]).ToArray();
            var movementParameters = command.Parameters as MoveAbsoluteParameters;

            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                float targetPosition = movementParameters.PositionerInfo[device.Name].TargetPosition;

               API.command_move_calb(_deviceInfo[device.Name].id, targetPosition, ref _deviceInfo[device.Name].calibration_t);
                
            }

            var waitUntilPositions = new Dictionary<char, float?>();
            var directions = new Dictionary<char, bool>();
            foreach (var (deviceName, movementInfo) in movementParameters.PositionerInfo)
            {
                waitUntilPositions[deviceName] = movementInfo.WaitUntilPosition;
                directions[deviceName] = movementInfo.Direction;
            }

            await WaitUntilStopAsync(waitUntilPositions, directions, semaphore);
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: move end");

        }
        protected async Task WaitUntilStopAsync(Dictionary<char, float?> waitUntilPositions, Dictionary<char, bool> directions, SemaphoreSlim semaphore)
        {
            //var devices = waitUntilPositions.Keys.Select(deviceName => Devices[deviceName]).ToArray();
            var queuedItems = new List<Func<Task<bool>>>();

            foreach (var deviceName in waitUntilPositions.Keys)
            {
                var device = Devices[deviceName];
                if (waitUntilPositions[deviceName] == null)
                {
                    queuedItems.Add
                        (
                            async () =>
                            {
                                var deviceInfo = _deviceInfo[device.Name];
                                CallResponse = API.get_status_calb(deviceInfo.id, out deviceInfo.statusCalibrated_t, ref deviceInfo.calibration_t);
                                device.CurrentPosition = deviceInfo.statusCalibrated_t.CurPosition;
                                bool boolCheck = (deviceInfo.statusCalibrated_t.MvCmdSts & MOVE_CMD_RUNNING) != 0;
                                await Task.Delay(10);
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
                                var deviceInfo = _deviceInfo[device.Name];
                                CallResponse = API.get_status_calb(deviceInfo.id, out deviceInfo.statusCalibrated_t, ref deviceInfo.calibration_t);
                                var moveStatus = (deviceInfo.statusCalibrated_t.MvCmdSts & MOVE_CMD_RUNNING) != 0;
                                var currentPosition = deviceInfo.statusCalibrated_t.CurPosition; ;
                                device.CurrentPosition = currentPosition;

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
        protected override Task Stop(Command command, SemaphoreSlim semaphore)
        {
            foreach (var (deviceName, device) in Devices)
            {
                if (device.IsConnected)
                {
                    CallResponse = API.command_stop(_deviceInfo[device.Name].id);
                    CallResponse = API.get_sync_in_settings_calb(_deviceInfo[deviceName].id, out sync_in_settings_calb_t sync_in_settings_calb, ref _deviceInfo[deviceName].calibration_t);
                    CallResponse = API.get_status_calb(_deviceInfo[deviceName].id, out _deviceInfo[deviceName].statusCalibrated_t, ref _deviceInfo[deviceName].calibration_t);
                    var position = _deviceInfo[deviceName].statusCalibrated_t.CurPosition;

                    sync_in_settings_calb.Position = position;

                    CallResponse = API.set_sync_in_settings_calb(_deviceInfo[deviceName].id, ref sync_in_settings_calb, ref _deviceInfo[deviceName].calibration_t);
                }
            }
            return Task.CompletedTask;
        }
        protected override Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            foreach (var positioner in Devices)
            {
                if (positioner.Value.IsConnected)
                {
                    var deviceInfo = _deviceInfo[positioner.Key];
                    CallResponse = API.get_move_settings_calb(deviceInfo.id, out deviceInfo.moveSettings_t, ref deviceInfo.calibration_t);

                    positioner.Value.Acceleration = deviceInfo.moveSettings_t.Accel;
                    positioner.Value.Deceleration = deviceInfo.moveSettings_t.Decel;
                    positioner.Value.Speed = deviceInfo.moveSettings_t.Speed;
                    
                    CallResponse = API.get_status_calb(deviceInfo.id, out deviceInfo.statusCalibrated_t, ref deviceInfo.calibration_t);

                    positioner.Value.CurrentPosition = deviceInfo.statusCalibrated_t.CurPosition;
                    positioner.Value.CurrentSpeed = deviceInfo.statusCalibrated_t.CurSpeed;

                    _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Updated state for device {positioner.Value.Name}, CurrentPos: {positioner.Value.CurrentPosition} CurrentSpeed: {positioner.Value.CurrentSpeed} Accel: {positioner.Value.Acceleration} Decel: {positioner.Value.Deceleration} Speed: {positioner.Value.Speed}  ");
                }
            }
            return Task.CompletedTask;
        }

        protected override Task AddSyncInAction(Command command, SemaphoreSlim semaphore)
        {
            var deviceNames = command.TargetDevices;
            
            if(command.Parameters is AddSyncInActionParameters parameters)
            {
                for (int i = 0; i < deviceNames.Length; i++)
                {
                    var deviceName = deviceNames[i];
                    var targetPosition = parameters.MovementInformation[deviceName].Position;
                    var allocatedTime = parameters.MovementInformation[deviceName].Time;    // [s]
                    var velocity = parameters.MovementInformation[deviceName].Velocity;    // [s]
                    var syncInAction = new command_add_sync_in_action_calb_t
                    {
                        Position = targetPosition,
                        Time = (uint)Math.Round(allocatedTime * 1000000),
                    };

                    CallResponse = API.command_add_sync_in_action_calb(_deviceInfo[deviceName].id, ref syncInAction, ref _deviceInfo[deviceName].calibration_t);
                    _log.Enqueue($"ximc: added ASIA to {deviceName} . Position: {syncInAction.Position};   Speed: {velocity}    Time: {allocatedTime}.");
                }
            }
            return Task.CompletedTask;
        }
        protected override Task<int> GetBufferFreeSpace(Command command, SemaphoreSlim semaphore)
        {
            return Task.Run(() =>
            {
                if (command.Parameters is GetBufferCountParameters getBufferSpaceCountParameters)
                {
                    var deviceName = getBufferSpaceCountParameters.Device;
                    if (deviceName != char.MinValue)
                    {
                        var deviceInfo = _deviceInfo[deviceName];
                        CallResponse = API.get_status_calb(deviceInfo.id, out deviceInfo.statusCalibrated_t, ref deviceInfo.calibration_t);
                        int count = (int)deviceInfo.statusCalibrated_t.CmdBufFreeSpace;
                        
                        Devices[deviceName].CurrentPosition = deviceInfo.statusCalibrated_t.CurPosition;
                        Devices[deviceName].CurrentSpeed = deviceInfo.statusCalibrated_t.CurSpeed;
                        return count;
                    }
                }
                return 0;
            });
        }






    }
}
