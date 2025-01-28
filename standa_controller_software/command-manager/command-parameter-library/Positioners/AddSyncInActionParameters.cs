using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager.command_parameter_library
{
    public class AddSyncInActionParameters
    {
        public required Dictionary<char, PositionTimePair> MovementInformation {  get; set; }

    }

    public class PositionTimePair
    {
        public float Position {  get; set; }
        public float Time { get; set; }
        public float Velocity { get; set; }
    }
}
