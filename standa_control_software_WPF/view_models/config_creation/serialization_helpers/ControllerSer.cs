using System.Collections.ObjectModel;

namespace standa_control_software_WPF.view_models.config_creation.serialization_helpers
{
    public class ControllerSer
    {
        public string Name { get; set; } = string.Empty;
        public string SelectedControllerType { get; set; } = string.Empty;
        public List<DeviceSer> Devices { get; set; } = new List<DeviceSer>();
        public List<PropertyDisplayItemSer> ControllerProperties {  get; set; } = new List<PropertyDisplayItemSer>();
    }
}