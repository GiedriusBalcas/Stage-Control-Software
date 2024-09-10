using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public abstract class BaseMasterController : BaseController
    {
        protected class MultiControllerMethodInformation()
        {
            public Func<Command[], Dictionary<string, SemaphoreSlim>, ConcurrentQueue<string>, Task> MethodHandle;
            public Func<Dictionary<string, Command>, ConcurrentQueue<string>, Task> AWaitAsync;
            public bool Quable = false;
            public MethodState State = MethodState.Free;
        }

        protected Dictionary<CommandDefinitions, MultiControllerMethodInformation> _methodMap_multiControntroller = new Dictionary<CommandDefinitions, MultiControllerMethodInformation>();

        protected BaseMasterController(string name) : base(name)
        {
        }

        public abstract Task ExecuteSlaveCommandsAsync(Command[] commands, Dictionary<string, SemaphoreSlim> semaphores, ConcurrentQueue<string> log);
        public abstract Task AwaitQueuedItems(Dictionary<string, SemaphoreSlim> semaphores, ConcurrentQueue<string> log);

    }
}
