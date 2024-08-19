using standa_controller_software.device_manager.controller_interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public class LinearPositionerDevice : IPositionerDevice
    {
        public string Name { get; }
        public float CurrentPosition { get; set; }
        public float CurrentSpeed { get ; set ; }
        public float MaxAcceleration { get; set; }
        public float MaxDeceleration { get; set; }
        public float MaxSpeed { get; set; }
        public float Acceleration { get; set; }
        public float Deceleration { get; set; }
        public float Speed { get; set; }

        public LinearPositionerDevice(string name)
        {
            Name = name;
        }

        public IDevice GetCopy()
        {
            return new LinearPositionerDevice(this.Name) { CurrentPosition = this.CurrentPosition, CurrentSpeed = this.CurrentSpeed, Acceleration = this.Acceleration, Deceleration = this.Deceleration, Speed = this.Speed, MaxAcceleration = this.MaxAcceleration, MaxDeceleration = this.MaxDeceleration, MaxSpeed = this.MaxSpeed };
        }
    }
}
