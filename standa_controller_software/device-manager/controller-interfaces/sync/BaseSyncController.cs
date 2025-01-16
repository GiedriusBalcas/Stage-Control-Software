using Microsoft.Extensions.Logging;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library.Common;
using standa_controller_software.command_manager.command_parameter_library.Synchronization;
using standa_controller_software.device_manager.controller_interfaces.shutter;
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

        public struct ExecutionInformation
        {
            public char[] Devices;
            public bool Launch;
            public float Rethrow;
            public bool Shutter;
            public float Shutter_delay_on;
            public float Shutter_delay_off;
        }
        public BaseSyncController(string name, ILoggerFactory loggerFactory) : base(name, loggerFactory)
        {
            _logger = _loggerFactory.CreateLogger<BaseSyncController>();
            
            _methodMap[CommandDefinitions.AddSyncControllerBufferItem] = new MethodInformation()
            {
                MethodHandle = AddSyncBufferItem,
            };
            _methodMap[CommandDefinitions.StartQueueExecution] = new MethodInformation()
            {
                MethodHandle = StartQueueExecution,
            };
            _methodMap[CommandDefinitions.GetBufferCount] = new MethodInformation<int>()
            {
                MethodHandle = GetBufferCount,
            };
        }

        public override BaseController GetVirtualCopy()
        {
            var virtualCopy = new SyncController_Virtual(Name, _loggerFactory)
            {
                ID  = this.ID,
                MasterController = this.MasterController,
            };

            return virtualCopy;
        }
        public override List<BaseDevice> GetDevices()
        {
            return new List<BaseDevice>();
        }
        public override void AddDevice(BaseDevice device)
        {
        }

        protected override Task ConnectDevice(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected override abstract Task Stop(Command command, SemaphoreSlim semaphore);
        protected override abstract Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore);
        protected async Task AddSyncBufferItem(Command command, SemaphoreSlim semaphore) 
        {
            if(command.Parameters is AddSyncControllerBufferItemParameters bufferItemParameters)
            {
                await AddSyncBufferItem_implementation(
                        bufferItemParameters.Devices,
                        bufferItemParameters.Launch,
                        bufferItemParameters.Rethrow,
                        bufferItemParameters.Shutter,
                        bufferItemParameters.ShutterDelayOn,
                        bufferItemParameters.ShutterDelayOff
                    );
            }
        }
        protected abstract Task StartQueueExecution(Command command, SemaphoreSlim semaphore);
        protected abstract Task<int> GetBufferCount(Command command, SemaphoreSlim semaphore);

        protected abstract Task ConnectDevice_implementation(BaseDevice device);
        protected abstract Task AddSyncBufferItem_implementation(char[] Devices, bool Launch, float Rethrow, bool Shutter, float Shutter_delay_on, float Shutter_delay_off);

    }
}
