using Microsoft.VisualBasic.Logging;
using standa_control_software_WPF.view_models.commands;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.config_creation
{
    public class ConfigurationViewModel : ViewModelBase
    {
        public readonly ConfigurationCreationViewModel ConfigManager;
        private readonly ConcurrentQueue<string> _log;

        private string _name;
        private string _xToolDependancy;
        private string _yToolDependancy;
        private string _zToolDependancy;
        public string Name
        {
            get
            {
                if (_name == string.Empty || _name is null)
                    return "Undefined";
                return _name;
            }
            set
            {
                if(value != "" && value != string.Empty)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }
        public string XToolDependancy
        {
            get
            {
                return _xToolDependancy;
            }
            set
            {
                if (_xToolDependancy != value)
                {
                    _xToolDependancy = value;
                    OnPropertyChanged(nameof(XToolDependancy));
                }
            }
        }

        public string YToolDependancy
        {
            get
            {
                return _yToolDependancy;
            }
            set
            {
                if (_yToolDependancy != value)
                {
                    _yToolDependancy = value;
                    OnPropertyChanged(nameof(YToolDependancy));
                }
            }
        }

        public string ZToolDependancy
        {
            get
            {
                return _zToolDependancy;
            }
            set
            {
                if (_zToolDependancy != value)
                {
                    _zToolDependancy = value;
                    OnPropertyChanged(nameof(ZToolDependancy));
                }
            }
        }


        public ObservableCollection<ControllerConfigViewModel> Controllers { get; set; } = new ObservableCollection<ControllerConfigViewModel>();

        public ICommand ClearConfigurationCommand { get; set; }
        public ICommand AddControllerCommand { get; set; }

        public ConfigurationViewModel(ConfigurationCreationViewModel systemConfigurations, ConcurrentQueue<string> log)
        {
            _log = log;
            Name = string.Empty;

            ConfigManager = systemConfigurations;

            ClearConfigurationCommand = new RelayCommand<ConfigurationViewModel>(ExecuteClearConfiguration);
            AddControllerCommand = new RelayCommand(ExecuteAddController);
        }

        private void ExecuteAddController()
        {
            Controllers.Add(new ControllerConfigViewModel(this, _log) { Name = "new Controller" });
        }

        private void ExecuteClearConfiguration(ConfigurationViewModel configuration)
        {
            ConfigManager.ClearConfiguration();
        }

        public IEnumerable<char> GetAllDeviceNames()
        {
            return Controllers.SelectMany(controller => controller.Devices.Select(device => device.Name)).Distinct();
        }


    }
}
