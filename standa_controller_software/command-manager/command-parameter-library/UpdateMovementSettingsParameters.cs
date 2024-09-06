
namespace standa_controller_software.command_manager.command_parameter_library
{
    public class UpdateMovementSettingsParameters
    {
        public Dictionary<char, MovementSettingsInfo> MovementSettingsInformation { get; set; } = new Dictionary<char, MovementSettingsInfo>();

    }

    public class MovementSettingsInfo
    {
        public float TargetSpeed { get; set; }
        public float TargetAcceleration { get; set; }
        public float TargetDeceleration { get; set; }
    }
}
