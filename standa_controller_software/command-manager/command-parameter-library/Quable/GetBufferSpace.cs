using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager.command_parameter_library.Positioners
{
    public class GetBufferCountParameters
    {
        public char Device { get; set; }
        public override string ToString()
        {
            string constructedString = "GetBufferSize off: [";
            if(Device != char.MinValue)
            {
                constructedString += " " + Device;    
            }
            constructedString += " ].";
            return constructedString;
        }
    }

}
