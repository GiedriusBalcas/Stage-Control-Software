using System.Windows.Input;
using System.Windows;
using standa_control_software_WPF.view_models.commands;
using standa_control_software_WPF.view_models.config_creation.system_properties;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;
using standa_controller_software.device_manager.controller_interfaces;
using standa_controller_software.device_manager.controller_interfaces.positioning;

namespace standa_control_software_WPF.view_models.system_control
{
    public class SystemPropertiesViewModel : ViewModelBase
    {

        private readonly ControllerManager _controllerManager;
        private readonly standa_controller_software.command_manager.CommandManager _commandManager;
        private DevicePropViewModel _selectedDevice;


        // Ill need once again DeviceVM.
        public List<DevicePropViewModel> Devices { get; set; }

        public DevicePropViewModel SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_selectedDevice != value)
                {
                    _selectedDevice = value;
                    OnPropertyChanged(nameof(SelectedDevice));
                    // Trigger update for DeviceProperties when SelectedDevice changes
                    OnPropertyChanged(nameof(DeviceProperties));
                }
            }
        }

        // This property is used to bind to the second ListView's ItemsSource
        public IEnumerable<DevicePropertyDisplayItem> DeviceProperties => SelectedDevice?.DeviceProperties;

        public ICommand ConnectAllCommand { get; set; }
        public ICommand ConnectCommand { get; set; }

        public SystemPropertiesViewModel(ControllerManager controllerManager, standa_controller_software.command_manager.CommandManager commandManager)
        {
            _controllerManager = controllerManager;
            _commandManager = commandManager;

            Devices = new List<DevicePropViewModel>();

            foreach (var device in _controllerManager.GetDevices<BaseDevice>())
            {
                Devices.Add(new DevicePropViewModel(_controllerManager, _commandManager, device));
            }

            SelectedDevice = Devices.FirstOrDefault();

            ConnectAllCommand = new RelayCommand(ExecuteConnectAllCommand);
            ConnectCommand = new RelayCommand(ExecuteConnectCommandAsync);

        }

        private async void ExecuteConnectCommandAsync()
        {
            try
            {
                await ConnectDevice(SelectedDevice);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async Task ConnectDevice(DevicePropViewModel device)
        {
            if (_controllerManager.TryGetDeviceController<BaseController>(device.Name, out var controller)
                                && _controllerManager.ControllerLocks.TryGetValue(controller.Name, out var semaphore))
            {
                await semaphore.WaitAsync();

                device.ConnectAsync(semaphore);

                if (semaphore.CurrentCount == 0)
                    semaphore.Release();

            }
            else
                throw new Exception($"Unable to connect device: {SelectedDevice.Name}.");
        }

        private async void ExecuteConnectAllCommand()
        {
            try
            {
                var tasks = new List<Task>();
                Devices.ForEach(device => tasks.Add(ConnectDevice(device)));
                
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
