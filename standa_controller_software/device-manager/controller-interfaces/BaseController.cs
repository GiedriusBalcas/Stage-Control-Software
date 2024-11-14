using OpenTK.Audio.OpenAL;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces
{
    public abstract class BaseController
    {
        protected Dictionary<CommandDefinitions, IMethodInformation> _methodMap = new Dictionary<CommandDefinitions, IMethodInformation>();
        protected ConcurrentQueue<string> _log;

        [DisplayPropertyAttribute]
        public BaseController? MasterController { get; set; } = null;
        
        [DisplayPropertyAttribute]
        public string Name { get;}

        [DisplayPropertyAttribute]
        public string ID { get; set; }
        protected BaseController(string name, ConcurrentQueue<string> log)
        {
            Name = name;
            _log = log;

            _methodMap[CommandDefinitions.UpdateState] = new MethodInformation()
            {
                MethodHandle = UpdateStatesAsync,
            };
            _methodMap[CommandDefinitions.Initialize] = new MethodInformation()
            {
                MethodHandle = InitializeController,
            };
            _methodMap[CommandDefinitions.ConnectDevice] = new MethodInformation()
            {
                MethodHandle = ConnectDevice,
            };
            _methodMap[CommandDefinitions.ConnectDevice] = new MethodInformation<int>()
            {
                MethodHandle = GetIntValue,
            };

        }

        protected Task<int> GetIntValue(Command command, SemaphoreSlim semaphore)
        {
            int value = 1;
            var kaka = Task.Run(() =>
            {
                return value;
            });

            return kaka;
        }

        public virtual async Task<T> ExecuteCommandAsync<T>(Command command, SemaphoreSlim semaphore)
        {
            if (_methodMap.TryGetValue(command.Action, out var method))
            {
                // Ensure the method's return type matches the expected type T
                if (method.ReturnType != typeof(T))
                {
                    throw new InvalidOperationException($"Return type mismatch. Expected {method.ReturnType}, but got {typeof(T)}.");
                }

                var result = await method.InvokeAsync(command, semaphore);

                // Handle null result if T is a value type
                if (result == null && typeof(T).IsValueType)
                {
                    throw new InvalidOperationException("Method returned null, but the expected return type is a non-nullable value type.");
                }

                return (T)result;
            }
            else
            {
                throw new InvalidOperationException("Invalid action");
            }
        }
        public virtual async Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore)
        {
            if (_methodMap.TryGetValue(command.Action, out var method))
            {
                if (command.Await)
                    //await method.MethodHandle(command, semaphore);
                    await method.InvokeAsync(command, semaphore);
                else
                    _ = method.InvokeAsync(command, semaphore);
            }
            else
            {
                throw new InvalidOperationException("Invalid action");
            }
        }

        public abstract List<BaseDevice> GetDevices();
        public abstract BaseController GetCopy();
        public abstract void AddDevice(BaseDevice device);
        protected abstract Task ConnectDevice(Command command, SemaphoreSlim semaphore);
        protected virtual Task InitializeController(Command command, SemaphoreSlim semaphore)
        {
            return Task.CompletedTask;
        }
        protected abstract Task Stop(Command command, SemaphoreSlim semaphore);
        protected abstract Task UpdateStatesAsync(Command command, SemaphoreSlim semaphore);

    }
}
