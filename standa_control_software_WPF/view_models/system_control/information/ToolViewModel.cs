using OxyPlot.Series;
using OxyPlot;
using standa_control_software_WPF.view_models.commands;
using standa_controller_software.device_manager.devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using standa_controller_software.device_manager;
using System.Numerics;

namespace standa_control_software_WPF.view_models.system_control.information
{
    public class ToolViewModel : ViewModelBase, IDisposable
    {
        private readonly ToolInformation _tool;
        private Vector3 _position;
        private float _speed;
        private bool _needsToBeTracked;
        private bool _isAcquiring;
        private DateTime _acquisitionStartTime;
        private double _timeElapsed;
        private LineSeries _positionSeriesX;
        private LineSeries _positionSeriesY;
        private LineSeries _positionSeriesZ;
        private LineSeries _shutterSeries;
        private LineSeries _speedSeries;
        private System.Timers.Timer _plotUpdateTimer;


        // Properties
        private DateTime _timeOfPrevUpdate;
        private Vector3 PrevPosition;

        public Vector3 Position
        {
            get => _tool.Position;
        }

        public float PositionX => Position.X;
        public float PositionY => Position.Y;
        public float PositionZ => Position.Z;

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


        public ICommand StartAcquisitionCommand { get; }
        public ICommand StopAcquisitionCommand { get; }
        public ToolViewModel(ToolInformation toolInformation)
        {
            
                _tool = toolInformation;
                _tool.PositionChanged += _tool_PositionChanged; ;
                
                Speed = 0f;

                //InitializePlotModel();
                

                StartAcquisitionCommand = new RelayCommand(StartAcquisition);
                StopAcquisitionCommand = new RelayCommand(StopAcquisition);
            InitializePlotModel();


        }

        private void _tool_PositionChanged(Vector3 vector)
        {
            if(vector != PrevPosition)
            {
                var currentTime = DateTime.Now;
                OnPropertyChanged(nameof(Position));
                OnPropertyChanged(nameof(PositionX));
                OnPropertyChanged(nameof(PositionY));
                OnPropertyChanged(nameof(PositionZ));

                var timeIntervalSinceSpeedUpdate = currentTime.Subtract(_timeOfPrevUpdate).TotalMilliseconds;
                if(timeIntervalSinceSpeedUpdate > 100)
                {
                    Speed = (float)((Position - PrevPosition).Length() / (timeIntervalSinceSpeedUpdate / 1000));
                    PrevPosition = Position;
                    _timeOfPrevUpdate = currentTime;
                }


                if (_isAcquiring)
                {
                    _timeElapsed = (DateTime.Now - _acquisitionStartTime).TotalSeconds;

                    _positionSeriesX.Points.Add(new DataPoint(_timeElapsed, Position.X));
                    _positionSeriesY.Points.Add(new DataPoint(_timeElapsed, Position.Y));
                    _positionSeriesZ.Points.Add(new DataPoint(_timeElapsed, Position.Z));
                    _shutterSeries.Points.Add(new DataPoint(_timeElapsed, _tool.IsOn ? 100 : 0));
                    _speedSeries.Points.Add(new DataPoint(_timeElapsed, Speed));
                }
            }
        }


        // Methods

        private void InitializePlotModel()
        {
            PlotModel = new PlotModel { Title = $"Tools Position and Speed" };

            _positionSeriesX = new LineSeries { Title = "X", Color = OxyColors.Blue };
            _positionSeriesY = new LineSeries { Title = "Y", Color = OxyColors.Green };
            _positionSeriesZ = new LineSeries { Title = "Z", Color = OxyColors.RosyBrown };
            _shutterSeries = new LineSeries { Title = "Shutter", Color = OxyColors.Red };
            _speedSeries = new LineSeries { Title = "Speed", Color = OxyColors.LightGray };

            PlotModel.Series.Add(_positionSeriesX);
            PlotModel.Series.Add(_positionSeriesY);
            PlotModel.Series.Add(_positionSeriesZ);
            PlotModel.Series.Add(_shutterSeries);
            PlotModel.Series.Add(_speedSeries);
        }

        private void StartAcquisition()
        {
            _isAcquiring = true;
            _acquisitionStartTime = DateTime.Now;
            _timeElapsed = 0;
            _positionSeriesX.Points.Clear();
            _positionSeriesY.Points.Clear();
            _positionSeriesZ.Points.Clear();
            _shutterSeries.Points.Clear();
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
            if (_tool != null)
            {
                _tool.PositionChanged -= _tool_PositionChanged;
            }
        }
    }
}
