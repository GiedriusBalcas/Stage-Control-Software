using OxyPlot;
using OxyPlot.Series;
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces.shutter;
using standa_controller_software.device_manager.devices;
using System.Timers;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.system_control.information
{
    /// <summary>
    /// ViewModel for managing and displaying the state and data of a shutter device.
    /// Handles state changes, connection status, and data acquisition for plotting.
    /// </summary>
    public class ShutterDeviceViewModel : DeviceViewModel
    {
        private readonly LineSeries _stateSeries;
        private readonly BaseShutterDevice _shutter;
        private bool _state;
        // PlotModel for OxyPlot
        private PlotModel _plotModel;
        private bool _isAcquiring;
        private DateTime _acquisitionStartTime;
        private double _timeElapsed;
        private System.Timers.Timer? _plotUpdateTimer;
        
        public bool State
        {
            get => _state;
            set
            {
                _state = value;
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(CurrentState));

            }
        }
        public string CurrentState { get => State ? "open" : "closed"; } 
        public PlotModel PlotModel
        {
            get => _plotModel;
            private set
            {
                _plotModel = value;
                OnPropertyChanged(nameof(PlotModel));
            }
        }

        public ICommand ToggleStateCommand { get; set; }

        public ShutterDeviceViewModel(BaseDevice device, standa_controller_software.command_manager.CommandManager commandManager, ControllerManager controllerManager) : base(device, commandManager, controllerManager)
        {
            if(device is BaseShutterDevice shutter)
            {
                _shutter = shutter;
                State = shutter.IsOn;
                shutter.StateChanged += OnStateChanged; ;
                IsConnected = shutter.IsConnected;
                shutter.ConnectionStateChanged += OnConnectionStateChanged;
                ToggleStateCommand = new RelayCommand(() => Task.Run(async () => await ExecuteToggleShutterState()));

                _plotModel = new PlotModel { Title = $"{Name} Position and Speed" };
                _stateSeries = new LineSeries
                {
                    Title = "State",
                    Color = OxyColors.Blue,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 2
                };
                PlotModel.Series.Add(_stateSeries);
            }
            else
            {
                throw new ArgumentException("Device must be a BaseShutterDevice", nameof(device));
            }
        }

        /// <summary>
        /// Asynchronously toggles the state of the shutter device.
        /// Sends a command to change the shutter state to the opposite of its current state.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ExecuteToggleShutterState()
        {
            if (_shutter.IsConnected)
            {
                var controller = _controllerManager.GetDeviceController<BaseShutterController>(_shutter.Name);
                var command = new Command
                {
                    TargetController = controller.Name,
                    TargetDevices = [_shutter.Name],
                    Action = CommandDefinitions.ChangeShutterState,
                    Parameters = new ChangeShutterStateParameters
                    {
                        State = !_shutter.IsOn
                    }
                };

                await _commandManager.TryExecuteCommand(command);
            }
        }
        /// <summary>
        /// Handles changes in the connection state of the shutter device.
        /// Updates the <see cref="IsConnected"/> property accordingly.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void OnConnectionStateChanged(object? sender, EventArgs e)
        {
            IsConnected = _shutter.IsConnected;
        }
        /// <summary>
        /// Handles changes in the state of the shutter device.
        /// Updates the state properties and logs the state change if data acquisition is active.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void OnStateChanged(object? sender, EventArgs e)
        {
            var prevState = State;
            State = _shutter.IsOn;

            if (_isAcquiring)
            {
                _timeElapsed = (DateTime.Now - _acquisitionStartTime).TotalSeconds;
                _stateSeries.Points.Add(new DataPoint(_timeElapsed - 0.01, prevState? 1 : 0));
                _stateSeries.Points.Add(new DataPoint(_timeElapsed, State? 1 : 0));
            }
        }
        /// <summary>
        /// Updates the ViewModel properties based on the latest data from the device.
        /// </summary>
        /// <param name="device">The device containing updated information.</param>
        public override void UpdateFromDevice(BaseDevice device)
        {
            if (device is BaseShutterDevice shutterDevice)
            {
                State = shutterDevice.IsOn;
            }
        }
        /// <summary>
        /// Starts the data acquisition process, initializing timing and plot data.
        /// Sets up a timer to periodically refresh the plot.
        /// </summary>
        public override void StartAcquisition()
        {
            _isAcquiring = true;
            _acquisitionStartTime = DateTime.Now;
            _timeElapsed = 0;
            _stateSeries.Points.Clear();
            
            if (_plotUpdateTimer == null)
            {
                _plotUpdateTimer = new System.Timers.Timer(1000); // Update every 1 second
                _plotUpdateTimer.Elapsed += OnPlotUpdateTimerElapsed;
            }
            _plotUpdateTimer.Start();
        }
        /// <summary>
        /// Event handler for the plot update timer.
        /// Refreshes the plot on the UI thread to reflect the latest data.
        /// </summary>
        /// <param name="sender">The source of the timer event.</param>
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
        /// Stops the data acquisition process and halts plot updates.
        /// </summary>
        public override void StopAcquisition()
        {
            _isAcquiring = false;
            _plotUpdateTimer?.Stop();
        }
    }

}
