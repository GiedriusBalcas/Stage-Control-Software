using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.custom_functions.helpers
{
    public class PositionerMovementInformation
    {
        public float CurrentPosition { get; set; }
        public float TargetPosition { get; set; }
        public float TargetSpeed { get; set; }
        public float CurrentSpeed { get; set; }
        public float TargetAcceleration { get; set; }
        public float TargetDeceleration { get; set; }
        public float MaxAcceleration { get; set; }
        public float MaxDeceleration { get; set; }
        public float MaxSpeed { get; set; }
    }
}
