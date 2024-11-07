using standa_controller_software.device_manager.attributes;
using standa_controller_software.device_manager.controller_interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public class LinearPositionerDevice : BasePositionerDevice
    {



        public LinearPositionerDevice(char name, string id) : base(name, id)
        {
        }

        

        public override BaseDevice GetCopy()
        {
            return new LinearPositionerDevice(this.Name, this.ID) { CurrentPosition = this.CurrentPosition, CurrentSpeed = this.CurrentSpeed, Acceleration = this.Acceleration, Deceleration = this.Deceleration, Speed = this.Speed, MaxAcceleration = this.MaxAcceleration, MaxDeceleration = this.MaxDeceleration, MaxSpeed = this.MaxSpeed, IsConnected = this.IsConnected, StepSize = this.StepSize, DefaultSpeed = this.DefaultSpeed};
        }

    }
}
