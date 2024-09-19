using OpenTK.Audio.OpenAL;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces
{
    public abstract class BaseController
    {

        protected enum MethodState
        {
            Working,
            Free,
            Waiting
        }

        protected class MethodInformation()
        {
            public Func<Command, SemaphoreSlim, ConcurrentQueue<string>, Task> MethodHandle;
            public Func<SemaphoreSlim, ConcurrentQueue<string>, Task> AWaitAsync;
            public bool Quable = false;
            public MethodState State = MethodState.Free;
        }

        protected Dictionary<CommandDefinitions, MethodInformation> _methodMap = new Dictionary<CommandDefinitions, MethodInformation>();
        
        [DisplayPropertyAttribute]
        public BaseController? MasterController { get; set; } = null;
        
        public bool IsQuable { get; set; } = false;

        [DisplayPropertyAttribute]
        public string Name { get;}
        protected BaseController(string name)
        {
            Name = name;
        }
        public virtual async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log)
        {
            // log.Enqueue($"{DateTime.Now.ToString("HH:mm:ss.fff")}: Executing {command.Action} command on device {string.Join(' ', command.TargetDevices)}, parameters: {FormatParameters(command.Parameters)}");

            Dictionary<char, CancellationToken> cancelationTokens = new Dictionary<char, CancellationToken>();


            if (_methodMap.TryGetValue(command.Action, out var method))
            {
                if (command.Await)
                    await method.MethodHandle(command, semaphore, log);
                else
                    _ = method.MethodHandle(command, semaphore, log);
            }
            else
            {
                throw new InvalidOperationException("Invalid action");
            }
        }
        public abstract Task UpdateStatesAsync(ConcurrentQueue<string> log);
        public abstract void AddDevice(BaseDevice device);
        public abstract Task ConnectDevice(BaseDevice device, SemaphoreSlim semaphore);
        public abstract List<BaseDevice> GetDevices();
        public abstract BaseController GetCopy();

        public virtual Task AwaitQueuedItems(SemaphoreSlim semaphore, ConcurrentQueue<string> log) 
        {
            return Task.CompletedTask ;
        }
        public abstract Task Stop(SemaphoreSlim semaphore, ConcurrentQueue<string> log);

    }
}
