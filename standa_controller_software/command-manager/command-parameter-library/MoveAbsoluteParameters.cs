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

        public override string ToString()
        {
            string constructedString = string.Empty;
            constructedString += $"time: {AllocatedTime}";
            foreach (var deviceName in PositionerInfo.Keys)
            {
                var info = PositionerInfo[deviceName];
                constructedString += $"; {deviceName}[{info.TargetPosition}, {info.TargetSpeed}]";
            }

            return constructedString;
        }
    }

    public class PositionerInfo
    {
        public float TargetPosition { get; set; }
        public float TargetSpeed { get; set; }
        public float? WaitUntil { get; set; }
        public float? WaitUntilTime { get; set; }
        public bool Direction {  get; set; }
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
        public float LeadOutStartPos { get; set; }
        public float LeadOutEndPos { get; set; }
        public float LeadInAllocatedTime { get; set; }
        public float LeadOutAllocatedTime { get; set; }
    }

}
