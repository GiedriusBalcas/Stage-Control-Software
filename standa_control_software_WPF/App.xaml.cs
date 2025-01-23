using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using standa_control_software_WPF.view_models;
using standa_control_software_WPF.view_models.stores;
using standa_control_software_WPF.view_models.system_control;
using standa_control_software_WPF.views;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using System.Collections.Concurrent;
using System.Windows;
using System;
using Microsoft.Extensions.Logging;
using standa_control_software_WPF.view_models.logging;
using standa_control_software_WPF.views.system_control;

namespace standa_control_software_WPF
{
    public partial class App : Application
    {
        private IHost _host;
        public IHost HostHandle { get => _host;}

        private MainNavigationStore _mainNavigationStore; // top-level store
        private LSCNavigationStore _lscNavigationStore;   // child-level store

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1) Build the Host + DI container
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Logging services
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.SetMinimumLevel(LogLevel.Debug);
                        logging.AddFilter("standa_controller_software.device_manager.controller_interfaces.positioning.PositionerController_Sim", LogLevel.Information);

                        var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log_on_demand.txt");
                        logging.AddOnDemandFileLogger(logPath, LogLevel.Debug);
                    });


                    // Register your two distinct NavigationStores
                    services.AddSingleton<MainNavigationStore>();
                    services.AddSingleton<LSCNavigationStore>();

                    // Manager classes (one shared manager in DI)
                    services.AddSingleton<ControllerManager>();
                    services.AddSingleton<CommandManager>();
                    services.AddSingleton<ControllerStateUpdater>();

                    // 2) Register your Wizard (ConfigurationCreationViewModel)
                    //    We inject the MainNavigationStore into it if needed, or it can just do 
                    //    the config. We also pass an onWizardComplete callback to navigate.
                    services.AddSingleton<ConfigurationCreationViewModel>(sp =>
                    {
                        var manager = sp.GetRequiredService<ControllerManager>();
                        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                        var logger = loggerFactory.CreateLogger<ConfigurationCreationViewModel>();

                        var mainNav = sp.GetRequiredService<MainNavigationStore>();

                        Action onWizardComplete = () =>
                        {
                            var app = (App)Application.Current;
                            app.OnInitializationComplete();
                        };

                        // This VM configures the manager, then calls onWizardComplete
                        return new ConfigurationCreationViewModel(manager, logger, loggerFactory, mainNav, onWizardComplete);
                    });

                    // 3) Register child-level VMs
                    services.AddSingleton<SystemPropertiesViewModel>();
                    services.AddSingleton<SystemInformtaionViewModel>();
                    services.AddSingleton<SystemControlViewModel>();

                    // 4) SystemControlMainViewModel uses LSCNavigationStore for its "CurrentViewModel"
                    //    so it can display SystemPropertiesVM / SystemInformtaionVM / SystemControlVM
                    services.AddSingleton<SystemControlMainViewModel>(sp =>
                    {
                        var cmdManager = sp.GetRequiredService<CommandManager>();
                        var lscNav = sp.GetRequiredService<LSCNavigationStore>(); // important!

                        Func<SystemPropertiesViewModel> getConfigVm =
                            () => sp.GetRequiredService<SystemPropertiesViewModel>();
                        Func<SystemInformtaionViewModel> getInfoVm =
                            () => sp.GetRequiredService<SystemInformtaionViewModel>();
                        Func<SystemControlViewModel> getControlVm =
                            () => sp.GetRequiredService<SystemControlViewModel>();

                        return new SystemControlMainViewModel(
                            cmdManager,
                            lscNav,  // NOTICE we use LSC store
                            getConfigVm,
                            getInfoVm,
                            getControlVm
                        );
                    });

                    // 5) Main Window + MainViewModel uses the MainNavigationStore
                    services.AddSingleton<MainView>();
                    services.AddSingleton<MainViewModel>(sp =>
                    {
                        var mainNavStore = sp.GetRequiredService<MainNavigationStore>();
                        return new MainViewModel(mainNavStore);
                    });
                })
                .Build();

            // 2) Start the Host
            await _host.StartAsync();

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var logger = _host.Services.GetRequiredService<ILogger<App>>();
                logger.LogCritical(ex.ExceptionObject as Exception, "An unhandled exception occurred.");
            };

            this.DispatcherUnhandledException += (s, ex) =>
            {
                var logger = _host.Services.GetRequiredService<ILogger<App>>();
                logger.LogCritical(ex.Exception, "A dispatcher unhandled exception occurred.");
                ex.Handled = true;
            };

            // 3) Retrieve references from DI
            _mainNavigationStore = _host.Services.GetRequiredService<MainNavigationStore>();
            _lscNavigationStore = _host.Services.GetRequiredService<LSCNavigationStore>();

            // Show the wizard first:
            var configCreationVm = _host.Services.GetRequiredService<ConfigurationCreationViewModel>();
            _mainNavigationStore.CurrentViewModel = configCreationVm; // top-level nav store

            // Set up the MainWindow
            MainWindow = _host.Services.GetRequiredService<MainView>();
            MainWindow.DataContext = _host.Services.GetRequiredService<MainViewModel>();
            MainWindow.Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }

        /// <summary>
        /// Called after the wizard finishes configuring the Manager
        /// </summary>
        private void OnInitializationComplete()
        {
            try
            {
                // The manager is now fully configured by the wizard
                var kaka = _host.Services.GetRequiredService<ControllerManager>();

                var controllerStateUpdater = _host.Services.GetRequiredService<ControllerStateUpdater>();
                var systemControlMainVM = _host.Services.GetRequiredService<SystemControlMainViewModel>();

                // 1) The child store (lsc) can show one of the child viewmodels, e.g. SystemPropertiesVM:
                // _lscNavigationStore.CurrentViewModel = _host.Services.GetRequiredService<SystemPropertiesViewModel>();
                // or whichever child you want to show first:
                _lscNavigationStore.CurrentViewModel = _host.Services.GetRequiredService<SystemPropertiesViewModel>();

                // 2) Now switch the top-level store from the wizard to SystemControlMainViewModel
                _mainNavigationStore.CurrentViewModel = systemControlMainVM;

                // 3) Start background updates
                _ = controllerStateUpdater.UpdateStatesAsync();
            }
            catch (Exception ex)
            {
                var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("General");
                logger.LogError("Fatal error encountered in System Control Window");
                logger.LogError($"{ex.Message} | {ex.StackTrace}");
                MessageBox.Show(ex.Message);

            }
        }
    }
}
