using standa_control_software_WPF.view_models;
using standa_control_software_WPF.view_models.stores;
using standa_control_software_WPF.view_models.system_control;
using standa_control_software_WPF.views;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using System.Collections.Concurrent;
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
        private ControllerStateUpdater _controllerStateUpdater;
        private ConcurrentQueue<string> _log;


        private SystemPropertiesViewModel _systemPropertiesViewModel;
        private SystemControlViewModel _systemControlViewModel;
        private SystemInformtaionViewModel _systemInformationViewModel;
        private SystemControlMainViewModel _systemControlMainViewModel;

        public App()
        {
            _log = new ConcurrentQueue<string>();
            _navigationStore = new NavigationStore();
            _lscNavigationStore = new NavigationStore();
            _configCreationViewModel = new ConfigurationCreationViewModel(OnInitializationComplete, _log);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                _navigationStore.CurrentViewModel = _configCreationViewModel;

                MainWindow = new MainView()
                {
                    DataContext = new MainViewModel(_navigationStore)
                };
                MainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                _log.Enqueue("Fatal error encountered in System Configuration Window.");
                _log.Enqueue(ex.Message);
            }
        }

        private void OnInitializationComplete(ControllerManager controllerManager)
        {
            try
            {
                _controllerManager = controllerManager;
                _controllerStateUpdater = new ControllerStateUpdater(_controllerManager, _log);
                _commandManager = new CommandManager(_controllerManager, _log);

                _systemPropertiesViewModel = new SystemPropertiesViewModel(_controllerManager, _commandManager);
                _systemControlViewModel = new SystemControlViewModel(_controllerManager,_commandManager, _log);
                _systemInformationViewModel = new SystemInformtaionViewModel(_controllerManager, _commandManager);


                _lscNavigationStore.CurrentViewModel = _systemPropertiesViewModel;
                _systemControlMainViewModel = new SystemControlMainViewModel(_commandManager, _lscNavigationStore, GetSystemConfigurationsViewModel, GetSystemInformtaionViewModel, GetSystemCompilerViewModel);

                _navigationStore.CurrentViewModel = _systemControlMainViewModel;

                _ = _controllerStateUpdater.UpdateStatesAsync();
            }
            catch(Exception ex)
            {
                _log.Enqueue("Fatal error encountered in System Control Window");
                _log.Enqueue(ex.Message);
            }
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
