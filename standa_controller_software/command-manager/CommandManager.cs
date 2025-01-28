using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using ximcWrapper;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
using System.Runtime.ExceptionServices;
using System.Xml.Linq;
using OpenTK.Platform.Windows;
using OpenTK.Graphics.OpenGL;
using Microsoft.Extensions.Logging;
using System.Numerics;

namespace standa_controller_software.command_manager
{
    public enum CommandManagerState
    {
        Processing,
        Waiting
    }
    
    public class CommandManager
    {
        private readonly ControllerManager _controllerManager;
        private readonly ILogger<CommandManager> _logger;
        private ConcurrentQueue<Command[]> _commandQueue = new ConcurrentQueue<Command[]>();
        private CommandManagerState _currentState = CommandManagerState.Waiting;
        public event Action<CommandManagerState>? OnStateChanged;
        public CommandManagerState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    // Fire the event whenever the state changes
                    OnStateChanged?.Invoke(_currentState);
                }
            }
        }

        private string _currentQueueController = string.Empty;
        private bool _allowedToRun = true;
        public CommandManager(ControllerManager controllerManager, ILogger<CommandManager> logger)
        {
            this._controllerManager = controllerManager;
            _logger = logger;

            foreach (var (controllerName, controller) in _controllerManager.Controllers)
            {
                var initializeCommand = new Command
                {
                    TargetController = controller.Name,
                    TargetDevices = [],
                    Parameters = controller.Name,
                    Action = CommandDefinitions.Initialize,
                };
                ExecuteControllerCommandWrapper(initializeCommand).GetAwaiter().GetResult();
            }
        }


        // interface for outside objects to execute commands.

        public IEnumerable<Command[]> GetCommandQueueList()
        {
            return [.. _commandQueue];
        }
        public string GetCommandQueueAsString()
        {
            var csvStringBuilder = new StringBuilder();
            var queueSnapshot = _commandQueue.ToArray();

            foreach (var commandArray in queueSnapshot)
            {
                for (int i = 0; i < commandArray.Length; i++)
                {
                    var command = commandArray[i];
                    csvStringBuilder.Append($"{command.TargetController},{string.Join(" ", command.TargetDevices)},{command.Action},{command.Parameters}");

                    if (i < commandArray.Length - 1)
                    {
                        csvStringBuilder.Append(" & ");
                    }
                    else
                    {
                        csvStringBuilder.Append(" ;");
                    }
                }
                csvStringBuilder.AppendLine();
            }

            return csvStringBuilder.ToString();
        }
        public void ClearQueue()
        {
            _commandQueue.Clear();
            CurrentState = CommandManagerState.Waiting;
        }

        public async Task Stop()
        {
            _allowedToRun = false;
            CurrentState = CommandManagerState.Waiting;

            // First stop the child controllers. Then let the master finish stopping everything.

            foreach (var (controllerName, controller) in _controllerManager.Controllers)
            {
                if (controller.MasterController is not null)
                {
                    var stopCommand = new Command
                    {
                        TargetController = controllerName,
                        TargetDevices = controller.GetDevices().Select(device => device.Name).ToArray(),
                        Parameters = controllerName,
                        Action = CommandDefinitions.Stop,

                    };
                    //await ExecuteControllerCommandWrapper(stopCommand);
                    await controller.ForceStop();
                }
            }

            foreach (var (controllerName, controller) in _controllerManager.Controllers)
            {
                if (controller is BaseMasterController baseMasterController)
                {
                    var stopCommand = new Command
                    {
                        TargetController = controllerName,
                        TargetDevices = controller.GetDevices().Select(device => device.Name).ToArray(),
                        Parameters = controllerName,
                        Action = CommandDefinitions.Stop,
                    };
                    await baseMasterController.ForceStop();

                }
            }

            CurrentState = CommandManagerState.Waiting;

        }
        public async Task ProcessQueue()
        {
            _allowedToRun = true;
            _logger.LogInformation("ProcessingQueue.");
            CurrentState = CommandManagerState.Processing;
            while (_commandQueue.Count > 0 && _allowedToRun)
            {
                if (_commandQueue.TryDequeue(out var commandLine))
                {
                    // Group commands by their target controller

                    await ExecuteCommandLine(commandLine);
                    // Wait for all commands to complete
                   
                }
            }
            if(_allowedToRun)
                await CheckAndUpdateControllerQueue(String.Empty);

            CurrentState = CommandManagerState.Waiting;
            _logger.LogInformation("QueueEnd in command manager.");

        }
        public void EnqueueCommandLine(Command[] commands)
        {
            _commandQueue.Enqueue(commands);
        }
        public async Task TryExecuteCommand(Command command)
        {
            await ExecuteControllerCommandWrapper(command);
        }
        public async Task TryExecuteCommandLine(Command[] commandLine)
        {
            if(CurrentState == CommandManagerState.Waiting)
            {
                CurrentState = CommandManagerState.Processing;

                await ExecuteCommandLine(commandLine);
                await CheckAndUpdateControllerQueue(String.Empty);

                CurrentState = CommandManagerState.Waiting;
            }
            else
            {
                throw new Exception("Unable to execute command line.");
            }
        }

        private async Task ExecuteCommandLine(Command[] commandLine)
        {
            var commandsByController = commandLine
                        .GroupBy(c => c.TargetController)
                        .ToDictionary(g => g.Key, g => g.ToArray());

            // Get all unique controller names in this command line
            var controllerNames = commandsByController.Keys.ToList();

            var commandsByMasterController = commandLine
                        .GroupBy(kvp =>
                        {
                            var controller = _controllerManager.Controllers[kvp.TargetController];
                            var masterController = controller.MasterController;
                            return masterController == null ? kvp.TargetController : masterController.Name;
                        })
                        .ToDictionary(g => g.Key, g => g.ToArray());

            // check if theres a queued controller
            // lets try to handle the quable first, then the non quable as they go in the command line
            var quableControllers = commandsByMasterController.Keys.Where(name => _controllerManager.Controllers[name] is IQuableController).ToList();

            if (_currentQueueController != null)
            {
                if (quableControllers.Contains(_currentQueueController))
                {
                    quableControllers.Remove(_currentQueueController);
                    var controllerName = _currentQueueController;
                    var commands = commandsByMasterController[controllerName];

                    await ExecuteCommandLineGroup(new Dictionary<string, Command[]>
                        {
                            { controllerName, commands }
                        });

                    commandsByMasterController.Remove(controllerName);
                }
            }
            for (var index = 0; index < quableControllers.Count; index++)
            {
                var controllerName = quableControllers[index];
                var commands = commandsByMasterController[controllerName];

                await CheckAndUpdateControllerQueue(controllerName);
                
                // execute its commands.
                await ExecuteCommandLineGroup(new Dictionary<string, Command[]>
                        {
                            { controllerName, commands }
                        });

                commandsByMasterController.Remove(controllerName);
            }

            // and now just move to the other controllers.
            if (commandsByMasterController.Count < 1)
                return;

            await CheckAndUpdateControllerQueue(string.Empty);
            await ExecuteCommandLineGroup(commandsByMasterController);

        }
        private async Task ExecuteCommandLineGroup(Dictionary<string, Command[]> commandsByMasterController)
        {
            // gathering all the semaphores needed for the command execution before starting execution.
            // in the case of master controller, we will need to gather all of its slave semaphores.

            var controllerNames = commandsByMasterController.Keys.ToList();
            
            var ackquiredSemaphores = await GatherSemaphoresForController(controllerNames);
            

            
                var executeTasks = commandsByMasterController.Keys.Select(async controllerName =>
                {
                    var commands = commandsByMasterController[controllerName];
                    var semaphore = ackquiredSemaphores[controllerName];
                    try
                    {
    
                    await ExecuteCommandsForControllerAsync(commands, controllerName, semaphore);

                    }
                    finally
                    {
                        ReleaseSemeaphores(new Dictionary<string, SemaphoreSlim>
                        {
                            { controllerName, semaphore }
                        });
                    }
                });

                await Task.WhenAll(executeTasks);
            
        }
        private static void ReleaseSemeaphores(Dictionary<string, SemaphoreSlim> ackquiredSemaphores)
        {
            foreach (var (controllerName, semaphore) in ackquiredSemaphores)
            {
                semaphore.Release();
            }
        }
        private async Task<Dictionary<string, SemaphoreSlim>> GatherSemaphoresForController(List<string> controllerNames)
        {
            var ackquiredSemaphores = new Dictionary<string, SemaphoreSlim>();
            foreach (var controllerName in controllerNames)
            {
                var controller = _controllerManager.Controllers[controllerName];
                if (_controllerManager.ControllerLocks.TryGetValue(controllerName, out var semaphore))
                {
                    await semaphore.WaitAsync();
                    ackquiredSemaphores[controllerName] = semaphore;
                }
                else
                {
                    throw new KeyNotFoundException($"Semaphore for controller '{controllerNames}' not found.");
                }
            }

            return ackquiredSemaphores;
        }
        private async Task ExecuteCommandsForControllerAsync(Command[] commands, string controllerName, SemaphoreSlim semaphore)
        {
            var controller = _controllerManager.Controllers[controllerName];

            if(controller is BaseMasterController masterController)
            {
                try
                {
                    await masterController.ExecuteSlaveCommandsAsync(commands, semaphore);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"{DateTime.Now}: Error executing command on controller {controllerName}: {ex.Message}");
                    throw;
                }
            }
            else
            {
                for (int i = 0; i < commands.Length; i++)
                {
                    try
                    {
                        await controller.ExecuteCommandAsync(commands[i], semaphore);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"{DateTime.Now}: Error executing command on controller {controllerName}: {ex.Message}");
                        throw;
                    }
                }
            }
        }
        private async Task CheckAndUpdateControllerQueue(string controllerName)
        {
            if (_currentQueueController != string.Empty && _currentQueueController != controllerName)
            {
                var semaphore = _controllerManager.ControllerLocks[_currentQueueController];
                var controllerQueued = _controllerManager.Controllers[_currentQueueController];
                await semaphore.WaitAsync();
                try
                {
                    if (controllerQueued is IQuableController queuedMasterController)
                    {
                        await queuedMasterController.AwaitQueuedItems(semaphore);
                    }
                    else
                    {
                        _logger.LogError($"Unexpected queued controller statement.");
                        throw new Exception("Unexpected queued controller statement.");
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
            if (_controllerManager.Controllers.ContainsKey(controllerName) && _controllerManager.Controllers[controllerName] is IQuableController)
            {
                _currentQueueController = controllerName;
            }
            else
            {
                _currentQueueController = string.Empty;
            }
        }
        private async Task ExecuteControllerCommandWrapper(Command command)
        {
            var controllerName = command.TargetController;
            if (controllerName != string.Empty && _controllerManager.Controllers.TryGetValue(controllerName, out var controller))
            {
                var controllerLock = await GatherSemaphoresForController([controllerName]);
                try
                {
                    await controller.ExecuteCommandAsync(command, controllerLock[controllerName]);
                }
                finally
                {
                    ReleaseSemeaphores(controllerLock);
                }
            }
            else
                throw new Exception("Unable to retrive controller in master controller.");

        }

    }
}