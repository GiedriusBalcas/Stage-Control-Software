using standa_controller_software.command_manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces
{
    public class MethodInformation : IMethodInformation
    {
        public Func<Command, SemaphoreSlim, Task> MethodHandle { get; set; }
        public MethodState State { get; set; } = MethodState.Free;
        public Type ReturnType => typeof(void);

        public async Task<object?> InvokeAsync(Command command, SemaphoreSlim semaphore)
        {
            await MethodHandle(command, semaphore);
            return null; // No return value
        }
    }

    public class MethodInformation<T> : IMethodInformation
    {
        public Func<Command, SemaphoreSlim, Task<T>> MethodHandle { get; set; }
        public MethodState State { get; set; } = MethodState.Free;
        public Type ReturnType => typeof(T);

        public async Task<object?> InvokeAsync(Command command, SemaphoreSlim semaphore)
        {
            T result = await MethodHandle(command, semaphore);
            return result;
        }
    }

    public class MultiControllerMethodInformation : IMultiControllerMethodInformation
    {
        public Func<Command[], SemaphoreSlim, Task> MethodHandle { get; set; }
        public MethodState State { get; set; } = MethodState.Free;
        public Type ReturnType => typeof(void);

        public async Task InvokeAsync(Command[] command, SemaphoreSlim semaphore)
        {
            await MethodHandle(command, semaphore);
        }
    }
}
