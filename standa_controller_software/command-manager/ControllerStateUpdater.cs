using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;

namespace standa_controller_software.command_manager
{
    public class ControllerStateUpdater
    {
        private readonly ControllerManager _controllerManager;
        private readonly ConcurrentQueue<BaseDevice> _updatedDevicesQueue = new ConcurrentQueue<BaseDevice>();
        private ConcurrentQueue<string> _log;

        // Event triggered when a device is updated
        public event EventHandler<BaseDevice> DeviceUpdated;

        public ControllerStateUpdater(ControllerManager controllerManager, ConcurrentQueue<string> log)
        {
            _controllerManager = controllerManager;
            _log = log;
        }

        public async Task UpdateStatesAsync()
        {
            while (true)
            {
                var tasks = new List<Task>();

                // Loop through all controllers
                foreach (var controllerPair in _controllerManager.Controllers)
                {
                    var controller = controllerPair.Value;
                    var semaphore = _controllerManager.ControllerLocks[controller.Name];

                    // Attempt to acquire the semaphore without waiting
                    if (await semaphore.WaitAsync(0))
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                // Perform the device state update as fast as possible
                                await controller.UpdateStatesAsync(_log);

                                // Release semaphore immediately after update
                            }
                            finally
                            {
                                semaphore.Release();
                            }

                            // Fetch the devices and enqueue them for further processing
                            var devices = controller.GetDevices();
                            foreach (var device in devices)
                            {
                                _updatedDevicesQueue.Enqueue(device);
                                // Notify listeners about the updated device
                                DeviceUpdated?.Invoke(this, device);
                            }
                        }));
                    }
                }

                // Wait for all tasks to complete before delaying the next cycle
                await Task.WhenAll(tasks);

                // Introduce a small delay to avoid overwhelming the system
                await Task.Delay(200);
            }
        }
    }
}
