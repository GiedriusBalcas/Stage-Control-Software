using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public abstract class BaseShutterDevice : BaseDevice
    {
        protected BaseShutterDevice(char name, string id) : base(name, id)
        {
        }
        public virtual int DelayOn { get; set; }
        public virtual int DelayOff { get; set; }
        public virtual bool IsOn { get; set; }

        public event Action? StateChanged;
    }
}
