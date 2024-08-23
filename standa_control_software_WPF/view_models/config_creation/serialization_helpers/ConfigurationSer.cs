using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_control_software_WPF.view_models.config_creation.serialization_helpers
{
    public class ConfigurationSer
    {
        public string Name { get; set; } = string.Empty;
        public string XToolPositionDependancy { get; set; } = string.Empty;
        public string YToolPositionDependancy { get; set; } = string.Empty;
        public string ZToolPositionDependancy { get; set; } = string.Empty;
        public List<ControllerSer> Controllers {  get; set; } = new List<ControllerSer>();
    }
}
