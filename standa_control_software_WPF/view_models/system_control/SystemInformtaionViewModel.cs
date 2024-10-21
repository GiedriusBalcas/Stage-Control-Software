

using standa_control_software_WPF.view_models.system_control.information;
using standa_control_software_WPF.view_models.system_control.information;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager.devices;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace standa_control_software_WPF.view_models.system_control
{
    public class SystemInformtaionViewModel : ViewModelBase
    {

        public ObservableCollection<DeviceViewModel> Devices { get; set; }
        private readonly ControllerStateUpdater _updater;

        public SystemInformtaionViewModel(ControllerStateUpdater updater)
        {
            Devices = new ObservableCollection<DeviceViewModel>();
            _updater = updater;

            // Subscribe to the DeviceUpdated event
            _updater.DeviceUpdated += OnDeviceUpdated;
        }

        // Event handler for device updates
        private void OnDeviceUpdated(object sender, BaseDevice device)
        {
            // Check if the device already has a corresponding ViewModel in the collection
            var viewModel = Devices.FirstOrDefault(vm => vm.Name == device.Name);

            if (viewModel == null)
            {
                // Create the ViewModel if it's a new device
                viewModel = CreateViewModelForDevice(device);
                Devices.Add(viewModel);
            }

            // Update the ViewModel with the new device state
            viewModel.UpdateFromDevice(device);
        }

        // Factory method to create a ViewModel based on the device type
        private DeviceViewModel CreateViewModelForDevice(BaseDevice device)
        {
            if (device is BasePositionerDevice positionerDevice)
            {
                return new PositionerDeviceViewModel
                {
                    Name = positionerDevice.Name,
                    Position = positionerDevice.CurrentPosition
                };
            }
            else if (device is BaseShutterDevice shutterDevice)
            {
                return new ShutterDeviceViewModel
                {
                    Name = shutterDevice.Name,
                    State = shutterDevice.IsOn
                };
            }

            throw new NotSupportedException("Device type not supported");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


}
