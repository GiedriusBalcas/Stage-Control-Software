using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public interface IPositionerDevice : IDevice
    {
        float CurrentPosition { get; set; }
        float CurrentSpeed { get; set; }
        float MaxSpeed { get; set; }
        float MaxAcceleration { get; set; }
        float MaxDeceleration { get; set; }
        float Acceleration { get; set; }
        float Deceleration { get; set; }
        float Speed { get; set; }
    }
}
