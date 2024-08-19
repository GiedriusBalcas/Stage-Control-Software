using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public interface IShutterDevice : IDevice
    {
        public string Name { get;}
        public int DelayOn { get; set; }
        public int DelayOff { get; set; }
        public bool IsOn { get; set; }

        public event Action? StateChanged;
    }
}
