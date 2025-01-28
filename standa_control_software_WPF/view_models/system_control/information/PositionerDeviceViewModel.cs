using Microsoft.Extensions.Logging;
using OxyPlot;
using OxyPlot.Series;
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.command_manager;
using standa_controller_software.custom_functions;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces.positioning;
using standa_controller_software.device_manager.devices;
using System.Timers;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.system_control.information
{
    /// <summary>
    /// View model responsible for managing a positioner device, including data acquisition,
    /// plotting of position and speed, and executing device-specific commands.
    /// </summary>
    public class PositionerDeviceViewModel : DeviceViewModel, IDisposable
    {
        private readonly BasePositionerDevice _positioner;
        private readonly ILoggerFactory _loggerFactory;
        private readonly LineSeries _positionSeries;
        private readonly LineSeries _speedSeries;
        private float _position;
        private float _speed;
        private bool _isAcquiring;
        private DateTime _acquisitionStartTime;
        private double _timeElapsed;
        private System.Timers.Timer? _plotUpdateTimer;
        // PlotModel for OxyPlot
        private PlotModel _plotModel;
        private float _targetMoveAbsoluteValue;
        private float _targetMoveRelativeValue;

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

                _plotModel = new PlotModel { Title = $"{Name} Position and Speed" };

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

                // Initialize commands
                StopCommand = new RelayCommand(async() => await ExecuteStop());
                HomeCommand = new RelayCommand(() => Task.Run(async () => await ExecuteHome()));
                MoveCommand = new RelayCommand(() => Task.Run(async() => await ExecuteMove() ) );
                ShiftCommand = new RelayCommand(() => Task.Run(async () => await ExecuteShift()));

            }
            else
            {
                throw new ArgumentException("Device must be a BasePositionerDevice", nameof(device));
            }
        }
        /// <summary>
        /// Starts continuous data acquisition, enabling periodic plotting of position and speed.
        /// </summary>
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
        /// <summary>
        /// Stops continuous data acquisition and halts plotting.
        /// </summary>
        public override void StopAcquisition()
        {
            _isAcquiring = false;
            _plotUpdateTimer?.Stop();
        }
        /// <summary>
        /// Event handler for the plot update timer's elapsed event.
        /// Invalidates the plot to refresh the UI.
        /// </summary>
        /// <param name="sender">The timer that triggered the event.</param>
        /// <param name="e">Event data.</param>
        private void OnPlotUpdateTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            // Refresh the plot on the UI thread
            App.Current.Dispatcher.Invoke(() =>
            {
                PlotModel.InvalidatePlot(true);
            });
        }
        /// <summary>
        /// Event handler for changes in the device's position.
        /// Updates the position and speed properties and appends data points to the plot if acquisition is active.
        /// </summary>
        /// <param name="sender">The device that triggered the event.</param>
        /// <param name="e">Event data.</param>
        private void OnPositionChanged(object? sender, EventArgs e)
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
        /// <summary>
        /// Event handler for changes in the device's connection state.
        /// Updates the <see cref="IsConnected"/> property accordingly.
        /// </summary>
        /// <param name="sender">The device that triggered the event.</param>
        /// <param name="e">Event data.</param>
        private void OnConnectionStateChanged(object? sender, EventArgs e)
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
        /// <summary>
        /// Executes the stop command, forcefully stopping the device's current operation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ExecuteStop()
        {
            var controller = _controllerManager.GetDeviceController<BasePositionerController>(_positioner.Name);

            await controller.ForceStop();

        }
        /// <summary>
        /// Executes the home command, returning the device to its predefined home position.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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
                    Parameters = _positioner.Name
                };

                await _commandManager.TryExecuteCommand(command);
            }
        }
        /// <summary>
        /// Executes the move command, moving the device to the specified absolute position.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ExecuteMove()
        {
            if (_positioner.IsConnected)
            {
                var functionDefinitionLibrary = new FunctionManager(_controllerManager, _loggerFactory);
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
        /// <summary>
        /// Executes the shift command, shifting the device by the specified relative value.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ExecuteShift()
        {
            if (_positioner.IsConnected)
            {
                var functionDefinitionLibrary = new FunctionManager(_controllerManager, _loggerFactory);
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
        public override void Dispose()
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
            base.Dispose();
        }
    }
}
