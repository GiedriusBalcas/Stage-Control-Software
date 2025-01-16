using OxyPlot;
using OxyPlot.Series;
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.command_manager;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.devices;
using System;
using System.Timers;
using System.Windows.Automation;
using System.Windows.Input;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.custom_functions;
using standa_controller_software.custom_functions.definitions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace standa_control_software_WPF.view_models.system_control.information
{
    public class PositionerDeviceViewModel : DeviceViewModel, IDisposable
    {
        private readonly BasePositionerDevice _positioner;
        private float _position;
        private float _speed;
        private bool _needsToBeTracked;
        private bool _isAcquiring;
        private DateTime _acquisitionStartTime;
        private double _timeElapsed;
        private LineSeries _positionSeries;
        private LineSeries _speedSeries;
        private System.Timers.Timer _plotUpdateTimer;

        public float Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    OnPropertyChanged(nameof(Position));
                }
            }
        }
        public float Speed
        {
            get => _speed;
            set
            {
                if (_speed != value)
                {
                    _speed = value;
                    OnPropertyChanged(nameof(Speed));
                }
            }
        }
        
        // PlotModel for OxyPlot
        private PlotModel _plotModel;
        private float _targetMoveAbsoluteValue;
        private float _targetMoveRelativeValue;
        private ILoggerFactory _loggerFactory;

        public PlotModel PlotModel
        {
            get => _plotModel;
            private set
            {
                _plotModel = value;
                OnPropertyChanged(nameof(PlotModel));
            }
        }


        public float TargetMoveAbsoluteValue
        {
            get => _targetMoveAbsoluteValue;
            set
            {
                if (_targetMoveAbsoluteValue != value)
                {
                    _targetMoveAbsoluteValue = value;
                    OnPropertyChanged(nameof(TargetMoveAbsoluteValue));
                }
            }
        }
        public float TargetMoveRelativeValue
        {
            get => _targetMoveRelativeValue;
            set
            {
                if (_targetMoveRelativeValue != value)
                {
                    _targetMoveRelativeValue = value;
                    OnPropertyChanged(nameof(TargetMoveRelativeValue));
                }
            }
        }
        // Commands
        public ICommand StopCommand { get; }
        public ICommand HomeCommand { get; }
        public ICommand MoveCommand { get; }
        public ICommand ShiftCommand { get; }

        public PositionerDeviceViewModel(BaseDevice device, standa_controller_software.command_manager.CommandManager commandManager, ControllerManager controllerManager, ILoggerFactory loggerFactory) : base(device, commandManager, controllerManager)
        {
            _loggerFactory = loggerFactory;
            if (device is BasePositionerDevice positioner)
            {
                _positioner = positioner;
                _positioner.PositionChanged += OnPositionChanged;
                _positioner.ConnectionStateChanged += OnConnectionStateChanged;

                Position = _positioner.CurrentPosition;
                Speed = _positioner.CurrentSpeed;
                IsConnected = _positioner.IsConnected;
                TargetMoveAbsoluteValue = _positioner.CurrentPosition;
                TargetMoveRelativeValue = 0f;
                
                InitializePlotModel();

                // Initialize commands
                StopCommand = new RelayCommand(ExecuteStop);
                HomeCommand = new RelayCommand(() => Task.Run(async () => await ExecuteHome()));
                MoveCommand = new RelayCommand(() => Task.Run(async() => await ExecuteMove() ) );
                ShiftCommand = new RelayCommand(() => Task.Run(async () => await ExecuteShift()));

            }
            else
            {
                throw new ArgumentException("Device must be a BasePositionerDevice", nameof(device));
            }
        }


        // Methods

        private void InitializePlotModel()
        {
            PlotModel = new PlotModel { Title = $"{Name} Position and Speed" };

            _positionSeries = new LineSeries
            {
                Title = "Position",
                Color = OxyColors.Blue,
                MarkerType = MarkerType.Circle,
                MarkerSize = 2
            };
            
            _speedSeries = new LineSeries
            {
                Title = "Speed",
                Color = OxyColors.Red,
                MarkerType = MarkerType.Circle,
                MarkerSize = 2
            };

            PlotModel.Series.Add(_positionSeries);
            PlotModel.Series.Add(_speedSeries);
        }

        public override void StartAcquisition()
        {
            _isAcquiring = true;
            _acquisitionStartTime = DateTime.Now;
            _timeElapsed = 0;
            _positionSeries.Points.Clear();
            _speedSeries.Points.Clear();

            if (_plotUpdateTimer == null)
            {
                _plotUpdateTimer = new System.Timers.Timer(1000); // Update every 1 second
                _plotUpdateTimer.Elapsed += OnPlotUpdateTimerElapsed;
            }
            _plotUpdateTimer.Start();
        }

        public override void StopAcquisition()
        {
            _isAcquiring = false;
            _plotUpdateTimer?.Stop();
        }

        private void OnPlotUpdateTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Refresh the plot on the UI thread
            App.Current.Dispatcher.Invoke(() =>
            {
                PlotModel.InvalidatePlot(true);
            });
        }

        private void OnPositionChanged(object sender, EventArgs e)
        {
            Position = _positioner.CurrentPosition;
            Speed = _positioner.CurrentSpeed;
            
            if (_isAcquiring)
            {
                _timeElapsed = (DateTime.Now - _acquisitionStartTime).TotalSeconds;

                _positionSeries.Points.Add(new DataPoint(_timeElapsed, Position));
                _speedSeries.Points.Add(new DataPoint(_timeElapsed, Speed));
            }
        }

        private void OnConnectionStateChanged(object sender, EventArgs e)
        {
            IsConnected = _positioner.IsConnected;
        }

        public override void UpdateFromDevice(BaseDevice device)
        {
            if (device is BasePositionerDevice positionerDevice)
            {
                Position = positionerDevice.CurrentPosition;
                Speed = positionerDevice.CurrentSpeed;
            }
        }

        // Command execution methods
        private void ExecuteStop()
        {
            //_positioner.Stop();
        }
        private async Task ExecuteHome()
        {
            if (_positioner.IsConnected)
            {
                var controller = _controllerManager.GetDeviceController<BasePositionerController>(_positioner.Name);
                var command = new Command
                {
                    TargetController = controller.Name,
                    TargetDevices = [_positioner.Name],
                    Action = CommandDefinitions.Home,
                };

                await _commandManager.TryExecuteCommand(command);
            }
        }
        private async Task ExecuteMove()
        {
            if (_positioner.IsConnected)
            {
                var functionDefinitionLibrary = new FunctionManager(_controllerManager, _commandManager, _loggerFactory);
                functionDefinitionLibrary.ClearCommandQueue();
                functionDefinitionLibrary.InitializeDefinitions();

                functionDefinitionLibrary.Definitions.ExecuteFunction("jumpA", [_positioner.Name.ToString(), TargetMoveAbsoluteValue]);

                _commandManager.ClearQueue();
                foreach (var commandLine in functionDefinitionLibrary.ExtractCommands())
                {
                    await _commandManager.TryExecuteCommandLine(commandLine);
                }

            }
        }

        private async Task ExecuteShift()
        {
            if (_positioner.IsConnected)
            {
                var functionDefinitionLibrary = new FunctionManager(_controllerManager, _commandManager, _loggerFactory);
                functionDefinitionLibrary.ClearCommandQueue();
                functionDefinitionLibrary.InitializeDefinitions();

                var targetPositionAbsolute = _positioner.CurrentPosition + TargetMoveRelativeValue;
                functionDefinitionLibrary.Definitions.ExecuteFunction("jumpA", [_positioner.Name.ToString(), targetPositionAbsolute]);

                _commandManager.ClearQueue();
                foreach (var commandLine in functionDefinitionLibrary.ExtractCommands())
                {
                    await _commandManager.TryExecuteCommandLine(commandLine);
                }

            }
        }

        // IDisposable implementation
        public void Dispose()
        {
            if (_plotUpdateTimer != null)
            {
                _plotUpdateTimer.Stop();
                _plotUpdateTimer.Elapsed -= OnPlotUpdateTimerElapsed;
                _plotUpdateTimer.Dispose();
                _plotUpdateTimer = null;
            }

            // Unsubscribe from events
            if (_positioner != null)
            {
                _positioner.PositionChanged -= OnPositionChanged;
                _positioner.ConnectionStateChanged -= OnConnectionStateChanged;
            }
        }
    }
}
