using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;

namespace standa_control_software_WPF.view_models.system_control.information
{
    /// <summary>
    /// Represents the base view model for a device, providing common properties and abstract methods
    /// for managing device acquisitions and updates.
    /// </summary>
    public abstract class DeviceViewModel : ViewModelBase
    {
        protected bool _needsToBeTracked;
        protected readonly standa_controller_software.command_manager.CommandManager _commandManager;
        protected readonly ControllerManager _controllerManager;

        public char Name { get; set; }
        public bool IsConnected { get; protected set; }
        public bool NeedsToBeTracked
        {
            get => _needsToBeTracked;
            set
            {
                if (_needsToBeTracked != value)
                {
                    _needsToBeTracked = value;
                    OnPropertyChanged(nameof(NeedsToBeTracked));
                }
            }
        }
        public DeviceViewModel(BaseDevice device, standa_controller_software.command_manager.CommandManager commandManager, ControllerManager controllerManager)
        {
            Name = device.Name;
            _commandManager = commandManager;
            _controllerManager = controllerManager;
        }
        /// <summary>
        /// Initiates the data acquisition process for the device.
        /// </summary>
        public abstract void StartAcquisition();
        /// <summary>
        /// Terminates the data acquisition process for the device.
        /// </summary>
        public abstract void StopAcquisition();
        /// <summary>
        /// Updates the view model's state based on the latest data from the device.
        /// </summary>
        public abstract void UpdateFromDevice(BaseDevice device);
    }
}
