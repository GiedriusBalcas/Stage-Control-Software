using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager
{
    public class Command
    {
        public string TargetController { get; set; }
        public char TargetDevice { get; set; }
        public string Action { get; set; }
        public object[] Parameters { get; set; }
        public bool Await {  get; set; }
        public DateTime Timestamp { get; set; }
    }
}
