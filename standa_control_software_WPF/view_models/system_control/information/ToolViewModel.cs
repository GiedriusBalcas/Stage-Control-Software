using OxyPlot;
using OxyPlot.Series;
using standa_controller_software.device_manager;
using System.Numerics;
using System.Timers;

namespace standa_control_software_WPF.view_models.system_control.information
{
    /// <summary>
    /// ViewModel for managing and displaying information related to a configurations tool.
    /// Handles position tracking, speed calculation, and data acquisition for plotting.
    /// </summary>
    public class ToolViewModel : ViewModelBase, IDisposable
    {
        private readonly ToolInformation _tool;
        private float _speed;
        private bool _needsToBeTracked;
        private bool _isAcquiring;
        private DateTime _acquisitionStartTime;
        private double _timeElapsed;
        private readonly LineSeries _positionSeriesX;
        private readonly LineSeries _positionSeriesY;
        private readonly LineSeries _positionSeriesZ;
        private readonly LineSeries _shutterSeries;
        private readonly LineSeries _speedSeries;
        private System.Timers.Timer? _plotUpdateTimer;
        private DateTime _timeOfPrevUpdate;
        private PlotModel _plotModel;

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
        public PlotModel PlotModel
        {
            get => _plotModel;
            private set
            {
                _plotModel = value;
                OnPropertyChanged(nameof(PlotModel));
            }
        }

        public ToolViewModel(ToolInformation toolInformation)
        {
            _tool = toolInformation;
            _tool.PositionChanged += Tool_PositionChanged; ;

            Speed = 0f;

            _plotModel = new PlotModel { Title = $"Tools Position and Speed" };

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

        /// <summary>
        /// Handles the event when the tool's position changes.
        /// Updates position properties, calculates speed, and logs data if acquisition is active.
        /// </summary>
        /// <param name="vector">The new position vector of the tool.</param>
        private void Tool_PositionChanged(Vector3 vector)
        {
            if (vector != PrevPosition)
            {
                var currentTime = DateTime.Now;
                OnPropertyChanged(nameof(Position));
                OnPropertyChanged(nameof(PositionX));
                OnPropertyChanged(nameof(PositionY));
                OnPropertyChanged(nameof(PositionZ));

                var timeIntervalSinceSpeedUpdate = currentTime.Subtract(_timeOfPrevUpdate).TotalMilliseconds;
                if (timeIntervalSinceSpeedUpdate > 100)
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
        /// <summary>
        /// Starts the data acquisition process, initializing timing and plot data.
        /// Sets up a timer to periodically refresh the plot.
        /// </summary>
        public void StartAcquisition()
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
        /// <summary>
        /// Stops the data acquisition process and halts plot updates.
        /// </summary>
        public void StopAcquisition()
        {
            _isAcquiring = false;
            _plotUpdateTimer?.Stop();
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
        /// Releases all resources used by the <see cref="ToolViewModel"/>.
        /// Stops the plot update timer and unsubscribes from events.
        /// </summary>
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
            if (_tool != null)
            {
                _tool.PositionChanged -= Tool_PositionChanged;
            }
            base.Dispose();
        }
    }
}
