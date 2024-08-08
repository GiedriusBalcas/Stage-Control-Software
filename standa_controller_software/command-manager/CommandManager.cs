using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controllers;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager
{
    public enum CommandManagerState
    {
        Processing,
        Nothing
    }

    public class CommandManager
    {
        private readonly ControllerManager controllerManager;
        private ConcurrentQueue<Command[]> commandQueue = new ConcurrentQueue<Command[]>();
        private ConcurrentQueue<string> log = new ConcurrentQueue<string>();
        private bool _running = true;
        private CommandManagerState _currentState = CommandManagerState.Nothing;

        public CommandManager(ControllerManager manager)
        {
            this.controllerManager = manager;
        }

        public CommandManagerState CurrentState
        {
            get { return _currentState; }
            private set
            {
                _currentState = value;
                log.Enqueue($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}: State changed to {_currentState}");
            }
        }

        public void EnqueueCommands(Command[] commands)
        {
            commandQueue.Enqueue(commands);
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
                if (commandQueue.TryDequeue(out Command[] commands))
                {
                    await ExecuteCommandLine(commands);
                }
                else
                {
                    CurrentState = CommandManagerState.Nothing;
                    _running = false;
                }
                await Task.Delay(10); // Adjust delay as needed
            }
        }

        private async Task ExecuteCommandLine(Command[] commands)
        {
            var tasks = new List<Task>();

            var groupedCommands = commands.GroupBy(c => c.TargetController);
            foreach (var group in groupedCommands)
            {
                var controllerName = group.Key;
                var controller = controllerManager.Controllers[controllerName];
                var semaphore = controllerManager.ControllerLocks[controllerName];

                foreach (var command in group)
                {
                    var task = ExecuteCommand(controller, semaphore, command);
                    tasks.Add(task);

                    if (command.Await)
                    {
                        await task;
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        private async Task ExecuteCommand(IController controller, SemaphoreSlim semaphore, Command command)
        {
            await semaphore.WaitAsync();
            try
            {
                var task = controller.ExecuteCommandAsync(command, semaphore, log);
                if (command.Await)
                {
                    await task;
                }
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
                foreach (var controllerPair in controllerManager.Controllers)
                {
                    var controller = controllerPair.Value;
                    await controller.UpdateStateAsync(log);
                }
                await Task.Delay(50); // More frequent updates
            }
        }

        public void PrintLog()
        {
            while (log.TryDequeue(out string logEntry))
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
            CurrentState = CommandManagerState.Nothing;
        }

        public void ClearQueue()
        {
            commandQueue.Clear();
            CurrentState = CommandManagerState.Nothing;
        }

        public string GetCommandQueue()
        {
            var csvStringBuilder = new StringBuilder();
            var queueSnapshot = commandQueue.ToArray();

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
    }
}
