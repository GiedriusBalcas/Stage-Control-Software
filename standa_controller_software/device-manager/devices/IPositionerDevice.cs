using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public interface IPositionerDevice : IDevice
    {
        float Position { get; set; }
        float Speed { get; set; }
    }
}
