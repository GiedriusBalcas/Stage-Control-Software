using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;

namespace standa_controller_software.command_manager
{
    public class ControllerStateUpdater
    {
        private readonly ControllerManager _controllerManager;
        private readonly ConcurrentQueue<BaseDevice> _updatedDevicesQueue = new ConcurrentQueue<BaseDevice>();
        private readonly ConcurrentQueue<string> _log;

        // Event triggered when a device is updated
        public event EventHandler<BaseDevice> DeviceUpdated;

        public ControllerStateUpdater(ControllerManager controllerManager, ConcurrentQueue<string> log)
        {
            _controllerManager = controllerManager;
            _log = log;

            // Subscribe to unobserved task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                _log.Enqueue($"Unobserved exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
                e.SetObserved(); // Prevents the process from crashing
            };
        }

        public async Task UpdateStatesAsync()
        {
            string currentControllerName = null;

            try
            {
                _log.Enqueue("Starting UpdateStatesAsync loop.");

                while (true)
                {
                    //_log.Enqueue("Beginning of loop iteration.");

                    // Loop through all controllers
                    foreach (var controllerPair in _controllerManager.Controllers)
                    {
                        var controller = controllerPair.Value;
                        currentControllerName = controller.Name; // Keep track of the current controller
                        var semaphore = _controllerManager.ControllerLocks[controller.Name];

                        var updateCommand = new Command
                        {
                            TargetController = controller.Name,
                            Action = CommandDefinitions.UpdateState,
                        };

                        // Log attempting to acquire semaphore
                        //_log.Enqueue($"Attempting to acquire semaphore for controller: {controller.Name}");

                        // Attempt to acquire the semaphore without waiting
                        if (await semaphore.WaitAsync(0))
                        {
                            //_log.Enqueue($"Semaphore acquired for controller: {controller.Name}");

                            try
                            {
                                if (!controller.GetDevices().Any(device => !device.IsConnected))
                                {
                                    //_log.Enqueue($"Starting state update for controller: {controller.Name}");

                                    // Use Task.Run to run the update on a separate thread
                                    var updateTask = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await controller.ExecuteCommandAsync(updateCommand, semaphore);
                                            //_log.Enqueue($"Successfully updated state for controller: {controller.Name}");
                                        }
                                        catch (Exception ex)
                                        {
                                            _log.Enqueue($"Exception in {controller.Name}'s UpdateStatesAsync: {ex.Message}\n{ex.StackTrace}");
                                            throw;
                                        }
                                    });

                                    var timeoutTask = Task.Delay(1000); // 1 second timeout

                                    var completedTask = await Task.WhenAny(updateTask, timeoutTask);

                                    if (completedTask == timeoutTask)
                                    {
                                        _log.Enqueue($"Stuck on updating: {controller.Name}");
                                        // Optionally, implement cancellation if UpdateStatesAsync supports it
                                    }
                                    else
                                    {
                                        // Await the updateTask to observe any exceptions
                                        await updateTask;
                                    }
                                }
                                else
                                {
                                    //_log.Enqueue($"Skipping state update for controller {controller.Name} because a device is not connected.");
                                }
                            }
                            catch (Exception ex)
                            {
                                _log.Enqueue($"Error encountered during state update of {controller.Name}: {ex.Message}\n{ex.StackTrace}");
                            }
                            finally
                            {
                                semaphore.Release();
                                //_log.Enqueue($"Semaphore released for controller: {controller.Name}");
                            }
                        }
                        else
                        {
                            //_log.Enqueue($"Could not acquire semaphore for controller: {controller.Name}");
                        }
                    }

                    //_log.Enqueue("End of loop iteration.");

                    // Introduce a small delay to avoid overwhelming the system
                    await Task.Delay(20);
                }
            }
            catch (Exception ex)
            {
                _log.Enqueue($"Exception in UpdateStatesAsync loop for controller {currentControllerName}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _log.Enqueue("Exiting UpdateStatesAsync loop.");
            }
        }
    }
}
