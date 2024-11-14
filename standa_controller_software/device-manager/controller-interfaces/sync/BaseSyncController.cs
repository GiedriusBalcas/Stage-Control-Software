using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.sync
{
    public abstract class BaseSyncController : BaseController
    {
        public BaseSyncController(string name) : base(name)
        {
            _methodMap[CommandDefinitions.AddSyncInAction] = new MethodInformation()
            {
                MethodHandle = AddSyncInAction,
                Quable = false,
                State = MethodState.Free,
            };
        }

        public override abstract void AddDevice(BaseDevice device);
        public override abstract Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore);
        public override abstract BaseController GetCopy();
        public override List<BaseDevice> GetDevices()
        {
            return new List<BaseDevice>();
        }
        protected override abstract Task Stop(SemaphoreSlim semaphore);
        protected override abstract Task UpdateStatesAsync();
        protected override abstract Task AddSyncInAction();

    }
}
