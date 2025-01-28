using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;

namespace standa_controller_software.device_manager.controller_interfaces.sync
{
    public class SyncController_Virtual : BaseSyncController
    {
        public SyncController_Virtual(string name, ILoggerFactory loggerFactory) : base(name, loggerFactory)
        {
            _logger = _loggerFactory.CreateLogger<SyncController_Virtual>();
        }

        public override Task ForceStop()
        {
            return Task.CompletedTask;
        }
        protected override Task AddSyncBufferItem_implementation(char[] Devices, bool Launch, float Rethrow, bool Shutter, float Shutter_delay_on, float Shutter_delay_off)
        {
            throw new NotImplementedException();
        }

        protected override Task ConnectDevice_implementation(BaseDevice device)
        {
            return Task.CompletedTask;
        }

        protected override Task<int> GetBufferCount(Command command, SemaphoreSlim semaphore)
        {
            throw new NotImplementedException();
        }

        protected override Task StartQueueExecution(Command command, SemaphoreSlim semaphore)
        {
            throw new NotImplementedException();
        }

        protected override Task Stop(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }

        protected override Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            throw new NotImplementedException();
        }
    }
}
