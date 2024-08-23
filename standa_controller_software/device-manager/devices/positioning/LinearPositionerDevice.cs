using standa_controller_software.device_manager.attributes;
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
        [DisplayPropertyAttribute]
        public string Name { get; }
        public float CurrentPosition { get; set; }
        public float CurrentSpeed { get ; set ; }
        [DynamicPropertyAttribute]
        [DisplayPropertyAttribute]
        public float MaxAcceleration { get; set; }
        [DynamicPropertyAttribute]
        [DisplayPropertyAttribute]
        public float MaxDeceleration { get; set; }
        [DynamicPropertyAttribute]
        [DisplayPropertyAttribute]
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
