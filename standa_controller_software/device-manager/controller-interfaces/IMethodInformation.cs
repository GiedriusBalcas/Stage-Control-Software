using standa_controller_software.command_manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controller_interfaces
{
    public enum MethodState
    {
        Busy,
        Free,
    }
    public interface IMethodInformation
    {
        MethodState State { get; set; }
        Type ReturnType { get; }
        Task<object?> InvokeAsync(Command command, SemaphoreSlim semaphore);
    }
    public interface IMultiControllerMethodInformation
    {
        MethodState State { get; set; }
        Type ReturnType { get; }
        Task<Dictionary<string,object?>> InvokeAsync(Command[] command, SemaphoreSlim semaphore);
    }
}
