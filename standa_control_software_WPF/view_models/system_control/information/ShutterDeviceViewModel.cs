using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static ICSharpCode.AvalonEdit.Editing.CaretWeakEventManager;

namespace standa_control_software_WPF.view_models.system_control.information
{
    public class ShutterDeviceViewModel : DeviceViewModel
    {
        private bool _state;
        private readonly BaseShutterDevice _shutter;
        private bool _needsToBeTracked;

        public bool State
        {
            get => _state;
            set
            {
                _state = value;
                OnPropertyChanged(nameof(State));
            }
        }

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
        public ICommand ToggleCommand { get; set; }

        public ShutterDeviceViewModel(BaseDevice device) : base(device)
        {
            if(device is BaseShutterDevice shutter)
            {
                _shutter = shutter;
                State = shutter.IsOn;
                shutter.StateChanged += OnStateChanged; ;
                IsConnected = shutter.IsConnected;
                shutter.ConnectionStateChanged += OnConnectionStateChanged;
            }
        }

        private void OnConnectionStateChanged(object? sender, EventArgs e)
        {
            IsConnected = _shutter.IsConnected;
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            State = _shutter.IsOn;

        }

        public override void UpdateFromDevice(BaseDevice device)
        {
            if (device is BaseShutterDevice shutterDevice)
            {
                State = shutterDevice.IsOn;
            }
        }
    }

}
