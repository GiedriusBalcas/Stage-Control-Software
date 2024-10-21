using OxyPlot;
using OxyPlot.Series;
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.device_manager.devices;
using System;
using System.Timers;
using System.Windows.Input;

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

        public PositionerDeviceViewModel(BaseDevice device) : base(device)
        {
            if (device is BasePositionerDevice positioner)
            {
                _positioner = positioner;
                _positioner.PositionChanged += OnPositionChanged;
                _positioner.ConnectionStateChanged += OnConnectionStateChanged;

                Position = _positioner.CurrentPosition;
                Speed = _positioner.CurrentSpeed;
                IsConnected = _positioner.IsConnected;

                InitializePlotModel();

                // Initialize commands
                StopCommand = new RelayCommand(ExecuteStop);
                HomeCommand = new RelayCommand(ExecuteHome);
                MoveCommand = new RelayCommand(ExecuteMove);
                ShiftCommand = new RelayCommand(ExecuteShift);

                StartAcquisitionCommand = new RelayCommand(StartAcquisition);
                StopAcquisitionCommand = new RelayCommand(StopAcquisition);
            }
            else
            {
                throw new ArgumentException("Device must be a BasePositionerDevice", nameof(device));
            }
        }

        // Properties

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

        public bool NeedsToBeTracked
        {
            get => _needsToBeTracked;
            set
            {
                if (_needsToBeTracked != value)
                {
                    _needsToBeTracked = value;
                    OnPropertyChanged(nameof(NeedsToBeTracked));
                }
            }
        }

        // PlotModel for OxyPlot
        private PlotModel _plotModel;
        public PlotModel PlotModel
        {
            get => _plotModel;
            private set
            {
                _plotModel = value;
                OnPropertyChanged(nameof(PlotModel));
            }
        }

        // Commands
        public ICommand StopCommand { get; }
        public ICommand HomeCommand { get; }
        public ICommand MoveCommand { get; }
        public ICommand ShiftCommand { get; }

        public ICommand StartAcquisitionCommand { get; }
        public ICommand StopAcquisitionCommand { get; }

        // Methods

        private void InitializePlotModel()
        {
            PlotModel = new PlotModel { Title = $"{Name} Position and Speed" };

            _positionSeries = new LineSeries { Title = "Position", Color = OxyColors.Blue };
            _speedSeries = new LineSeries { Title = "Speed", Color = OxyColors.Red };

            PlotModel.Series.Add(_positionSeries);
            PlotModel.Series.Add(_speedSeries);
        }

        private void StartAcquisition()
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

        private void StopAcquisition()
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

        private void ExecuteHome()
        {
            //_positioner.Home();
        }

        private void ExecuteMove()
        {
            // Implement Move logic, possibly using an input value
        }

        private void ExecuteShift()
        {
            // Implement Shift logic, possibly using an input value
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
