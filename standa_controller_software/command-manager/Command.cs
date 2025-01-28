using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager
{
    public class Command
    {
        public required string TargetController { get; set; }
        public required char[] TargetDevices { get; set; }
        public CommandDefinitions Action { get; set; }
        public required object Parameters { get; set; }
        public bool Await { get; set; } = true;
        public DateTime Timestamp { get; set; }
        public float EstimatedTime { get; set; } = 0f;
    }
}
