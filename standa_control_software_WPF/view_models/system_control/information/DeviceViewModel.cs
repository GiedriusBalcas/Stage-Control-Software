using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_control_software_WPF.view_models.system_control.information    
{
    public abstract class DeviceViewModel : ViewModelBase
    {
        public char Name { get; set; }
        public bool IsConnected { get; protected set; }

        public abstract void UpdateFromDevice(BaseDevice device);
        public DeviceViewModel(BaseDevice device)
        {
            Name = device.Name;
        }

    }
}
