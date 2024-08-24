using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

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
        private ConcurrentQueue<string> _log = new ConcurrentQueue<string>();
        private bool _running = true;
        private CommandManagerState _currentState = CommandManagerState.Waiting;

        public CommandManager(ControllerManager manager)
        {
            this._controllerManager = manager;
        }

        public CommandManagerState CurrentState
        {
            get { return _currentState; }
            private set
            {
                _currentState = value;
                _log.Enqueue($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}: State changed to {_currentState}");
            }
        }

        public void EnqueueCommandLine(Command[] commands)
        {
            _commandQueue.Enqueue(commands);
        }

        public void Start()
        {
            _running = true;
            CurrentState = CommandManagerState.Processing;
            Task.Run(() => ProcessQueue());
        }
        private async Task ProcessQueue()
        {
            while (_commandQueue.Count > 0)
            {
                if (_commandQueue.TryDequeue(out Command[] commandLine))
                {
                    // Group commands by their target controller

                    await ExecuteCommandLine(commandLine);
                    // Wait for all commands to complete
                }
            }
        }


        public async Task ExecuteCommandLine(Command[] commandLine)
        {
            var commandsByController = commandLine
                        .GroupBy(c => c.TargetController)
                        .ToDictionary(g => g.Key, g => g.ToArray());

            // Get all unique controller names in this command line
            var controllerNames = commandsByController.Keys.ToList();

            // Wait for and acquire all necessary semaphores for the controllers
            foreach (var controllerName in controllerNames)
            {
                await _controllerManager.ControllerLocks[controllerName].WaitAsync();
            }
            // Execute all commands for each controller group
            var executeTasks = controllerNames.Select(async controllerName =>
            {
                var commands = commandsByController[controllerName];
                var semaphore = _controllerManager.ControllerLocks[controllerName];
                await ExecuteCommandsForControllerAsync(commands, controllerName, semaphore);
            });

            await Task.WhenAll(executeTasks);

        }

        private async Task ExecuteCommandsForControllerAsync(Command[] commands, string controllerName, SemaphoreSlim semaphore)
        {
            for (int i = 0; i < commands.Length; i++)
            {
                if (i > 0)
                {
                    // Reacquire the semaphore for subsequent commands
                    await semaphore.WaitAsync();
                }

                try
                {
                    // Execute the command
                    await _controllerManager.Controllers[controllerName].ExecuteCommandAsync(commands[i], semaphore, _log);
                    if (semaphore.CurrentCount == 0)
                        semaphore.Release();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now}: Error executing command on controller {controllerName}: {ex.Message}");
                    throw;
                }
                // The semaphore will be released by the controller after this command
            }
        }



        //private async Task ProcessQueue()
        //{
        //    while (_commandQueue.Count > 0)
        //    {
        //        if (_commandQueue.TryDequeue(out Command[] commands))
        //        {
        //            // Execute the command line
        //            var commandLineTask = ExecuteCommandLine(commands);
        //            await commandLineTask;

        //            //// Check if any command in the line has Await = true
        //            //if (commands.Any(c => c.Await))
        //            //{
        //            //    // Wait for the command line to complete before continuing
        //            //    await commandLineTask;
        //            //}
        //        }
        //        else
        //        {
        //            CurrentState = CommandManagerState.Waiting;
        //        }

        //        //await Task.Delay(10); // Adjust delay as needed
        //    }
        //}

        //public Task ExecuteCommandLine(Command[] commands)
        //{
        //    var tasks = new List<Task>();

        //    // Group commands by their target device
        //    var groupedCommands = commands.GroupBy(c => c.TargetDevice);

        //    // Dictionary to track the last task for each device
        //    var deviceTasks = new Dictionary<string, Task>();

        //    foreach (var group in groupedCommands)
        //    {
        //        var deviceName = group.Key;
        //        var controller = _controllerManager.GetDeviceController<IController>(deviceName);
        //        var semaphore = _controllerManager.ControllerLocks[controller.Name];

        //        foreach (var command in group)
        //        {
        //            // Check if there is a previous task for the same device
        //            if (deviceTasks.TryGetValue(deviceName, out var lastTask))
        //            {
        //                // Chain the command execution after the last task for the same device
        //                var task = lastTask.ContinueWith(_ => ExecuteCommand(controller, semaphore, command)).Unwrap();
        //                deviceTasks[deviceName] = task;
        //                tasks.Add(task);
        //            }
        //            else
        //            {
        //                // No previous task, execute immediately
        //                var task = ExecuteCommand(controller, semaphore, command);
        //                deviceTasks[deviceName] = task;
        //                tasks.Add(task);
        //            }
        //        }
        //    }

        //    // Return a task that completes when all commands in this line are done
        //    return Task.WhenAll(tasks);
        //}

        //private async Task ExecuteCommand(IController controller, SemaphoreSlim semaphore, Command command)
        //{
        //    await semaphore.WaitAsync();
        //    try
        //    {
        //        await controller.ExecuteCommandAsync(command, semaphore, _log);
        //    }
        //    finally
        //    {
        //        if(semaphore.CurrentCount == 0)
        //            semaphore.Release();
        //    }
        //}


        //public async Task ExecuteCommandLine(Command[] commands)
        //{
        //    var tasks = new List<Task>();

        //    // Group commands by their target device
        //    var groupedCommands = commands.GroupBy(c => c.TargetDevice);

        //    // Dictionary to track the last task for each device
        //    var deviceTasks = new Dictionary<char, Task>();

        //    // List of semaphores to acquire
        //    var semaphoresToAcquire = new List<SemaphoreSlim>();

        //    // Acquire all semaphores before starting execution
        //    foreach (var group in groupedCommands)
        //    {
        //        var deviceName = group.Key;
        //        var controller = _controllerManager.GetDeviceController<BaseController>(deviceName);
        //        var semaphore = _controllerManager.ControllerLocks[controller.Name];

        //        // Add the semaphore to the list to be acquired
        //        semaphoresToAcquire.Add(semaphore);
        //    }

        //    // Acquire all semaphores
        //    foreach (var semaphore in semaphoresToAcquire)
        //    {
        //        await semaphore.WaitAsync();
        //    }

        //    try
        //    {
        //        // Execute commands after acquiring all semaphores
        //        foreach (var group in groupedCommands)
        //        {
        //            var deviceName = group.Key;
        //            var controller = _controllerManager.GetDeviceController<BaseController>(deviceName);
        //            var semaphore = _controllerManager.ControllerLocks[controller.Name];

        //            foreach (var command in group)
        //            {
        //                // Check if there is a previous task for the same device
        //                if (deviceTasks.TryGetValue(deviceName, out var lastTask))
        //                {
        //                    // Chain the command execution after the last task for the same device
        //                    var task = lastTask.ContinueWith(_ => ExecuteCommand(controller, semaphore, command)).Unwrap();
        //                    deviceTasks[deviceName] = task;
        //                    tasks.Add(task);
        //                }
        //                else
        //                {
        //                    // No previous task, execute immediately
        //                    var task = ExecuteCommand(controller, semaphore, command);
        //                    deviceTasks[deviceName] = task;
        //                    tasks.Add(task);
        //                }
        //            }
        //        }
        //    }
        //    finally
        //    {
        //        // Release all semaphores
        //        foreach (var semaphore in semaphoresToAcquire)
        //        {
        //            if (semaphore.CurrentCount == 0)
        //                semaphore.Release();
        //        }
        //    }

        //    // Return a task that completes when all commands in this line are done
        //    await Task.WhenAll(tasks);
        //}

        //public async Task ExecuteCommandLine(Command[] commands)
        //{
        //    var tasks = new List<Task>();

        //    // Group commands by their target controller
        //    var groupedCommands = commands
        //        .GroupBy(c => _controllerManager.GetDeviceController<BaseController>(c.TargetDevice));

        //    // Dictionary to track the last task for each controller
        //    var controllerTasks = new Dictionary<string, Task>();

        //    // List of semaphores to acquire
        //    var semaphoresToAcquire = new List<SemaphoreSlim>();

        //    // Acquire all semaphores before starting execution
        //    foreach (var group in groupedCommands)
        //    {
        //        var controller = group.Key;
        //        var semaphore = _controllerManager.ControllerLocks[controller.Name];
        //        semaphoresToAcquire.Add(semaphore);
        //        await semaphore.WaitAsync();
        //    }

        //    try
        //    {
        //        // Execute commands after acquiring all semaphores
        //        foreach (var group in groupedCommands)
        //        {
        //            var controller = group.Key;
        //            var semaphore = _controllerManager.ControllerLocks[controller.Name];

        //            foreach (var command in group)
        //            {
        //                // Check if there is a previous task for the same controller
        //                if (controllerTasks.TryGetValue(controller.Name, out var lastTask))
        //                {
        //                    // Chain the command execution after the last task for the same controller
        //                    var task = lastTask.ContinueWith(_ => ExecuteCommand(controller, semaphore, command)).Unwrap();
        //                    controllerTasks[controller.Name] = task;
        //                    tasks.Add(task);
        //                }
        //                else
        //                {
        //                    // No previous task, execute immediately
        //                    var task = ExecuteCommand(controller, semaphore, command);
        //                    controllerTasks[controller.Name] = task;
        //                    tasks.Add(task);
        //                }
        //            }
        //        }
        //    }
        //    finally
        //    {
        //        // Release all semaphores
        //        foreach (var semaphore in semaphoresToAcquire)
        //        {
        //            if (semaphore.CurrentCount == 0)
        //                semaphore.Release();
        //        }
        //    }

        //    // Return a task that completes when all commands in this line are done
        //    await Task.WhenAll(tasks);
        //}


        //private async Task ProcessControllerQueue(string controllerName, AsyncQueue<Command> queue)
        //{
        //    var controller = _controllerManager.Controllers[controllerName];
        //    var semaphore = _controllerManager.ControllerLocks[controller.Name];

        //    while (await queue.WaitForNextItemAsync())
        //    {
        //        var command = await queue.DequeueAsync();

        //        // Acquire semaphore before executing command
        //        await semaphore.WaitAsync();

        //        // Execute the command
        //        _ = ExecuteCommand(controller, semaphore, command)
        //            .ContinueWith(_ => semaphore.Release(), TaskContinuationOptions.ExecuteSynchronously);
        //    }
        //}


        //private async Task ExecuteCommand(BaseController controller, SemaphoreSlim semaphore, Command command)
        //{
        //    try
        //    {
        //        //await semaphore.WaitAsync();

        //        await controller.ExecuteCommandAsync(command, semaphore, _log);
        //    }
        //    finally
        //    {
        //        if (semaphore.CurrentCount == 0)
        //            semaphore.Release();
        //    }
        //}


        // I should release semaphore only when awaited the response here.
        public async Task UpdateStatesAsync()
        {
            while (true)
            {
                var tasks = new List<Task>();

                foreach (var controllerPair in _controllerManager.Controllers)
                {

                    var controller = controllerPair.Value;
                    var semaphore = _controllerManager.ControllerLocks[controller.Name];
                    if (semaphore.CurrentCount > 0)
                    {
                        //await semaphore.WaitAsync();

                        var task = Task.Run(async () =>
                        {
                            await controller.UpdateStateAsync(_log);
                            //if (semaphore.CurrentCount == 0)
                            //    semaphore.Release();
                        });
                        tasks.Add(task);
                    }

                }
                await Task.WhenAll(tasks);
                await Task.Delay(100);
            }
        }

        public void PrintLog()
        {
            while (_log.TryDequeue(out string logEntry))
            {
                Console.WriteLine(logEntry);
            }
        }

        public void StopDequeuing()
        {
            _running = false;
            CurrentState = CommandManagerState.Waiting;
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
                    csvStringBuilder.Append($"{command.TargetController},{command.TargetDevice},{command.Action},{string.Join(" ", command.Parameters)}");

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