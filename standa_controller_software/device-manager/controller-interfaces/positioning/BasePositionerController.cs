using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library.Common;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.positioning
{
    public abstract class BasePositionerController : BaseController
    {

        protected ConcurrentDictionary<char, CancellationTokenSource> deviceCancellationTokens = new ConcurrentDictionary<char, CancellationTokenSource>();
        protected Dictionary<char, BasePositionerDevice> Devices { get; }


        public BasePositionerController(string name, ConcurrentQueue<string> log) : base(name, log)
        {
            _methodMap[CommandDefinitions.MoveAbsolute] = new MethodInformation()
            {
                MethodHandle = MoveAbsolute,
            };
            _methodMap[CommandDefinitions.UpdateMoveSettings] = new MethodInformation()
            {
                MethodHandle = UpdateMoveSettings,
            };

            _methodMap[CommandDefinitions.AddSyncInAction] = new MethodInformation()
            {
                MethodHandle = AddSyncInAction,
            };
            _methodMap[CommandDefinitions.GetBufferCount] = new MethodInformation<int>()
            {
                MethodHandle = GetBufferFreeSpace,
            };

            Devices = new Dictionary<char, BasePositionerDevice>();
            //methodMap["UpdateStates"] = UpdateStatesCall;
        }

        public override void AddDevice(BaseDevice device)
        {
            if (device is BasePositionerDevice positioningDevice)
            {
                Devices.Add(positioningDevice.Name, positioningDevice);
            }
            else
                throw new Exception($"Unable to add device: {device.Name}. Controller {this.Name} only accepts positioning devices.");
        }
        public override List<BaseDevice> GetDevices()
        {
            return Devices.Values.Cast<BaseDevice>().ToList();
        }
        public override BaseController GetVirtualCopy()
        {
            var virtualController = new PositionerController_Virtual(Name, _log)
            {
                MasterController = this.MasterController,
            };
            foreach (var (deviceName,device) in Devices)
            {
                virtualController.AddDevice(device.GetCopy());
            }

            return virtualController;
        }

        protected override Task ConnectDevice(Command command, SemaphoreSlim semaphore)
        {
            if (command.Parameters is ConnectDevicesParameters connectDevicesParameters)
            {
                var deviceNames = connectDevicesParameters.Devices;
                foreach (var deviceName in deviceNames)
                {

                    var device = Devices[deviceName];
                    ConnectDevice_implementation(device);
                    device.IsConnected = true;
                }
            }

            return Task.CompletedTask;
        }
        protected override abstract Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore);
        protected abstract Task MoveAbsolute(Command command, SemaphoreSlim semaphore);
        protected abstract Task UpdateMoveSettings(Command command, SemaphoreSlim semaphore);
        protected abstract void ConnectDevice_implementation(BaseDevice device);

        protected abstract Task AddSyncInAction(Command command, SemaphoreSlim semaphore);
        protected abstract Task<int> GetBufferFreeSpace(Command command, SemaphoreSlim semaphore);
    }
}
