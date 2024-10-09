using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces.master_controller
{
    public abstract class BaseMasterController : BaseController
    {
        public Dictionary<string, BaseController> SlaveControllers { get; set; } = new Dictionary<string, BaseController>();
        public Dictionary<string, SemaphoreSlim> SlaveControllersLocks { get; set; } = new Dictionary<string, SemaphoreSlim>();

        protected class MultiControllerMethodInformation()
        {
            public Func<Command[], SemaphoreSlim, Dictionary<string, SemaphoreSlim>, ConcurrentQueue<string>, Task> MethodHandle;
            public Func<Dictionary<string, Command>, ConcurrentQueue<string>, Task> AWaitAsync;
            public bool Quable = false;
            public MethodState State = MethodState.Free;
        }

        protected Dictionary<CommandDefinitions, MultiControllerMethodInformation> _methodMap_multiControntroller = new Dictionary<CommandDefinitions, MultiControllerMethodInformation>();

        protected BaseMasterController(string name) : base(name)
        {
        }
        public abstract void AddSlaveController(BaseController controller, SemaphoreSlim controllerLock);
        public virtual async Task ExecuteSlaveCommandsAsync(Command[] commands, SemaphoreSlim semaphore, Dictionary<string, SemaphoreSlim> slaveSemaphores, ConcurrentQueue<string> log) 
        {
            log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Executing {string.Join(' ', commands.Select(command => command.Action).ToArray())} command on device {string.Join(' ', commands.SelectMany(command => command.TargetDevices).ToArray())}");

            var groupedCommands = commands
                .GroupBy(command => command.Action)
                .ToDictionary(group => group.Key, group => new
                {
                    Commands = group.ToArray(),
                    Semaphores = group
                        .Select(command => command.TargetController)
                        .Distinct() // Ensure unique TargetController entries
                        .Where(targetController => slaveSemaphores.ContainsKey(targetController))
                        .ToDictionary(
                            targetController => targetController,
                            targetController => slaveSemaphores[targetController]
                        )
                });

            foreach (var (action, groupData) in groupedCommands)
            {
                var commandGroup = groupData.Commands; // The array of commands for this action
                var groupSemaphores = groupData.Semaphores; // The list of semaphores for this action's commands

                var targetControllerCount = commandGroup.Select(command => command.TargetController).Distinct().Count();
                var semaphoreCount = groupSemaphores.Count();

                if (_methodMap_multiControntroller.TryGetValue(action, out var method))
                {
                    if (commandGroup.Any(groupsCommand => groupsCommand.Await))
                        await method.MethodHandle(commands, semaphore, groupSemaphores, log);
                    else
                        _ = method.MethodHandle(commands, semaphore, groupSemaphores, log);
                }
                else
                {
                    foreach (Command command in commands)
                    {
                        if (SlaveControllers.TryGetValue(command.TargetController, out var slaveController))
                        {
                            await slaveController.ExecuteCommandAsync(command, groupSemaphores[command.TargetController], log);
                        }
                        else
                            throw new Exception($"Slave controller {command.TargetController} was not found.");

                    }
                }
            }
        }

        public virtual Task AwaitQueuedItems(SemaphoreSlim semaphore, Dictionary<string, SemaphoreSlim> slaveSemaphors, ConcurrentQueue<string> log)
        {
            return Task.CompletedTask;
        }

    }
}
