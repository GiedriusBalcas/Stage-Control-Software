using standa_control_software_WPF.view_models;
using standa_control_software_WPF.view_models.stores;
using standa_control_software_WPF.view_models.system_control;
using standa_control_software_WPF.views;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using System.Configuration;
using System.Data;
using System.Windows;

namespace standa_control_software_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly NavigationStore _navigationStore;
        private readonly NavigationStore _lscNavigationStore;
        private readonly ConfigurationCreationViewModel _configCreationViewModel;

        private ControllerManager _controllerManager;
        private CommandManager _commandManager;
        private SystemPropertiesViewModel _systemPropertiesViewModel;
        private SystemControlViewModel _systemControlViewModel;
        private SystemInformtaionViewModel _systemInformationViewModel;
        private SystemControlMainViewModel _systemControlMainViewModel;

        public App()
        {
            _navigationStore = new NavigationStore();
            _lscNavigationStore = new NavigationStore();
            _configCreationViewModel = new ConfigurationCreationViewModel(OnInitializationComplete);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            _navigationStore.CurrentViewModel = _configCreationViewModel;

            MainWindow = new MainView()
            {
                DataContext = new MainViewModel(_navigationStore)
            };
            MainWindow.Show();

            base.OnStartup(e);
        }

        private void OnInitializationComplete(ControllerManager controllerManager)
        {
            _controllerManager = controllerManager;
            _commandManager = new CommandManager(_controllerManager);
            _systemPropertiesViewModel = new SystemPropertiesViewModel(_controllerManager);
            _systemControlViewModel = new SystemControlViewModel(_controllerManager,_commandManager);
            _systemInformationViewModel = new SystemInformtaionViewModel();


            _lscNavigationStore.CurrentViewModel = _systemControlViewModel;
            _systemControlMainViewModel = new SystemControlMainViewModel(_commandManager, _lscNavigationStore, GetSystemConfigurationsViewModel, GetSystemInformtaionViewModel, GetSystemCompilerViewModel);

            _navigationStore.CurrentViewModel = _systemControlMainViewModel;
        }



        private SystemPropertiesViewModel GetSystemConfigurationsViewModel()
        {
            return _systemPropertiesViewModel;
        }
        private SystemInformtaionViewModel GetSystemInformtaionViewModel()
        {
            return _systemInformationViewModel;
        }
        private SystemControlViewModel GetSystemCompilerViewModel()
        {
            return _systemControlViewModel;
        }

    }

}
