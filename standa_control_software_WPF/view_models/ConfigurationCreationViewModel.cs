using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Windows;
using standa_controller_software.device_manager;
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.device_manager.devices;
using ToolDependancyBuilder;
using standa_control_software_WPF.view_models.config_creation.serialization_helpers;
using standa_control_software_WPF.view_models.config_creation;
using standa_controller_software.device_manager.devices.shutter;
using standa_controller_software.device_manager.controller_interfaces.master_controller;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using standa_controller_software.command_manager;
using standa_control_software_WPF.view_models.stores;
using Microsoft.Extensions.Logging;

namespace standa_control_software_WPF.view_models
{
    public class ConfigurationCreationViewModel : ViewModelBase
    {
        private ConfigurationData _configurationData;
        private ViewModelBase _currentViewModel;
        private object _selectedItem;

        private readonly SerializationHelper _serializationHelper;
        private readonly ControllerManager _controllerManager;
        private readonly ILogger<ConfigurationCreationViewModel> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly NavigationStore _navigationStore;

        // This is our new no-arg callback for when the wizard finishes
        private readonly Action _onWizardComplete;

        public ObservableCollection<ConfigurationViewModel> Configurations { get; set; }

        public ConfigurationViewModel Configuration
        {
            get => Configurations.FirstOrDefault();
            set => Configurations[0] = value;
        }

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                _currentViewModel = value;
                OnPropertyChanged(nameof(CurrentViewModel));
            }
        }

        public object SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    OnPropertyChanged(nameof(SelectedItem));
                }
            }
        }

        // Commands
        public ICommand CreateConfigInstanceCommand { get; }
        public ICommand SaveConfigurationsCommand { get; }
        public ICommand SaveAsConfigurationsCommand { get; }
        public ICommand LoadConfigurationsCommand { get; }

        public ConfigurationCreationViewModel(
            ControllerManager manager,
            ILogger<ConfigurationCreationViewModel> logger,
            ILoggerFactory loggerFactory,
            NavigationStore navigationStore,
            Action onWizardComplete
        )
        {
            _controllerManager = manager;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _navigationStore = navigationStore;
            _onWizardComplete = onWizardComplete;

            _serializationHelper = new SerializationHelper(loggerFactory.CreateLogger<SerializationHelper>(), _loggerFactory);
            Configurations = new ObservableCollection<ConfigurationViewModel>
            {
                new ConfigurationViewModel(this, loggerFactory.CreateLogger<ConfigurationViewModel>(), _loggerFactory)
            };

            _configurationData = new ConfigurationData();

            // Initialize commands
            LoadConfigurationsCommand = new RelayCommand(LoadConfigurations);
            SaveConfigurationsCommand = new RelayCommand(SaveConfigurationsExecute);
            SaveAsConfigurationsCommand = new RelayCommand(SaveAsConfigurationsExecute);
            CreateConfigInstanceCommand = new RelayCommand(
                ExecuteCreateConfigsInstance,
                CanCreateConfigurationInstance
            );
        }

        private bool CanCreateConfigurationInstance()
        {
            if (Configuration.Controllers.Count < 1)
                return false;

            if (string.IsNullOrWhiteSpace(Configuration.XToolDependancy) ||
                string.IsNullOrWhiteSpace(Configuration.YToolDependancy) ||
                string.IsNullOrWhiteSpace(Configuration.ZToolDependancy))
                return false;

            return true;
        }

        private void ExecuteCreateConfigsInstance()
        {
            try
            {
                // 1) Clear & set manager name (since user might re-run wizard)
                //_controllerManager.ClearControllers();
                _controllerManager.Name = Configuration.Name;

                // 2) Add controllers/devices
                foreach (var ctrlModel in Configuration.Controllers)
                {
                    if (!ctrlModel.IsEnabled)
                        continue;

                    var controllerInstance = ctrlModel.ExtractController();
                    foreach (var deviceModel in ctrlModel.Devices.Where(d => d.IsEnabled))
                    {
                        var deviceInstance = deviceModel.ExtractDevice(controllerInstance);
                        controllerInstance.AddDevice(deviceInstance);
                    }
                    _controllerManager.AddController(controllerInstance);
                }

                // 3) Master/Slave
                foreach (var ctrlModel in Configuration.Controllers)
                {
                    if (string.IsNullOrWhiteSpace(ctrlModel.SelectedMasterControllerName))
                        continue;

                    var selectedMaster = _controllerManager.Controllers.Values
                        .FirstOrDefault(c => c.Name == ctrlModel.SelectedMasterControllerName);

                    if (selectedMaster is BaseMasterController masterController)
                    {
                        _controllerManager.Controllers[ctrlModel.Name].MasterController = masterController;
                        masterController.AddSlaveController(
                            _controllerManager.Controllers[ctrlModel.Name],
                            _controllerManager.ControllerLocks[ctrlModel.Name]
                        );
                    }
                }

                // 4) Tool creation
                var shutterDevice = _controllerManager.GetDevices<BaseShutterDevice>().FirstOrDefault()
                    ?? new ShutterDevice('s', "undefined");
                var positioners = _controllerManager.GetDevices<BasePositionerDevice>();

                var calculator = new ToolPositionCalculator(_loggerFactory.CreateLogger<ToolPositionCalculator>());

                // build X function
                calculator.CreateFunction(Configuration.XToolDependancy, positioners.Select(d => d.Name).ToList());
                var funcX = calculator.GetFunction();
                // Y
                calculator.CreateFunction(Configuration.YToolDependancy, positioners.Select(d => d.Name).ToList());
                var funcY = calculator.GetFunction();
                // Z
                calculator.CreateFunction(Configuration.ZToolDependancy, positioners.Select(d => d.Name).ToList());
                var funcZ = calculator.GetFunction();

                if (funcX is null || funcY is null || funcZ is null)
                    throw new ArgumentNullException("Tool position is not defined.");

                var tool = new ToolInformation(
                    positioners,
                    shutterDevice,
                    positions => new System.Numerics.Vector3
                    {
                        X = funcX(positions),
                        Y = funcY(positions),
                        Z = funcZ(positions)
                    }
                )
                {
                    MinimumCoordinates = new System.Numerics.Vector3
                    {
                        X = Configuration.MinimumPositionX,
                        Y = Configuration.MinimumPositionY,
                        Z = Configuration.MinimumPositionZ
                    },
                    MaximumCoordinates = new System.Numerics.Vector3
                    {
                        X = Configuration.MaximumPositionX,
                        Y = Configuration.MaximumPositionY,
                        Z = Configuration.MaximumPositionZ
                    },
                };

                _controllerManager.ToolInformation = tool;

                // 5) All done! Notify the App that the wizard is finished
                _onWizardComplete?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private void SaveConfigurationsExecute()
        {
            if (!string.IsNullOrEmpty(_configurationData.Filepath) && Configurations.Any())
            {
                var serConfig = _serializationHelper.CreateSeriazableObject(Configurations.First());
                var filePath = _configurationData.Filepath;
                var json = System.Text.Json.JsonSerializer.Serialize(serConfig);
                File.WriteAllText(filePath, json);
            }
            else
            {
                SaveAsConfigurationsExecute();
            }
        }

        private void SaveAsConfigurationsExecute()
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Configuration file (*.json)|*.json",
                DefaultExt = "*.json",
                FileName = Configuration.Name,
                AddExtension = true
            };

            if (saveFileDialog.ShowDialog() == true && Configurations.Any())
            {
                var filePath = saveFileDialog.FileName;
                var serConfig = _serializationHelper.CreateSeriazableObject(Configurations.First());
                var json = System.Text.Json.JsonSerializer.Serialize(serConfig);
                var fileName = Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                File.WriteAllText(filePath, json);
                _configurationData.Name = fileName;
                _configurationData.Filepath = filePath;
                Configuration.Name = fileName;
            }
        }

        private void LoadConfigurations()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Configuration file (*.json)|*.json",
                DefaultExt = "*.json"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var pathToConfig = openFileDialog.FileName;
                if (!File.Exists(pathToConfig)) return;

                string configName = Path.GetFileNameWithoutExtension(pathToConfig);
                var json = File.ReadAllText(pathToConfig);
                var configurationSer = JsonConvert.DeserializeObject<ConfigurationSer>(json);
                var configuration = _serializationHelper.DeserializeObject(configurationSer, this);

                Configuration = configuration;
                _configurationData.Filepath = pathToConfig;
                _configurationData.Name = configName;
                Configuration.Name = configName;
            }
        }

        internal void ClearConfiguration()
        {
            Configurations.Clear();
            Configurations.Add(new ConfigurationViewModel(this, _loggerFactory.CreateLogger<ConfigurationViewModel>(), _loggerFactory));
        }
    }
}
