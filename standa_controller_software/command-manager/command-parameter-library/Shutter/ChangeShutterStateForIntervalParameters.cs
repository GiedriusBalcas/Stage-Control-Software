using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager.command_parameter_library
{
    public class ChangeShutterStateForIntervalParameters
    {
        public float Duration { get; set; } = float.NaN;

        public override string ToString()
        {
            string constructedString = string.Empty;
            constructedString += $"duration: {Duration}";

            return constructedString;
        }
    }
}
