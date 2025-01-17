using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;

namespace standa_controller_software.command_manager
{
    public class ControllerStateUpdater
    {
        private readonly ControllerManager _controllerManager;
        private readonly ILogger<ControllerStateUpdater> _logger;
        private readonly ConcurrentQueue<BaseDevice> _updatedDevicesQueue = new ConcurrentQueue<BaseDevice>();

        // Event triggered when a device is updated
        public event EventHandler<BaseDevice> DeviceUpdated;

        public ControllerStateUpdater(ControllerManager controllerManager, ILogger<ControllerStateUpdater> logger)
        {
            _controllerManager = controllerManager;
            _logger = logger;

            // Subscribe to unobserved task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                _logger.LogInformation($"Unobserved exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
                e.SetObserved(); // Prevents the process from crashing
            };
        }

        public async Task UpdateStatesAsync()
        {
            string currentControllerName = null;

            try
            {
                _logger.LogInformation("Starting UpdateStatesAsync loop.");

                while (true)
                {
                    //_logger.LogInformation("Beginning of loop iteration.");

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
                        //_logger.LogInformation($"Attempting to acquire semaphore for controller: {controller.Name}");

                        // Attempt to acquire the semaphore without waiting
                        if (await semaphore.WaitAsync(0))
                        {
                            //_logger.LogInformation($"Semaphore acquired for controller: {controller.Name}");

                            try
                            {
                                if (!controller.GetDevices().Any(device => !device.IsConnected))
                                {
                                    //_logger.LogInformation($"Starting state update for controller: {controller.Name}");

                                    // Use Task.Run to run the update on a separate thread
                                    var updateTask = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await controller.ExecuteCommandAsync(updateCommand, semaphore);
                                            //_logger.LogInformation($"Successfully updated state for controller: {controller.Name}");
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogInformation($"Exception in {controller.Name}'s UpdateStatesAsync: {ex.Message}\n{ex.StackTrace}");
                                            throw;
                                        }
                                    });

                                    var timeoutTask = Task.Delay(1000); // 1 second timeout

                                    var completedTask = await Task.WhenAny(updateTask, timeoutTask);

                                    if (completedTask == timeoutTask)
                                    {
                                        _logger.LogInformation($"Stuck on updating: {controller.Name}");
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
                                    //_logger.LogInformation($"Skipping state update for controller {controller.Name} because a device is not connected.");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation($"Error encountered during state update of {controller.Name}: {ex.Message}\n{ex.StackTrace}");
                            }
                            finally
                            {
                                semaphore.Release();
                                //_logger.LogInformation($"Semaphore released for controller: {controller.Name}");
                            }
                        }
                        else
                        {
                            //_logger.LogInformation($"Could not acquire semaphore for controller: {controller.Name}");
                        }
                    }

                    //_logger.LogInformation("End of loop iteration.");

                    // Introduce a small delay to avoid overwhelming the system
                    await Task.Delay(20);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Exception in UpdateStatesAsync loop for controller {currentControllerName}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _logger.LogInformation("Exiting UpdateStatesAsync loop.");
            }
        }
    }
}
