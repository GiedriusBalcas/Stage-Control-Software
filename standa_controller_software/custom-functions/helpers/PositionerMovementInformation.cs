using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.custom_functions.helpers
{
    public class PositionerMovementInformation
    {
        public PositionerParameters PositionerParameters { get; set; } = new PositionerParameters();
        public StartingMovementParameters StartingMovementParameters { get; set; } = new StartingMovementParameters();
        public TargetMovementParameters TargetMovementParameters { get; set; } = new TargetMovementParameters();
        public KinematicParameters KinematicParameters { get; set; } = new KinematicParameters();
    }


    public class PositionerParameters
    {
        public float MaxAcceleration { get; set; }
        public float MaxDeceleration { get; set; }
        public float MaxSpeed { get; set; }
        public float Jerk { get; set; }
    }
    public class StartingMovementParameters
    {
        public float Position { get; set; }
        public float Speed { get; set; }
        public bool Direction { get; set; }
        public float TargetSpeed { get; set; }
        public float Acceleration { get; set; }
        public float Deceleration { get; set; }
    }
    public class TargetMovementParameters
    {
        public float Position { get; set; }
        public float Speed { get; set; }
        public float TargetSpeed { get; set; }
        public float Acceleration { get; set; }
        public float Deceleration { get; set; }
        public bool Direction { get; set; }
        public float Distance { get; set; }
        public float Rethrow { get; set; }
    }
    public class KinematicParameters
    {
        public float ConstantSpeedStartPosition { get; set; }
        public float ConstantSpeedEndPosition { get; set; }
        public float ConstantSpeedStartTime { get; set; }
        public float ConstantSpeedEndTime { get; set; }
        public float TotalTime { get; set; }
    }

}
