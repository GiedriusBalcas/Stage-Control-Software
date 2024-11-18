using standa_controller_software.painter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Navigation;

namespace standa_control_software_WPF.view_models.system_control.control
{
    public class CameraViewModel : ViewModelBase
    {
        private readonly PainterManager _painterManager;

        private float _yaw;
        private float _pitch;
        private float _distance;
        private float _fovy;

        public bool IsTrackingTool
        {
            get => IsTrackingTool;
            set
            {
                IsTrackingTool = value;
                OnPropertyChanged(nameof(IsTrackingTool));
            }
        }
        public bool IsOrthographicView
        {
            get => IsOrthographicView;
            set
            {
                IsOrthographicView = value;
                OnPropertyChanged(nameof(IsOrthographicView));
            }
        }
        public ICommand CameraViewXYCommand { get; set; }
        public ICommand CameraViewXZCommand { get; set; }
        public ICommand CameraViewYZCommand { get; set; }
        public ICommand CameraFitObjectCommand { get; set; }

        public CameraViewModel(PainterManager painterManager)
        {
            _painterManager = painterManager;
        }
    }
}
