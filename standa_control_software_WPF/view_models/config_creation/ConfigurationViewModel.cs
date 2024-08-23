using standa_control_software_WPF.view_models.commands;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.config_creation
{
    public class ConfigurationViewModel : ViewModelBase
    {
        public readonly ConfigurationCreationViewModel ConfigManager;
        
        public string Name
        {
            get
            {
                if (Name == string.Empty || Name is null)
                    return "Undefined";
                return Name;
            }
            set
            {
                Name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public string XToolDependancy
        {
            get
            {
                return XToolDependancy;
            }
            set
            {
                XToolDependancy = value;
                OnPropertyChanged(nameof(XToolDependancy));
            }
        }
        public string YToolDependancy
        {
            get
            {
                return YToolDependancy;
            }
            set
            {
                YToolDependancy = value;
                OnPropertyChanged(nameof(YToolDependancy));
            }
        }
        public string ZToolDependancy
        {
            get
            {
                return ZToolDependancy;
            }
            set
            {
                ZToolDependancy = value;
                OnPropertyChanged(nameof(ZToolDependancy));
            }
        }

        public ObservableCollection<ControllerConfigViewModel> Controllers { get; set; } = new ObservableCollection<ControllerConfigViewModel>();

        public ICommand ClearConfigurationCommand { get; set; }
        public ICommand AddControllerCommand { get; set; }

        public ConfigurationViewModel(ConfigurationCreationViewModel systemConfigurations)
        {
            ConfigManager = systemConfigurations;

            ClearConfigurationCommand = new RelayCommand<ConfigurationViewModel>(ExecuteClearConfiguration);
            AddControllerCommand = new RelayCommand(ExecuteAddController);
        }

        private void ExecuteAddController()
        {
            Controllers.Add(new ControllerConfigViewModel(this) { Name = "new Controller" });
        }

        private void ExecuteClearConfiguration(ConfigurationViewModel configuration)
        {
            ConfigManager.ClearConfiguration();
        }

        public IEnumerable<string> GetAllDeviceNames()
        {
            return Controllers.SelectMany(controller => controller.Devices.Select(device => device.Name)).Distinct();
        }


    }
}
