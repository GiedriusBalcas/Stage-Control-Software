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

        protected enum MethodState
        {
            Working,
            Free,
            Waiting
        }

        protected class MethodInformation()
        {
            public Func<Command, SemaphoreSlim, ConcurrentQueue<string>, Task> MethodHandle;
            public Func<SemaphoreSlim, ConcurrentQueue<string>, Task> AWaitAsync;
            public bool Quable = false;
            public MethodState State = MethodState.Free;
        }

        protected Dictionary<CommandDefinitionsLibrary, MethodInformation> _methodMap = new Dictionary<CommandDefinitionsLibrary, MethodInformation>();

        public BaseController? MasterController { get; set; } = null;
        public Dictionary<string, BaseController> SlaveControllers { get; set; } = new Dictionary<string, BaseController>();

        public bool IsQuable { get; set; } = false;

        [DisplayPropertyAttribute]
        public string Name { get;}
        protected BaseController(string name)
        {
            Name = name;
            SlaveControllers = new Dictionary<string, BaseController>();
        }
        public abstract Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        public abstract Task UpdateStatesAsync(ConcurrentQueue<string> log);
        public abstract void AddDevice(BaseDevice device);
        public abstract void AddSlaveController(BaseController controller);
        public abstract Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore);
        public abstract List<BaseDevice> GetDevices();
        public abstract BaseController GetCopy();

        public virtual Task AwaitQueuedItems(SemaphoreSlim semaphore, ConcurrentQueue<string> log) 
        {
            return Task.CompletedTask ;
        }

    }
}
