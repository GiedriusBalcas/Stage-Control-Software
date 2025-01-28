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
        public float? WaitUntilTime { get; set; }
        public Dictionary<char, PositionerInfo> PositionerInfo { get; set; } = [];
        public ShutterInfo ShutterInfo { get; set; } = new ShutterInfo();

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
        public float? WaitUntilPosition { get; set; }
        public float? WaitUntilTime { get; set; }
        public bool Direction {  get; set; }
        public LeadInfo? LeadInformation { get; set; }
        public MovementInformation MovementInformation { get; set; } = new MovementInformation();

    }

    public class ShutterInfo
    {
        public float DelayOn { get; set; } = float.NaN;
        public float DelayOff { get; set; } = float.NaN;
    }

    public class MovementInformation
    {
        public float StartPosition { get; set; }
        public float EndPosition { get; set; }
        public float ConstantSpeedStartPosition { get; set; }
        public float ConstantSpeedEndPosition { get; set; }
        public float ConstantSpeedStartTime { get; set; }
        public float ConstantSpeedEndTime { get; set; }
        public float TotalTime { get; set; }
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
