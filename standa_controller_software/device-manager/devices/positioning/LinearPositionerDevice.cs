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
        public float Position { get; set; }
        public float Speed { get; set; }
        public float MaxSpeed { get; set; }
        public float MaxAcceleration { get; set; }
        public float MaxDeceleration { get; set; }
        public float Acceleration { get; set; }
        public float Deceleration { get; set; }

        public LinearPositionerDevice(string name)
        {
            Name = name;
        }

        public IDevice GetCopy()
        {
            return new LinearPositionerDevice(this.Name) { Position = this.Position, Speed = this.Speed, Acceleration = this.Acceleration, Deceleration = this.Deceleration, MaxAcceleration = this.MaxAcceleration, MaxDeceleration = this.MaxDeceleration, MaxSpeed = this.MaxSpeed};
        }
    }
}
