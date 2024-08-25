using standa_controller_software.command_manager;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces
{
    public abstract class BaseController
    {
        [DisplayPropertyAttribute]
        public string Name { get;}
        protected BaseController(string name)
        {
            Name = name;
        }
        public abstract Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        public abstract Task UpdateStateAsync(ConcurrentQueue<string> log);
        public abstract void AddDevice(BaseDevice device);
        public abstract void ConnectDevice(BaseDevice device, SemaphoreSlim semaphore);
        public abstract List<BaseDevice> GetDevices();
        public abstract BaseController GetCopy();

    }
}
