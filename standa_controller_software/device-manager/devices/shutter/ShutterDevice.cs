using standa_controller_software.device_manager.attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices.shutter
{
    public class ShutterDevice : BaseShutterDevice
    {

        public ShutterDevice(char name, string id) : base(name, id)
        {
        }

        public override BaseDevice GetCopy()
        {
            return new ShutterDevice(Name,ID) { DelayOff = this.DelayOff, DelayOn = this.DelayOn, IsOn = this.IsOn };
        }

    }
}
