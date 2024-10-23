using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.custom_functions.helpers
{
    public class PositionerMovementInformation
    {
        public float StartingPosition { get; set; }
        public float StartingSpeed { get; set; }
        public float StartingAcceleration { get; set; }
        public float StartingDeceleration { get; set; }
        public float TargetPosition { get; set; }
        public float TargetSpeed { get; set; }
        public float CurrentTargetSpeed { get; set; }
        public float TargetAcceleration { get; set; }
        public float TargetDeceleration { get; set; }
        public bool TargetDirection { get; set; }
        public float TargetDistance { get; set; }
        public float MaxAcceleration { get; set; }
        public float MaxDeceleration { get; set; }
        public float MaxSpeed { get; set; }
        public float Rethrow { get; set; }
        public float Jerk { get; set; }

    }
}
