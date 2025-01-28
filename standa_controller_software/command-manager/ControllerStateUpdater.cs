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

        public ControllerStateUpdater(ControllerManager controllerManager, ILogger<ControllerStateUpdater> logger)
        {
            _controllerManager = controllerManager;
            _logger = logger;

            // Subscribe to unobserved task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                _logger.LogInformation($"Unobserved exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
                e.SetObserved();
            };
        }

        public async Task UpdateStatesAsync()
        {
            string currentControllerName = "";

            try
            {
                _logger.LogInformation("Starting UpdateStatesAsync loop.");

                while (true)
                {
                    foreach (var controllerPair in _controllerManager.Controllers)
                    {
                        var controller = controllerPair.Value;
                        currentControllerName = controller.Name;
                        var semaphore = _controllerManager.ControllerLocks[controller.Name];

                        var updateCommand = new Command
                        {
                            TargetController = controller.Name,
                            TargetDevices = controller.GetDevices().Select(device => device.Name).ToArray(),
                            Parameters = controller.Name,
                            Action = CommandDefinitions.UpdateState,
                        };

                        if (await semaphore.WaitAsync(0))
                        {
                            try
                            {
                                if (!controller.GetDevices().Any(device => !device.IsConnected))
                                {
                                    var updateTask = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await controller.ExecuteCommandAsync(updateCommand, semaphore);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogInformation($"Exception in {controller.Name}'s UpdateStatesAsync: {ex.Message}\n{ex.StackTrace}");
                                        }
                                    });

                                    var timeoutTask = Task.Delay(1000); // 1 second timeout

                                    var completedTask = await Task.WhenAny(updateTask, timeoutTask);

                                    if (completedTask == timeoutTask)
                                    {
                                        _logger.LogInformation($"Stuck on updating: {controller.Name}");
                                    }
                                    else
                                    {
                                        await updateTask;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation($"Error encountered during state update of {controller.Name}: {ex.Message}\n{ex.StackTrace}");
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }
                    }
                    await Task.Delay(40);
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
