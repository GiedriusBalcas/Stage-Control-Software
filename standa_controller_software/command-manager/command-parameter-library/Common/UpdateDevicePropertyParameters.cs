using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_controller_software.command_manager.command_parameter_library.Common
{
    public class UpdateDevicePropertyParameters
    {
        public char DeviceName { get; set; }
        public string PropertyName { get; set; }
        public object PropertyValue { get; set; }

        public override string ToString()
        {
            string constructedString = string.Empty;
            constructedString += $"Device: {DeviceName} Property: {PropertyName} Value: {PropertyValue}";

            return constructedString;
        }
    }
}
