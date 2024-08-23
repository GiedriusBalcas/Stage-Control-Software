using System.Windows.Input;
using System.Windows;


namespace standa_control_software_WPF.view_models.system_control
{
    public class SystemPropertiesViewModel : ViewModelBase
    {

        //private readonly SystemConfig _config;
        //private DevicePropViewModel _selectedDevice;


        //// Ill need once again DeviceVM.
        //public List<DevicePropViewModel> Devices { get; set; }
        
        //public DevicePropViewModel SelectedDevice
        //{
        //    get => _selectedDevice;
        //    set
        //    {
        //        if (_selectedDevice != value)
        //        {
        //            _selectedDevice = value;
        //            OnPropertyChanged(nameof(SelectedDevice));
        //            // Trigger update for DeviceProperties when SelectedDevice changes
        //            OnPropertyChanged(nameof(DeviceProperties));
        //        }
        //    }
        //}

        //// This property is used to bind to the second ListView's ItemsSource
        //public IEnumerable<DevicePropertyDisplayItem> DeviceProperties => SelectedDevice?.DeviceProperties;

        //public ICommand ConnectAllCommand { get; set; }
        //public ICommand ConnectCommand { get; set; }

        //public SystemPropertiesViewModel(SystemConfig config)
        //{
        //    _config = config;

        //    Devices = new List<DevicePropViewModel>();
        //    foreach (var device in _config.GetAllDevices())
        //    {
        //        Devices.Add( new DevicePropViewModel(_config,device) );
        //    }

        //    SelectedDevice = Devices.FirstOrDefault();

        //    ConnectAllCommand = new RelayCommand(ExecuteConnectAllCommand);
        //    ConnectCommand = new RelayCommand(ExecuteConnectCommand);

        //}

        //private void ExecuteConnectCommand()
        //{
        //    try
        //    {
        //        SelectedDevice.Connect();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.Message);
        //    }
        //}

        //private void ExecuteConnectAllCommand()
        //{
        //    try { 
        //        foreach (var device in Devices)
        //        {
        //            device.Connect();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.Message);
        //    }
        //}
    }
}
