using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using ximcWrapper;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
using System.Runtime.ExceptionServices;

namespace standa_controller_software.command_manager
{
    public enum CommandManagerState
    {
        Processing,
        Waiting
    }
    public class AsyncQueue<T>
    {
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);

        public void Enqueue(T item)
        {
            lock (_queue)
            {
                _queue.Enqueue(item);
            }
            _semaphore.Release();
        }

        public async Task<T> DequeueAsync()
        {
            await _semaphore.WaitAsync();
            lock (_queue)
            {
                return _queue.Dequeue();
            }
        }

        public async Task<bool> WaitForNextItemAsync()
        {
            if (_semaphore.CurrentCount > 0)
            {
                return true;
            }

            await _semaphore.WaitAsync();
            return true;
        }
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
                controller.InitializeController(_controllerManager.ControllerLocks[controllerName], _log);
                _controllerManager.ControllerLocks[controllerName].Release();
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
        public void Start()
        {
            CurrentState = CommandManagerState.Processing;
            Task.Run(() => ProcessQueue());
        }
        public async Task ProcessQueue()
        {
            _allowedToRun = true;
            _log.Enqueue("ProcessingQueue.");
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

            // Wait for and acquire all necessary semaphores for the controllers
            Dictionary<string, SemaphoreSlim> controllerSemaphores = new Dictionary<string, SemaphoreSlim>();
            
            foreach (var controllerName in controllerNames)
            {
                await _controllerManager.ControllerLocks[controllerName].WaitAsync();
                controllerSemaphores[controllerName] = _controllerManager.ControllerLocks[controllerName];
            }
            // Execute all commands for each controller group


            var commandsByMasterController = commandLine
                        .GroupBy
                        ( 
                        kvp => 
                            _controllerManager.Controllers[ kvp.TargetController ].MasterController == null 
                                ? kvp.TargetController 
                                : _controllerManager.Controllers[kvp.TargetController].MasterController.Name
                        )
                        .ToDictionary(g => g.Key, g => g.ToArray());

            var masterControllerNames = commandsByMasterController.Keys.ToList();

            var executeTasks = masterControllerNames.Select(async controllerName =>
            {
                var commands = commandsByMasterController[controllerName];
                var semaphore = _controllerManager.ControllerLocks[controllerName];

                await CheckAndUpdateControllerQueue(controllerName);

                if (_controllerManager.Controllers[controllerName] is BaseMasterController masterController)
                {
                    var slaveSemaphores = new Dictionary<string, SemaphoreSlim>();
                    foreach (var (slaveControllerName, slaveController) in masterController.SlaveControllers)
                    {
                        slaveSemaphores[slaveControllerName] = _controllerManager.ControllerLocks[slaveControllerName];
                    }

                    await ExecuteCommandsForMasterControllerAsync(commands, controllerName, semaphore, slaveSemaphores);

                    foreach (var (slaveControllerName, slaveSemaphore) in slaveSemaphores)
                    {
                        if (slaveSemaphore.CurrentCount == 0)
                            slaveSemaphore.Release();
                    }
                    if (semaphore.CurrentCount == 0)
                        semaphore.Release();
                }
                else
                {
                    await ExecuteCommandsForControllerAsync(commands, controllerName, semaphore);
                    if (semaphore.CurrentCount == 0)
                        semaphore.Release();
                }


            });

            await Task.WhenAll(executeTasks);

        }
        private async Task ExecuteCommandsForMasterControllerAsync(Command[] commands, string controllerName, SemaphoreSlim semaphore, Dictionary<string, SemaphoreSlim> slaveSemaphores)
        {
            if (_controllerManager.Controllers[controllerName] is BaseMasterController masterController)
            {
                await masterController.ExecuteSlaveCommandsAsync(commands, semaphore, slaveSemaphores, _log);
            }
        }
        private async Task ExecuteCommandsForControllerAsync(Command[] commands, string controllerName, SemaphoreSlim semaphore)
        {

            for (int i = 0; i < commands.Length; i++)
            {
                try
                {
                    await _controllerManager.Controllers[controllerName].ExecuteCommandAsync(commands[i], semaphore, _log);
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"{DateTime.Now}: Error executing command on controller {controllerName}: {ex.Message}");
                    throw;
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

        //public async Task UpdateStatesAsync()
        //{
        //    while (true)
        //    {
        //        var tasks = new List<Task>();

        //        foreach (var controllerPair in _controllerManager.Controllers)
        //        {

        //            var controller = controllerPair.Value;
        //            var semaphore = _controllerManager.ControllerLocks[controller.Name];
        //            if (semaphore.CurrentCount > 0)
        //            {
        //                //await semaphore.WaitAsync();

        //                var task = Task.Run(async () =>
        //                {
        //                    await controller.UpdateStatesAsync(_log);
        //                    //if (semaphore.CurrentCount == 0)
        //                    //    semaphore.Release();
        //                });
        //                tasks.Add(task);
        //            }

        //        }
        //        await Task.WhenAll(tasks);
        //        await Task.Delay(50);
        //    }
        //}


        public async Task UpdateStatesAsync()
        {
            while (true)
            {
                var tasks = new List<Task>();

                foreach (var controllerPair in _controllerManager.Controllers)
                {
                    var controller = controllerPair.Value;
                    var semaphore = _controllerManager.ControllerLocks[controller.Name];

                    // Attempt to acquire the semaphore without waiting
                    if (await semaphore.WaitAsync(0))
                    {
                        // Define an asynchronous method to update and release
                        async Task UpdateAndReleaseAsync()
                        {
                            try
                            {
                                await controller.UpdateStatesAsync(_log);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }

                        tasks.Add(UpdateAndReleaseAsync());
                    }
                    // If semaphore wasn't acquired, skip this controller
                }

                await Task.WhenAll(tasks);
                await Task.Delay(50);
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

                    if (_controllerManager.ControllerLocks[controllerName].CurrentCount == 0)
                        _controllerManager.ControllerLocks[controllerName].Release();
                }
            }

            foreach (var (controllerName, controller) in _controllerManager.Controllers)
            {
                if (controller.MasterController is null)
                {
                    //await _controllerManager.ControllerLocks[controllerName].WaitAsync();
                    await controller.Stop(_controllerManager.ControllerLocks[controllerName], _log);

                    if (_controllerManager.ControllerLocks[controllerName].CurrentCount == 0)
                        _controllerManager.ControllerLocks[controllerName].Release();
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

        private string FormatParameters(object[][] parameters)
        {
            var formattedParameters = parameters
                .Select(paramArray =>
                {
                    if (paramArray == null)
                    {
                        return "[null]";
                    }
                    return $"[{string.Join(", ", paramArray.Select(p => p?.ToString() ?? "null"))}]";
                });

            return string.Join(" ", formattedParameters); // Join all sub-arrays with a space
        }

        public IEnumerable<Command[]> GetCommandQueueList()
        {
            return [.. _commandQueue];
        }

    }
}