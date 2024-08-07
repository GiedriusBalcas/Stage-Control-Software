using standa_controller_software.device_manager;
using System.Collections.Concurrent;
using System.Text;

namespace standa_controller_software.command_manager
{
    public class CommandManager
    {
        private readonly ControllerManager controllerManager;
        private ConcurrentQueue<Command[]> commandQueue = new ConcurrentQueue<Command[]>();
        private ConcurrentQueue<string> log = new ConcurrentQueue<string>();
        private bool _allowed = true;

        public CommandManager(ControllerManager manager)
        {
            this.controllerManager = manager;
        }

        public void EnqueueCommands(Command[] commands)
        {
            commandQueue.Enqueue(commands);
        }

        public void Start()
        {
            _allowed = true;
            Task.Run(() => ProcessQueue());
            //await ProcessQueue();
        }

        private async Task ProcessQueue()
        {
            while (commandQueue.Count > 0 && _allowed)
            {
                if (commandQueue.TryDequeue(out Command[] commands))
                {
                    var parallelTasks = new List<Task>();

                    var groupedCommands = commands.GroupBy(c => c.TargetController);
                    foreach (var group in groupedCommands)
                    {
                        var controllerName = group.Key;
                        var controller = controllerManager.Controllers[controllerName];
                        var semaphore = controllerManager.ControllerLocks[controllerName];

                        var task = semaphore.WaitAsync().ContinueWith(async _ =>
                        {
                            try
                            {
                                foreach (var command in group)
                                {
                                    await controller.ExecuteCommandAsync(command, semaphore, log);
                                }
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });

                        parallelTasks.Add(task.Unwrap());
                    }

                    await Task.WhenAll(parallelTasks);
                }

                await Task.Delay(10); // Adjust delay as needed
            }

            var kaka = "final";
        }

        public async Task UpdateStatesAsync()
        {
            while (true)
            {
                foreach (var controllerPair in controllerManager.Controllers)
                {
                    var controller = controllerPair.Value;
                    await controller.UpdateStateAsync(log);
                }
                await Task.Delay(1000); // Update interval
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
            var parallelTasks = new List<Task>();

            var groupedCommands = commands.GroupBy(c => c.TargetController);
            foreach (var group in groupedCommands)
            {
                var controllerName = group.Key;
                var controller = controllerManager.Controllers[controllerName];
                var semaphore = controllerManager.ControllerLocks[controllerName];

                var task = semaphore.WaitAsync().ContinueWith(async _ =>
                {
                    try
                    {
                        foreach (var command in group)
                        {
                            await controller.ExecuteCommandAsync(command, semaphore, log);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                parallelTasks.Add(task.Unwrap());
            }

            await Task.WhenAll(parallelTasks);
        }

        public void StopDequeuing()
        {
            _allowed = false;
        }

        public void ClearQueue()
        {
            commandQueue.Clear();
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

        //public void DisplayControllerStates()
        //{
        //    foreach (var controllerPair in controllerManager.Controllers)
        //    {
        //        var controller = controllerPair.Value;
        //        foreach (var device in controller.Devices.Values)
        //        {
        //            if (device is IPositionerDevice positioner)
        //            {
        //                Console.WriteLine($"Positioner: {device.DeviceId}, Position: {positioner.Position}");
        //            }
        //        }
        //    }
        //}
    }
}
