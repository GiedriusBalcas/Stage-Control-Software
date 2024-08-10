using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

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

        public void EnqueueCommands(Command[] commands)
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
            while (_running)
            {
                if (_commandQueue.TryDequeue(out Command[] commands))
                {
                    // Execute the command line
                    var commandLineTask = ExecuteCommandLine(commands);

                    // Check if any command in the line has Await = true
                    if (commands.Any(c => c.Await))
                    {
                        // Wait for the command line to complete before continuing
                        await commandLineTask;
                    }
                }
                else
                {
                    CurrentState = CommandManagerState.Waiting;
                    _running = false;
                }

                await Task.Delay(10); // Adjust delay as needed
            }
        }

        private Task ExecuteCommandLine(Command[] commands)
        {
            var tasks = new List<Task>();

            // Group commands by their target controller
            var groupedCommands = commands.GroupBy(c => c.TargetController);

            // Dictionary to track the last task for each controller
            var controllerTasks = new Dictionary<string, Task>();

            foreach (var group in groupedCommands)
            {
                var controllerName = group.Key;
                var controller = _controllerManager.Controllers[controllerName];
                var semaphore = _controllerManager.ControllerLocks[controllerName];

                foreach (var command in group)
                {
                    // Check if there is a previous task for the same controller
                    if (controllerTasks.TryGetValue(controllerName, out var lastTask))
                    {
                        // Chain the command execution after the last task
                        var task = lastTask.ContinueWith(_ => ExecuteCommand(controller, semaphore, command)).Unwrap();
                        controllerTasks[controllerName] = task;
                        tasks.Add(task);
                    }
                    else
                    {
                        // No previous task, execute immediately
                        var task = ExecuteCommand(controller, semaphore, command);
                        controllerTasks[controllerName] = task;
                        tasks.Add(task);
                    }
                }
            }

            // Return a task that completes when all commands in this line are done
            return Task.WhenAll(tasks);
        }

        private async Task ExecuteCommand(IController controller, SemaphoreSlim semaphore, Command command)
        {
            await semaphore.WaitAsync();
            try
            {
                await controller.ExecuteCommandAsync(command, semaphore, _log);
            }
            finally
            {
                semaphore.Release();
            }
        }


        public async Task UpdateStatesAsync()
        {
            while (_running)
            {
                foreach (var controllerPair in _controllerManager.Controllers)
                {
                    var controller = controllerPair.Value;
                    await controller.UpdateStateAsync(_log);
                }
                await Task.Delay(50); // More frequent updates
            }
        }

        public void PrintLog()
        {
            while (_log.TryDequeue(out string logEntry))
            {
                Console.WriteLine(logEntry);
            }
        }

        public async Task ExecuteSingleCommandLine(Command[] commands)
        {
            await ExecuteCommandLine(commands);
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

        public string GetCommandQueue()
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

        public void SendCommandQueueCopy(CommandManager commandManager) 
        {
            foreach (var commandLine in _commandQueue)
            {
                commandManager.EnqueueCommands(commandLine);
            }
        }
    }
}
