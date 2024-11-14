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
        private ConcurrentQueue<Command[]> _commandQueue = new ConcurrentQueue<Command[]>();
        private ConcurrentQueue<string> _log;
        private CommandManagerState _currentState = CommandManagerState.Waiting;
        private string _currentQueueController = string.Empty;
        private bool _allowedToRun = true;
        public CommandManager(ControllerManager manager, ConcurrentQueue<string> log)
        {
            this._controllerManager = manager;
            _log = log;

            foreach (var (controllerName, controller) in _controllerManager.Controllers)
            {
                _controllerManager.ControllerLocks[controllerName].Wait();
                try
                {
                    controller.InitializeController(_controllerManager.ControllerLocks[controllerName], _log);
                }
                finally
                {
                _controllerManager.ControllerLocks[controllerName].Release();
                }
            }
        }

        public CommandManagerState CurrentState
        {
            get { return _currentState; }
            private set
            {
                _currentState = value;
                //_// log.Enqueue($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}: State changed to {_currentState}");
            }
        }
        public void ClearLog()
        {
            _log.Clear();
        }
        public void EnqueueCommandLine(Command[] commands)
        {
            _commandQueue.Enqueue(commands);
        }

        public IEnumerable<string> GetLog()
        {
            return _log;
        }
        
        public async Task ProcessQueue()
        {
            _allowedToRun = true;
            _log.Enqueue("ProcessingQueue.");
            CurrentState = CommandManagerState.Processing;
            while (_commandQueue.Count > 0 && _allowedToRun)
            {
                if (_commandQueue.TryDequeue(out Command[] commandLine))
                {
                    // Group commands by their target controller

                    await ExecuteCommandLine(commandLine);
                    // Wait for all commands to complete
                   
                }
            }

            await CheckAndUpdateControllerQueue(String.Empty);

            CurrentState = CommandManagerState.Waiting;
            _log.Enqueue("QueueEnd in command manager.");

        }


        public async Task ExecuteCommandLine(Command[] commandLine)
        {
            var commandsByController = commandLine
                        .GroupBy(c => c.TargetController)
                        .ToDictionary(g => g.Key, g => g.ToArray());

            // Get all unique controller names in this command line
            var controllerNames = commandsByController.Keys.ToList();

            var commandsByMasterController = commandLine
                        .GroupBy
                        (
                        kvp =>
                            _controllerManager.Controllers[kvp.TargetController].MasterController == null
                                ? kvp.TargetController
                                : _controllerManager.Controllers[kvp.TargetController].MasterController.Name
                        )
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
                if (_controllerManager.ControllerLocks.TryGetValue(controllerName, out SemaphoreSlim semaphore))
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
                    await masterController.ExecuteSlaveCommandsAsync(commands, semaphore, _log);
                }
                catch (Exception ex)
                {
                    _log.Enqueue($"{DateTime.Now}: Error executing command on controller {controllerName}: {ex.Message}");
                    throw;
                }
            }
            else
            {
                for (int i = 0; i < commands.Length; i++)
                {
                    try
                    {
                        await controller.ExecuteCommandAsync(commands[i], semaphore, _log);
                    }
                    catch (Exception ex)
                    {
                        _log.Enqueue($"{DateTime.Now}: Error executing command on controller {controllerName}: {ex.Message}");
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
                await semaphore.WaitAsync();
                if (_controllerManager.Controllers[_currentQueueController] is BaseMasterController queuedMasterController)
                {
                    await queuedMasterController.AwaitQueuedItems(semaphore, new Dictionary<string, SemaphoreSlim>(), _log);
                }
                else
                {
                    await _controllerManager.Controllers[_currentQueueController].AwaitQueuedItems(semaphore, _log);
                }
            }
            if (_controllerManager.Controllers.ContainsKey(controllerName) && _controllerManager.Controllers[controllerName].IsQuable)
            {
                _currentQueueController = controllerName;
            }
        }

        public void PrintLog()
        {
            while (_log.TryDequeue(out string logEntry))
            {
                Console.WriteLine(logEntry);
            }
        }

        public async void Stop()
        {
            _allowedToRun = false;
            CurrentState = CommandManagerState.Waiting;

            // First stop the child controllers. Then let the master finish stopping everything.

            foreach (var (controllerName, controller) in _controllerManager.Controllers)
            {
                if (controller.MasterController is not null)
                {
                    await _controllerManager.ControllerLocks[controllerName].WaitAsync();
                    await controller.Stop(_controllerManager.ControllerLocks[controllerName], _log);

                    //if (_controllerManager.ControllerLocks[controllerName].CurrentCount == 0)
                    _controllerManager.ControllerLocks[controllerName].Release();
                }
            }

            foreach (var (controllerName, controller) in _controllerManager.Controllers)
            {
                if (controller.MasterController is BaseMasterController baseMasterController)
                {
                    var ackquiredSemaphores = await GatherSemaphoresForController([baseMasterController.Name]);
                    try
                    {
                        await controller.Stop(ackquiredSemaphores[baseMasterController.Name], _log);
                    }
                    finally
                    {
                        ReleaseSemeaphores(ackquiredSemaphores);
                    }
                }
            }


        }

        public void ClearQueue()
        {
            _commandQueue.Clear();
            CurrentState = CommandManagerState.Waiting;
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


        public IEnumerable<Command[]> GetCommandQueueList()
        {
            return [.. _commandQueue];
        }

    }
}