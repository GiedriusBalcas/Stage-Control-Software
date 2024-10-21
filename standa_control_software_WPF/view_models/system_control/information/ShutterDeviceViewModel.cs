using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.system_control.information
{
    public class ShutterDeviceViewModel : DeviceViewModel
    {
        private bool _state;
        public bool State
        {
            get => _state;
            set
            {
                _state = value;
                OnPropertyChanged(nameof(State));
            }
        }

        public ICommand ToggleCommand { get; set; }

        public override void UpdateFromDevice(BaseDevice device)
        {
            if (device is BaseShutterDevice shutterDevice)
            {
                State = shutterDevice.IsOn;
            }
        }
    }

}
