using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager
{
    public class Command
    {
        public string TargetController { get; set; } = string.Empty;
        public char[] TargetDevices { get; set; }
        public CommandDefinitionsLibrary Action { get; set; }
        public object[][] Parameters { get; set; }
        public bool Await {  get; set; }
        public DateTime Timestamp { get; set; }
    }
}
