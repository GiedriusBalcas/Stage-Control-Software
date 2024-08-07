using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controllers
{
    public interface IController
    {
        string Name { get;}
        Task ExecuteCommandAsync(Command command, SemaphoreSlim semaphore, ConcurrentQueue<string> log);
        Task UpdateStateAsync(ConcurrentQueue<string> log);
        void AddDevice(IDevice device);
        List<IDevice> GetDevices();

    }
}
