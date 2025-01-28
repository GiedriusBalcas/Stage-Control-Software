using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager.command_parameter_library.Synchronization
{
    public class AddSyncControllerBufferItemParameters
    {
        public required char[] Devices { get; set; }
        public bool Launch { get; set; }
        public bool Shutter { get; set; }
        public float Rethrow { get; set; }
        public float ShutterDelayOn { get; set; }
        public float ShutterDelayOff { get; set; }
    }
}
