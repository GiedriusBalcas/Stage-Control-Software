using standa_controller_software.device_manager;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.system_control.information    
{
    public abstract class DeviceViewModel : ViewModelBase
    {
        protected bool _needsToBeTracked;

        public char Name { get; set; }

        protected readonly standa_controller_software.command_manager.CommandManager _commandManager;
        protected readonly ControllerManager _controllerManager;

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
        public abstract void StartAcquisition();
        public abstract void StopAcquisition();
        public abstract void UpdateFromDevice(BaseDevice device);
        public DeviceViewModel(BaseDevice device, standa_controller_software.command_manager.CommandManager commandManager, ControllerManager controllerManager)
        {
            Name = device.Name;
            _commandManager = commandManager;
            _controllerManager = controllerManager;
        }

    }
}
