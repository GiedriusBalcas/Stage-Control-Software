using Microsoft.Extensions.Logging;
using standa_control_software_WPF.view_models.commands;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.config_creation
{
    public class ConfigurationViewModel : ViewModelBase
    {
        public readonly ConfigurationCreationViewModel ConfigManager;

        private readonly ILogger<ConfigurationViewModel> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private string _name = "Undefined";
        private string _xToolDependancy = "";
        private string _yToolDependancy = "";
        private string _zToolDependancy = "";
        private float _minimumPositionX;
        private float _maximumPositionX;
        private float _minimumPositionY;
        private float _maximumPositionY;
        private float _minimumPositionZ;
        private float _maximumPositionZ;
        
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
        public float MinimumPositionX
        {
            get { return _minimumPositionX; }
            set
            {
                _minimumPositionX = value;
                OnPropertyChanged(nameof(MinimumPositionX));
            }
        }
        public float MaximumPositionX
        {
            get { return _maximumPositionX; }
            set
            {
                _maximumPositionX = value;
                OnPropertyChanged(nameof(MaximumPositionX));
            }
        }
        public float MinimumPositionY
        {
            get { return _minimumPositionY; }
            set
            {
                _minimumPositionY = value;
                OnPropertyChanged(nameof(MinimumPositionY));
            }
        }
        public float MaximumPositionY
        {
            get { return _maximumPositionY; }
            set
            {
                _maximumPositionY = value;
                OnPropertyChanged(nameof(MaximumPositionY));
            }
        }
        public float MinimumPositionZ
        {
            get { return _minimumPositionZ; }
            set
            {
                _minimumPositionZ = value;
                OnPropertyChanged(nameof(MinimumPositionZ));
            }
        }
        public float MaximumPositionZ
        {
            get { return _maximumPositionZ; }
            set
            {
                _maximumPositionZ = value;
                OnPropertyChanged(nameof(MaximumPositionZ));
            }
        }

        public ObservableCollection<ControllerConfigViewModel> Controllers { get; set; } = new ObservableCollection<ControllerConfigViewModel>();

        public ICommand ClearConfigurationCommand { get; set; }
        public ICommand AddControllerCommand { get; set; }

        public ConfigurationViewModel(ConfigurationCreationViewModel systemConfigurations, ILogger<ConfigurationViewModel> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            
            ConfigManager = systemConfigurations;

            ClearConfigurationCommand = new RelayCommand<ConfigurationViewModel>(ExecuteClearConfiguration);
            AddControllerCommand = new RelayCommand(ExecuteAddController);
        }

        private void ExecuteAddController()
        {
            Controllers.Add(new ControllerConfigViewModel(this, _loggerFactory) { Name = "new Controller" });
        }
        private void ExecuteClearConfiguration(ConfigurationViewModel configuration)
        {
            ConfigManager.ClearConfiguration();
        }



    }
}
