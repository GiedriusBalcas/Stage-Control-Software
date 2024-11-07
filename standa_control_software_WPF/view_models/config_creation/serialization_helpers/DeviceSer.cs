namespace standa_control_software_WPF.view_models.config_creation.serialization_helpers
{
    public class DeviceSer
    {
        public char Name { get; set; }
        public string SelectedDeviceType { get; set; } = string.Empty;
        public List<PropertyDisplayItemSer> DeviceProperties { get; set; } = new List<PropertyDisplayItemSer>();
    }
}