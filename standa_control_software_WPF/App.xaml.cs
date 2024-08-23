using standa_control_software_WPF.view_models;
using standa_control_software_WPF.view_models.config_creation;
using standa_control_software_WPF.view_models.stores;
using standa_control_software_WPF.views;
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
        private readonly ConfigurationCreationViewModel _configCreationViewModel;

        public App()
        {
            _configCreationViewModel = new ConfigurationCreationViewModel(OnInitializationComplete);
            _navigationStore = new NavigationStore();
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

        private void OnInitializationComplete(ControllerManager config)
        {
            //_config = config;
            //_configViewModel = new SystemPropertiesViewModel(_config);
            //_compilerViewModel = new SystemCompilerViewModel(_config);
            //_infoViewModel = new SystemInformtaionViewModel(_config, _compilerViewModel);


            //_lscNavigationStore.CurrentViewModel = _configViewModel;
            //_lscViewModel = new LSCViewModel(_config, _lscNavigationStore, GetSystemConfigurationsViewModel, GetSystemInformtaionViewModel, GetSystemCompilerViewModel);

            //_navigationStore.CurrentViewModel = _lscViewModel;
        }

    }

}
