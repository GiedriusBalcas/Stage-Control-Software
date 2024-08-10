namespace standa_controller_software.device_manager
{
    public struct ControllerInfo
    {
        public string Name { get; set; }
        public Type Type { get; set; }
        public List<DeviceInfo> AllowedDevices { get; set; }
        public Type VirtualType { get; set; }
    }
}
