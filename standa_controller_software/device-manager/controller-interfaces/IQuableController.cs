using standa_controller_software.command_manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces
{
    public interface IQuableController
    {
        protected Task GetBufferFreeSpace(Command command, SemaphoreSlim semaphore);
        protected Task AwaitQueuedItems(Command command, SemaphoreSlim semaphore);
        protected Task AddBufferItem(Command command, SemaphoreSlim semaphore);
        protected Task OnSyncIn(Command command, SemaphoreSlim semaphore);
        

    }
}
