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

        protected Dictionary<CommandDefinitions, MultiControllerMethodInformation> _multiControllerMethodMap = new Dictionary<CommandDefinitions, MultiControllerMethodInformation>();

        protected BaseMasterController(string name, ConcurrentQueue<string> log) : base(name, log) 
        {
        }
        
        public virtual async Task ExecuteSlaveCommandsAsync(Command[] commands, SemaphoreSlim semaphore) 
        {
            _log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Executing {string.Join(' ', commands.Select(command => command.Action).ToArray())} command on device {string.Join(' ', commands.SelectMany(command => command.TargetDevices).ToArray())}");

            var groupedCommands = commands
                .GroupBy(command => command.Action)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToArray()
                );

            var resultDictionary = new Dictionary<string, object?>();

            foreach (var (action, groupsCommands) in groupedCommands)
            {
                var targetControllerCount = groupsCommands.Select(command => command.TargetController).Distinct().Count();
                
                if (_multiControllerMethodMap.TryGetValue(action, out var method))
                {
                    if (groupsCommands.Any(groupsCommand => groupsCommand.Await))
                        await method.InvokeAsync(commands, semaphore);
                    else
                        _ = method.MethodHandle(commands, semaphore);
                }
                else
                {
                    foreach (Command command in commands)
                    {
                        await ExecuteSlaveCommand(command);
                    }
                }
            }
        }
        public abstract void AddSlaveController(BaseController controller, SemaphoreSlim controllerLock);
        public override List<BaseDevice> GetDevices()
        {
            return new List<BaseDevice>();
        }
        public override void AddDevice(BaseDevice device)
        {
            throw new NotImplementedException();
        }

        protected override Task ConnectDevice(Command command, SemaphoreSlim semaphore)
        {
            throw new NotImplementedException();
        }
        protected override Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected async Task<Dictionary<string, SemaphoreSlim>> GatherSemaphoresForController(List<string> controllerNames)
        {
            var ackquiredSemaphores = new Dictionary<string, SemaphoreSlim>();
            foreach (var controllerName in controllerNames)
            {
                var controller = SlaveControllers[controllerName];
                if (SlaveControllersLocks.TryGetValue(controllerName, out SemaphoreSlim semaphore))
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
        protected static void ReleaseSemeaphores(Dictionary<string, SemaphoreSlim> ackquiredSemaphores)
        {
            foreach (var (controllerName, semaphore) in ackquiredSemaphores)
            {
                semaphore.Release();
            }
        }
        protected async Task ExecuteSlaveCommand(Command command)
        {
            var controllerName = command.TargetController;
            if (controllerName != string.Empty && SlaveControllers.TryGetValue(controllerName, out var controller))
            {
                var controllerLock = await GatherSemaphoresForController([controllerName]);
                try
                {
                    if(command.Await)
                        await controller.ExecuteCommandAsync(command, controllerLock[controllerName]);
                    else
                        _ = controller.ExecuteCommandAsync(command, controllerLock[controllerName]);
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
