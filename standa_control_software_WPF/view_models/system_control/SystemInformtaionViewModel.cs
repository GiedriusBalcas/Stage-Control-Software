

namespace standa_control_software_WPF.view_models.system_control
{
    public class SystemInformtaionViewModel : ViewModelBase
    {

        //Should Hold all of the Devices. Display what's needed here.
        //Lets stick to positioners for now.
        //Extra 2D + 1D grid layour for displaying Tool's position.

        //private readonly SystemConfig _config;
        //private readonly System.Timers.Timer _updateTimer;
        //private GetPosition _getPosObj;
        //private MoveAbsolute _moveAbsObj;
        //private PositionerViewModel _selectedPositioner;
        //private ObservableCollection<PositionTrackerViewModel> _positionTrackers;


        //public List<PositionerViewModel> Positioners { get; set; }
        //private float _toolPosX => _config.GetTool().Position.X;
        //private float _toolPosY => _config.GetTool().Position.Y;
        //private float _toolPosZ => _config.GetTool().Position.Z;

        //public float ToolPosX
        //{
        //    get { return _toolPosX; }
        //}
        //public float ToolPosY
        //{
        //    get { return _toolPosY; }
        //}

        //public float ToolPosZ
        //{
        //    get { return _toolPosZ; }
        //}

        //public ObservableCollection<PositionTrackerViewModel> PositionTrackers
        //{
        //    get
        //    {
        //        return _positionTrackers;
        //    }
        //    set
        //    {
        //        _positionTrackers = value;
        //        OnPropertyChanged(nameof(PositionTrackers));
        //    }
        //}

        //public PositionerViewModel SelectedPositioner
        //{
        //    get => _selectedPositioner;
        //    set
        //    {
        //        if (_selectedPositioner != value)
        //        {
        //            _selectedPositioner = value;
        //            OnPropertyChanged(nameof(SelectedPositioner));
        //        }
        //    }
        //}


        //private int _numberOfDataPoints;
        //private float _duration;
        //public int NumberOfDataPoints
        //{
        //    get => _numberOfDataPoints;
        //    set
        //    {
        //        _numberOfDataPoints = value;
        //        OnPropertyChanged(nameof(NumberOfDataPoints));
        //    }
        //}

        //public float Duration
        //{
        //    get => _duration;
        //    set
        //    {
        //        _duration = value;
        //        OnPropertyChanged(nameof(Duration));
        //    }
        //}

        //public ICommand AddNewTracker { get; set; }
        //public ICommand StartCollection { get; set; }
        //public bool IsStartOnCommandExecution { get; set; }

        //public SystemInformtaionViewModel(SystemConfig config, SystemControlViewModel systemCompilerViewModel)
        //{
        //    _config = config;
        //    //_getPosObj = _config;
        //    _moveAbsObj = new MoveAbsolute(_config);

        //    Positioners = new List<PositionerViewModel>();
        //    GeneratePositioner();


        //    _updateTimer = new System.Timers.Timer(100); // Update every 1 second, adjust as needed
        //    _updateTimer.Elapsed += UpdateValues;
        //    _updateTimer.AutoReset = true;
        //    _updateTimer.Enabled = true;

        //    PositionTrackers = new ObservableCollection<PositionTrackerViewModel>();

        //    AddNewTracker = new RelayCommand(ExecuteAddNewTracker, CanExecuteAddNewTracker);
        //    StartCollection = new RelayCommand(ExecuteStartCollectiom, CanExecuteStartCollection);

        //    systemCompilerViewModel.OnExecutionStart += () =>
        //    {
        //        if (CanExecuteStartCollection() && IsStartOnCommandExecution)
        //            ExecuteStartCollectiom();
        //    };

        //    _config.GetTool().EngagedStateChanged += () =>
        //    {
        //        //if (tool.IsEngaged)
        //        //{
        //        //    _toolColor = new Vector4(1, 0, 0, 1);
        //        //    _lineColor = _lineColorEngaged;
        //        //    _painter.LineColor = _lineColor;
        //        //}
        //        //else
        //        //{
        //        //    _toolColor = _defaultToolColor;
        //        //    _lineColor = _lineColorNotEngaged;
        //        //    _painter.LineColor = _lineColor;
        //        //}
        //    };
        //}

        //private async void ExecuteStartCollectiom()
        //{
        //    var tasks = new List<Task>();
        //    foreach (var tracker in PositionTrackers)
        //    {
        //        var task = tracker.Collectpoints(NumberOfDataPoints, Duration);
        //        tasks.Add(task);
        //    }
        //    await Task.WhenAll(tasks);
        //}

        //private bool CanExecuteStartCollection()
        //{
        //    if (_numberOfDataPoints > 0 && _duration > 0)
        //        return true;
        //    return false;
        //}

        //private bool CanExecuteAddNewTracker()
        //{
        //    if (SelectedPositioner is null)
        //        return false;
        //    return true;
        //}

        //private void ExecuteAddNewTracker()
        //{
        //    PositionTrackers.Add(new PositionTrackerViewModel(SelectedPositioner));
        //    OnPropertyChanged(nameof(PositionTrackers));
        //}

        //private void UpdateValues(object? sender, ElapsedEventArgs e)
        //{
        //    Positioners.ForEach(positioner => positioner.UpdatePosition());
        //    OnPropertyChanged(nameof(ToolPosX));
        //    OnPropertyChanged(nameof(ToolPosY));
        //    OnPropertyChanged(nameof(ToolPosZ));

        //    //PositionTrackerViewModel.UpdatePosition(ToolPosX, (float)_stopwatch.Elapsed.TotalSeconds);
        //}

        //private void GeneratePositioner()
        //{
        //    var positionerDevices = _config.GetDevicesByType<IPositioner>();


        //    foreach (var positioner in positionerDevices)
        //    {

        //        Action<float> moveAbsoluteFunction = (float position) =>
        //        {
        //            char[] deviceNames = new char[] { positioner.Name };
        //            float[] positions = new float[] { position };
        //            try
        //            {
        //                _ = _moveAbsObj.ExecutionCore(deviceNames, positions);
        //            }
        //            catch (Exception ex)
        //            {

        //            }
        //        };

        //        Func<float> getPositionFunction = () =>
        //        {
        //            char name = positioner.Name;
        //            var controller = _config.GetDeviceController<IPositionerController>(name);

        //            try
        //            {
        //                //float position = controller.GetPosition(name);
        //                float position = positioner.Position;
        //                return position;
        //            }
        //            catch (Exception ex)
        //            {
        //                //TODO Actually handle the error. Exception Service
        //                return 0;
        //            }

        //        };

        //        Func<float> getSpeedFunction = () =>
        //        {
        //            char name = positioner.Name;
        //            var controller = _config.GetDeviceController<IPositionerController>(name);
        //            try
        //            {
        //                //float speed = controller.GetSpeed(name);
        //                float speed = controller.GetSpeed(name);
        //                return speed;
        //            }
        //            catch (Exception ex)
        //            {
        //                //TODO Actually handle the error. Exception Service
        //                return 0;
        //            }

        //        };

        //        Positioners.Add(new PositionerViewModel(positioner, getPositionFunction, getSpeedFunction, moveAbsoluteFunction));
        //    }
        //}
    }


}
