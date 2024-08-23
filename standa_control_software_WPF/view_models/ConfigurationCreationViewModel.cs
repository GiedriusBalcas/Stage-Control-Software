
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

namespace standa_control_software_WPF.view_models
{
    public class ConfigurationCreationViewModel : ViewModelBase
    {
        private readonly Action _onInitializationComple;
        private ControllerManager _controllerManager;

        public ObservableCollection<ConfigurationViewModel> Configurations { get; set; }
        public ConfigurationViewModel Configuration
        {
            get
            {
                return Configurations.FirstOrDefault();
            }
            set
            {
                Configurations[0] = value;
            }
        }

        private ConfigurationData _configurationData;

        private ViewModelBase _currentViewModel;
        private object _selectedItem;
        private readonly Action<ControllerManager> _onInitializationComplete;

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


        public ICommand CreateConfigInstanceCommand { get; set; }
        public ICommand SaveConfigurationsCommand { get; set; }
        public ICommand SaveAsConfigurationsCommand { get; set; }
        public ICommand LoadConfigurationsCommand { get; set; }

        public ICommand CompleteInitializationCommand { get; }


        public ConfigurationCreationViewModel(Action<ControllerManager> onInitializationCompleted)
        {
            _onInitializationComplete = onInitializationCompleted;

            _controllerManager = new ControllerManager();
            Configurations = new ObservableCollection<ConfigurationViewModel> { new ConfigurationViewModel(this) };

            _configurationData = new ConfigurationData();

            LoadConfigurationsCommand = new RelayCommand(LoadConfigurations);
            SaveConfigurationsCommand = new RelayCommand(SaveConfigurationsExecute);
            SaveAsConfigurationsCommand = new RelayCommand(SaveAsConfigurationsExecute);
            CreateConfigInstanceCommand = new RelayCommand(ExecuteCreateConfigsInstance);

        }

        private void SaveConfigurationsExecute()
        {
            if (!string.IsNullOrEmpty(_configurationData.Filepath))
            {
                if (Configurations.Count > 0)
                {
                    var serConfig = SerializationHelper.CreateSeriazableObject(Configurations.First());

                    var filePath = _configurationData.Filepath;
                    var json = System.Text.Json.JsonSerializer.Serialize(serConfig);
                    File.WriteAllText(filePath, json);
                }
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

            if (saveFileDialog.ShowDialog() == true)
            {
                if (Configurations.Count > 0)
                {
                    var filePath = saveFileDialog.FileName;

                    var serConfig = SerializationHelper.CreateSeriazableObject(Configurations.First());
                    var json = System.Text.Json.JsonSerializer.Serialize(serConfig);
                    var fileName = Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                    File.WriteAllText(filePath, json);
                    _configurationData.Name = fileName;
                    _configurationData.Filepath = filePath;
                    Configuration.Name = fileName;
                }
            }
        }

        private void ExecuteCreateConfigsInstance()
        {
            try
            {

                ControllerManager controllerMangerInstance = new ControllerManager()
                {
                    Name = Configuration.Name
                };

                foreach (var controller in Configuration.Controllers)
                {
                    if (controller.IsEnabled)
                    {

                        var controllerInstance = controller.ExtractController();
                        foreach (var device in controller.Devices)
                        {
                            if (device.IsEnabled)
                            {
                                var deviceInstance = device.ExtractDevice(controllerInstance);
                                controllerInstance.AddDevice(deviceInstance);
                            }
                        }
                        controllerMangerInstance.AddController(controllerInstance);
                    }
                }

                _controllerManager = controllerMangerInstance;

                var shutterDevice = _controllerManager.GetDevices<IShutterDevice>().First();
                var positionerDevices = _controllerManager.GetDevices<IPositionerDevice>();
                var calculator = new ToolPositionCalculator();

                calculator.CreateFunction(Configuration.XToolDependancy, positionerDevices.Select(dev => dev.Name).ToList());
                var funcDelegX = calculator.GetFunction();
                calculator.CreateFunction(Configuration.YToolDependancy, positionerDevices.Select(dev => dev.Name).ToList());
                var funcDelegY = calculator.GetFunction();
                calculator.CreateFunction(Configuration.ZToolDependancy, positionerDevices.Select(dev => dev.Name).ToList());
                var funcDelegZ = calculator.GetFunction();

                Func<Dictionary<string, float>, System.Numerics.Vector3> toolPosFunction = (positions) =>
                {
                    return new System.Numerics.Vector3()
                    {
                        X = funcDelegX.Invoke(positions),
                        Y = funcDelegX.Invoke(positions),
                        Z = funcDelegX.Invoke(positions),
                    };
                };

                var tool = new ToolInformation(
                    _controllerManager.GetDevices<IPositionerDevice>(),
                    shutterDevice,
                    toolPosFunction
                    );

                _controllerManager.ToolInformation = tool;

                _onInitializationComplete.Invoke(_controllerManager);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        public void SaveConfigurations()
        {
            var serConfig = SerializationHelper.CreateSeriazableObject(Configurations.FirstOrDefault());

            var filePath = "configuration.json";
            var json = System.Text.Json.JsonSerializer.Serialize(serConfig);
            File.WriteAllText(filePath, json);
        }




        public void LoadConfigurations()
        {

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Configuration file (*.json)|*.json",
                DefaultExt = "*.json"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var pathToConfig = openFileDialog.FileName;
                //Extract the file name without extension as the document name
                string configName = Path.GetFileNameWithoutExtension(pathToConfig);

                if (File.Exists(pathToConfig))
                {
                    var json = File.ReadAllText(pathToConfig);
                    var configurationSer = JsonConvert.DeserializeObject<ConfigurationSer>(json);
                    var configuration = SerializationHelper.DeserializeObject(configurationSer, this);

                    Configuration = configuration;
                    _configurationData.Filepath = pathToConfig;
                    _configurationData.Name = configName;
                    Configuration.Name = configName;
                }
            }

        }

        internal void ClearConfiguration()
        {
            Configurations.Clear();
            Configurations.Add(new ConfigurationViewModel(this));
        }
    }
}
