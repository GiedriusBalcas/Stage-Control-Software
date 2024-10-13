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
        }

        public override abstract void AddDevice(BaseDevice device);
        public override abstract Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore);
        public override abstract BaseController GetCopy();
        public override List<BaseDevice> GetDevices()
        {
            return new List<BaseDevice>();
        }
        public override abstract Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        public override abstract Task UpdateStatesAsync(ConcurrentQueue<string> log);

    }
}
