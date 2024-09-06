using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager.command_parameter_library
{
    public class MoveAbsoluteParameters
    {
        public bool IsLine { get; set; }
        public bool IsShutterUsed { get; set; }
        public bool IsLeadInUsed { get; set; }
        public bool IsLeadOutUsed { get; set; }
        public float AllocatedTime { get; set; }
        public Dictionary<char, PositionerInfo> PositionerInfo { get; set; } = new Dictionary<char, PositionerInfo>();
        public ShutterInfo? ShutterInfo { get; set; }
    }

    public class PositionerInfo
    {
        public float TargetPosition { get; set; }
        public float? WaitUntil { get; set; }
        public LeadInfo? LeadInformation { get; set; }

    }

    public class ShutterInfo
    {
        public float DelayOn { get; set; }
        public float DelayOff { get; set; }
    }

    public class LeadInfo
    {
        public float LeadInStartPos { get; set; }
        public float LeadInEndPos { get; set; }
        public float LeadOutEndPos { get; set; }
    }

}
