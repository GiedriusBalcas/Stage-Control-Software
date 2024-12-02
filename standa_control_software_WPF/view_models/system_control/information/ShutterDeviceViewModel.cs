using OxyPlot.Series;
using OxyPlot;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static ICSharpCode.AvalonEdit.Editing.CaretWeakEventManager;
using System.Timers;
using System.Windows.Forms;
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.command_manager;
using standa_controller_software.command_manager.command_parameter_library;
using standa_controller_software.device_manager;
using standa_controller_software.device_manager.controller_interfaces.shutter;

namespace standa_control_software_WPF.view_models.system_control.information
{
    public class ShutterDeviceViewModel : DeviceViewModel
    {
        private bool _state;
        private readonly BaseShutterDevice _shutter;
        private bool _needsToBeTracked;

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

        // PlotModel for OxyPlot
        private LineSeries _stateSeries;
        private PlotModel _plotModel;
        private bool _isAcquiring;
        private DateTime _acquisitionStartTime;
        private double _timeElapsed;
        private System.Timers.Timer _plotUpdateTimer;

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
                InitializePlotModel();
            }
        }

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

        private void OnConnectionStateChanged(object? sender, EventArgs e)
        {
            IsConnected = _shutter.IsConnected;
        }

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

        public override void UpdateFromDevice(BaseDevice device)
        {
            if (device is BaseShutterDevice shutterDevice)
            {
                State = shutterDevice.IsOn;
            }
        }

        private void InitializePlotModel()
        {
            PlotModel = new PlotModel { Title = $"{Name} Position and Speed" };

            _stateSeries = new LineSeries
            {
                Title = "State",
                Color = OxyColors.Blue,
                MarkerType = MarkerType.Circle,
                MarkerSize = 2
            };

            PlotModel.Series.Add(_stateSeries);
        }
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
        private void OnPlotUpdateTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Refresh the plot on the UI thread
            App.Current.Dispatcher.Invoke(() =>
            {
                PlotModel.InvalidatePlot(true);
            });
        }
        public override void StopAcquisition()
        {
            _isAcquiring = false;
            _plotUpdateTimer?.Stop();
        }
    }

}
