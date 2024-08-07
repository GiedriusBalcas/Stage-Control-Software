using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.controllers
{
    public interface IPositionerController : IController
    {
        Dictionary<string, IPositionerDevice> Devices { get; }
    }
}
