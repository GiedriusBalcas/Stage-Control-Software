using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.system_control.information
{
    public class PositionerDeviceViewModel : DeviceViewModel
    {
        private float _position;
        public float Position
        {
            get => _position;
            set
            {
                _position = value;
                OnPropertyChanged(nameof(Position));
            }
        }

        public ICommand StopCommand { get; set; }
        public ICommand HomeCommand { get; set; }
        public ICommand MoveCommand { get; set; }
        public ICommand ShiftCommand { get; set; }

        public override void UpdateFromDevice(BaseDevice device)
        {
            if (device is BasePositionerDevice positionerDevice)
            {
                Position = positionerDevice.CurrentPosition;
            }
        }
    }
}
