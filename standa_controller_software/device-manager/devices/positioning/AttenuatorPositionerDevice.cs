using standa_controller_software.device_manager.attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices.positioning
{
    public class AttenuatorPositionerDevice : BasePositionerDevice
    {
        [DisplayPropertyAttribute]
        [DynamicPropertyAttribute]
        public float PositionForMin { get; set; }

        [DisplayPropertyAttribute]
        [DynamicPropertyAttribute]
        public float PositionForMax { get; set; }

        [DisplayPropertyAttribute]
        [DynamicPropertyAttribute]
        public float PowerAmplitude { get; set; }

        public float CurrentPower => ConvertFromPositionToPower(CurrentPosition);
        public AttenuatorPositionerDevice(char name, string id) : base(name, id)
        {
        }
        public float ConvertFromPositionToPower(float position)
        {
            var theta = Math.PI / 2 / (PositionForMin - PositionForMax) * (position - PositionForMax);
            var power = PowerAmplitude * Math.Pow(Math.Cos(theta), 2);

            return (float)power;
        }

        public float ConvertFromPowerToPosition(float power)
        {
            var theta = Math.Acos(Math.Sqrt(power / PowerAmplitude));
            var position = 2 * theta / Math.PI * (PositionForMin - PositionForMax) + PositionForMax;

            return (float)position;
        }
        public override BaseDevice GetCopy()
        {
            return new AttenuatorPositionerDevice(this.Name, this.ID) { CurrentPosition = this.CurrentPosition, CurrentSpeed = this.CurrentSpeed, Acceleration = this.Acceleration, Deceleration = this.Deceleration, Speed = this.Speed, MaxAcceleration = this.MaxAcceleration, MaxDeceleration = this.MaxDeceleration, MaxSpeed = this.MaxSpeed, IsConnected = this.IsConnected, StepSize = this.StepSize, DefaultSpeed = this.DefaultSpeed , PositionForMax = this.PositionForMax, PositionForMin = this.PositionForMin, PowerAmplitude= this.PowerAmplitude};
        }
    }
}
