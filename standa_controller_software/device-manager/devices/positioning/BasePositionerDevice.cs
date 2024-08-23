using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public abstract class BasePositionerDevice : BaseDevice
    {
        protected BasePositionerDevice(char name, string id) : base(name, id)
        {
        }

        public virtual float CurrentPosition { get; set; }
        public virtual float CurrentSpeed { get; set; }
        public virtual float MaxSpeed { get; set; }
        public virtual float MaxAcceleration { get; set; }
        public virtual float MaxDeceleration { get; set; }
        public virtual float Acceleration { get; set; }
        public virtual float Deceleration { get; set; }
        public virtual float Speed { get; set; }
    }
}
