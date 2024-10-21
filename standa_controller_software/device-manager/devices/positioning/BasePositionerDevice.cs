using standa_controller_software.device_manager.attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.device_manager.devices
{
    public abstract class BasePositionerDevice : BaseDevice
    {
        protected BasePositionerDevice(char name, string id) : base(name, id)
        {
        }

        [DisplayPropertyAttribute]
        [DynamicPropertyAttribute]
        public virtual float MaxSpeed 
        { 
            get; 
            set; 
        }
        [DisplayPropertyAttribute]
        [DynamicPropertyAttribute]
        public virtual float DefaultSpeed { get; set; }
        [DisplayPropertyAttribute]
        [DynamicPropertyAttribute]
        public virtual float MaxAcceleration 
        {
            get; 
            set; 
        }
        [DisplayPropertyAttribute]
        [DynamicPropertyAttribute]
        public virtual float MaxDeceleration { get; set; }
        [DisplayPropertyAttribute]
        public float StepSize { get; set; }

        public event EventHandler PositionChanged;
        
        private float _currentPosition = 0f;
        public virtual float CurrentPosition
        {
            get
            {
                return _currentPosition;
            }
            set
            {
                if (_currentPosition != value)
                {
                    _currentPosition = value;
                    PositionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public virtual float CurrentSpeed { get; set; }
        public virtual float Acceleration { get; set; }
        public virtual float Deceleration { get; set; }
        public virtual float Speed { get; set; }
    }
}
