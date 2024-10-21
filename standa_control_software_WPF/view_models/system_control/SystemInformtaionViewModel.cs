

using standa_control_software_WPF.view_models.commands;
using standa_control_software_WPF.view_models.system_control.information;
using standa_control_software_WPF.view_models.system_control.information;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.system_control
{
    public class SystemInformtaionViewModel : ViewModelBase
    {
        private readonly ControllerManager _controllerManager;

        private double _acquisitionDuration;
        public double AcquisitionDuration
        {
            get => _acquisitionDuration;
            set
            {
                _acquisitionDuration = value;
                OnPropertyChanged(nameof(AcquisitionDuration));
            }
        }

        public ObservableCollection<DeviceViewModel> Devices { get; set; }
        
        public SystemInformtaionViewModel(ControllerManager controllerManager)
        {
            _controllerManager = controllerManager;
            Devices = new ObservableCollection<DeviceViewModel>();

            foreach(BaseDevice device in _controllerManager.GetDevices<BaseDevice>())
            {
                var deviceViewModel = CreateViewModelForDevice(device);
                Devices.Add(deviceViewModel);
            }

        }
        public ICommand AcquireCommand => new RelayCommand(StartAcquisition);

        private void StartAcquisition()
        {
            foreach (var deviceViewModel in Devices.OfType<PositionerDeviceViewModel>())
            {
                if (deviceViewModel.NeedsToBeTracked)
                {
                    deviceViewModel.StartAcquisitionCommand.Execute(null);
                }
            }

            // Stop acquisition after the specified duration
            Task.Delay(TimeSpan.FromSeconds(AcquisitionDuration)).ContinueWith(_ =>
            {
                foreach (var deviceViewModel in Devices.OfType<PositionerDeviceViewModel>())
                {
                    deviceViewModel.StopAcquisitionCommand.Execute(null);
                }
            });
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
                return new PositionerDeviceViewModel(positionerDevice);
            }
            else if (device is BaseShutterDevice shutterDevice)
            {
                return new ShutterDeviceViewModel(shutterDevice);
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
