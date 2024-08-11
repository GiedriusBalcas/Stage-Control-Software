using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices.shutter
{
    public class ShutterDevice_Virtual : IShutterDevice
    {
        public string Name { get; private set; }
        public int DelayOn { get; set; }
        public int DelayOff { get; set; }
        public bool IsOn { get; set; }

        public event EventHandler? StateChanged;

        public ShutterDevice_Virtual(string name)
        {
            Name = name;
        }

        public IDevice GetCopy()
        {
            return new ShutterDevice_Virtual(Name) { DelayOff = this.DelayOff, DelayOn = this.DelayOn, IsOn = this.IsOn };
        }
    }
}
