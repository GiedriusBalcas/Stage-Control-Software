


namespace standa_control_software_WPF.view_models.system_control
{
    public class SystemControlViewModel : ViewModelBase
    {
        //private readonly CommandManager _commandManager;
        //private readonly Painter _painter;
        //private string _commandText="";
        //private string _outputMessage;
        //private TextParserViewModel _configParser;
        //private SystemConfig _virtualConfig;
        //private TextParserViewModel _virtualParser;
        //private SystemConfig _painterConfig;
        //private TextParserViewModel _painterParser;
        //private DebuggerViewModel _debugger;
        //private Vector4 _toolColor;
        //private Vector4 _lineColor;
        //private Vector4 _defaultToolColor = new Vector4(0.3411764705882353f, 0.4117647058823529f, 0.8352941176470589f, 0.8f);


        //private DocumentViewModel _selectedDocument;
        //private bool _isTrackingTool;
        //private bool _isOrthographicView;
        //private Vector4 _lineColorEngaged = new Vector4(1,0,0,0.5f);
        //private Vector4 _lineColorNotEngaged = new Vector4(0,1,0,0.05f);
        //private bool _IsRendering;

        //public ICommand CheckCommandTextCommand { get; set; }
        //public ICommand ClearOutputMessageCommand { get; set; }
        //public ICommand RenderCommand { get; set; }
        //public ICommand ExecuteCommandsCommand { get; set; }

        //public ICommand PauseDebuggingCommand { get; set; }
        //public ICommand ResumeDebuggingCommand { get; set; }
        //public ICommand StopDebuggingCommand { get; set; }

        //public ICommand CameraViewXYCommand { get; set; }
        //public ICommand CameraViewXZCommand { get; set; }
        //public ICommand CameraViewYZCommand { get; set; }
        //public ICommand CameraFitObjectCommand { get; set; }

        //public ICommand ForceStopCommand { get; set; }

        //public bool IsTrackingTool
        //{
        //    get => _isTrackingTool;
        //    set
        //    {
        //        _isTrackingTool = value;
        //        _painter.IsTrackingTool = value;
        //        OnPropertyChanged(nameof(IsTrackingTool));
        //    }
        //}

        //public bool IsOrthographicView
        //{
        //    get => _isOrthographicView;
        //    set
        //    {
        //        _isOrthographicView = value;
        //        _painter.IsOrthographic = value;
        //        OnPropertyChanged(nameof(IsTrackingTool));
        //    }
        //}

        //public string CommandText
        //{
        //    get { return _commandText; }
        //    set 
        //    { 
        //        _commandText = value;
        //        //OnPropertyChanged(nameof(CommandText));
        //    }
        //}

        //public string OutputMessage 
        //{
        //    get 
        //    {
        //        return _outputMessage;
        //    }
        //    set 
        //    {
        //        _outputMessage = value;
        //        OnPropertyChanged(nameof(OutputMessage));
        //    }
        //}

        //private int _highlightedLineNumber => _debugger.CurrentLine;
        //public int HighlightedLineNumber
        //{
        //    get { return _highlightedLineNumber; }
        //}


        //public ObservableCollection<DocumentViewModel> Documents { get; } = new ObservableCollection<DocumentViewModel>();

        //public DocumentViewModel SelectedDocument
        //{
        //    get => _selectedDocument;
        //    set 
        //    {
        //        CommandText = _selectedDocument?.CommandText;
        //        _selectedDocument = value;
        //        OnPropertyChanged(nameof(SelectedDocument));
        //    }
        //}

        //public ICommand AddNewDocumentCommand {  get; set; }
        //public ICommand OpenDocumentCommand {  get; set; }
        //public event Action OnExecutionStart;

        //public SystemControlViewModel(SystemConfig config)
        //{
            
        //    _commandManager = config;
        //    _toolColor =  _commandManager.GetTool().IsEngaged ? new Vector4(1, 0, 0, 1) : _defaultToolColor;
        //    _painter = new Painter()
        //    {
        //        LineColor = _commandManager.GetTool().IsEngaged ? _lineColorEngaged : _lineColorNotEngaged,
        //    };
        //    _debugger = new DebuggerViewModel();

        //    _debugger.OnCurrentLineChanged = () => 
        //    {
        //        if(SelectedDocument is not null)
        //            SelectedDocument.HighlightedLineNumber = _debugger.CurrentLine;
        //    };

        //    CheckCommandTextCommand = new RelayCommand(CheckCommandText);
        //    ClearOutputMessageCommand = new RelayCommand(() => OutputMessage = string.Empty);
        //    ExecuteCommandsCommand = new RelayCommand(ExecuteCommandTextAsync);

        //    ResumeDebuggingCommand = new RelayCommand( () => { _debugger.Resume(); });
        //    PauseDebuggingCommand = new RelayCommand(() => { _debugger.Pause(); });
        //    StopDebuggingCommand = new RelayCommand(ExecuteStopDebugingCommand);

        //    OpenDocumentCommand = new RelayCommand(ExecuteOpenDocumentCommand);
        //    AddNewDocumentCommand = new RelayCommand(() => AddNewDocument());

        //    CameraViewXYCommand = new RelayCommand(() => _painter.ExecuteCameraViewXY());
        //    CameraViewXZCommand = new RelayCommand(() => _painter.ExecuteCameraViewXZ());
        //    CameraViewYZCommand = new RelayCommand(() => _painter.ExecuteCameraViewYZ());
        //    CameraFitObjectCommand = new RelayCommand( () => _painter.SnapObjectToFit() );

        //    ForceStopCommand = new RelayCommand(ExecuteForceStopCommandAsync);

        //    OutputMessage = string.Empty;


        //    CreateParsers();

        //    _IsRendering = true;
        //}

        //private async void ExecuteStopDebugingCommand()
        //{
        //    _debugger.Stop();
        //    var tasks = new List<Task>();
        //    foreach (var positioner in _commandManager.GetDevicesByType<IPositioner>())
        //    {
        //        var controller = _commandManager.GetDeviceController<IPositionerController>(positioner);
        //        var task = controller.StopSoft(positioner.Name);
        //        tasks.Add(task);
        //    }

        //    await Task.WhenAll(tasks);
        //}

        //private async void ExecuteForceStopCommandAsync()
        //{
        //    _debugger.Stop();
        //    var tasks = new List<Task>();
        //    foreach (var positioner in _commandManager.GetDevicesByType<IPositioner>())
        //    {
        //        var controller = _commandManager.GetDeviceController<IPositionerController>(positioner);
        //        var task = controller.Stop(positioner.Name);
        //        tasks.Add(task);
        //    }

        //    await Task.WhenAll(tasks);
        //}

        //private void AddNewDocument(string content = "")
        //{
        //    var newDoc = new DocumentViewModel
        //    {
        //        Name = $"Document {Documents.Count + 1}",
        //        CommandText = content,
        //    };
        //    Documents.Add(newDoc);
        //    newDoc.CloseDocumentRequested += RemoveDocument;
        //    SelectedDocument = newDoc;
        //}

        //private void RemoveDocument(DocumentViewModel document)
        //{
        //    if (Documents.Contains(document))
        //    {
        //        Documents.Remove(document);
        //    }
        //}


        //private void ExecuteOpenDocumentCommand()
        //{
        //    var openFileDialog = new Microsoft.Win32.OpenFileDialog
        //    {
        //        Filter = "Text file (*.txt)|*.txt",
        //        DefaultExt = "*.txt"
        //    };

        //    if (openFileDialog.ShowDialog() == true)
        //    {
        //        var filePath = openFileDialog.FileName;
        //        // Extract the file name without extension as the document name
        //        string documentName = Path.GetFileNameWithoutExtension(filePath);

        //        // Check if a document with the same name is already open
        //        int count = Documents.Count(d => d.Name.StartsWith(documentName));

        //        // If a document with the same name exists, append a number to the new document's name
        //        if (count > 0)
        //        {
        //            documentName += $" ({count})";
        //        }

        //        // Load the file content
        //        string content;
        //        using (StreamReader sr = new StreamReader(filePath))
        //        {
        //            content = sr.ReadToEnd();
        //        }

        //        // Create a new DocumentViewModel instance with the loaded content and adjusted name
        //        DocumentViewModel newDocument = new DocumentViewModel
        //        {
        //            Name = documentName,
        //            CommandText = content, // Assuming CommandText is where you store the document's content
        //            FilePath = filePath,
        //        };

        //        // Add the new document to the Documents collection
        //        Documents.Add(newDocument);
        //        newDocument.CloseDocumentRequested += RemoveDocument;
        //        // Optionally, set the new document as the currently selected document
        //        SelectedDocument = newDocument;
        //        // If you're managing multiple documents, instead of setting CommandText,
        //        // you might want to create a new DocumentViewModel with the loaded text and add it to your documents collection.
        //    }
        //}

        //private void CreateParsers()
        //{
        //    _commandManager.GetTool().EngagedStateChanged += () =>
        //    {
        //        if (_commandManager.GetTool().IsEngaged)
        //            _toolColor = new Vector4(1, 0, 0, 1);
        //        else
        //            _toolColor = _defaultToolColor;
        //    };
        //    _configParser = new TextParserViewModel(_commandManager, _debugger);

        //    //  Virtual Controller System Configuration.

        //    _virtualConfig = new SystemConfig("Virtual")
        //    {
        //        IsVirtual = true
        //    };

        //    foreach (var controller in _commandManager.GetAllControllers())
        //    {
        //        Type? virtualControllerType = DeviceDefinitions.AvailableControllers
        //            .FirstOrDefault(controllerInfo => controllerInfo.Type == controller.GetType())?
        //        .VirtualType;

        //        if(virtualControllerType != null) { 

        //            var controllerInstance = Activator.CreateInstance(virtualControllerType) as IVirtualController;
        //            controllerInstance.Name = controller.Name;

        //            _virtualConfig.AddController(controllerInstance);
                
        //            foreach (var device in controller.GetDevices())
        //            {
        //                Type? deviceType = device.GetType();
        //                var deviceInstance = device.ShallowCopy();
        //                controllerInstance.Name = controller.Name;

        //                controllerInstance.RegisterDevice(deviceInstance);
        //                controllerInstance.ConnectDevice(deviceInstance.Name);
        //                _virtualConfig.AddDevice(deviceInstance);
        //            }
        //            controllerInstance.CopyState(controller);
        //        }
        //    }
        //    var positioners = _virtualConfig.GetDevicesByType<IPositioner>();

        //    var shutterDeviceVirtual = _virtualConfig.GetDevicesByType<IShutter>().First();


        //    var tool = new ToolDevice(positioners, _commandManager.GetTool().GetDependencyStrings(), shutterDeviceVirtual);
        //    _virtualConfig.AddTool(tool);
        //    tool.EngagedStateChanged += () => 
        //    {
        //        if (tool.IsEngaged) 
        //        { 
        //            _toolColor = new Vector4(1,0,0,1);
        //            _lineColor = new Vector4(1, 0, 0, 1);
        //        }
        //        else 
        //        { 
        //            _toolColor = _defaultToolColor;
        //            _lineColor = new Vector4(0, 1, 0, 1);
        //        }
        //    };


        //    _virtualParser = new TextParserViewModel(_virtualConfig, _debugger);


        //    //  Painter Controller System Configuration.


        //    _painterConfig = new SystemConfig("Painter")
        //    {
        //        IsVirtual = true
        //    };

        //    var firstPositionerController = _commandManager.GetAllControllers()
        //                                    .OfType<IPositionerController>()
        //                                    .FirstOrDefault();

        //    // If there's at least one IPositionerController, create a single OpenTKPainter instance
        //    OpenTKPainter? positionerControllerInstance = null;
        //    if (firstPositionerController != null)
        //    {
                
        //        foreach (var shutterController in _commandManager.GetAllControllers().OfType<IShutterController>().ToList())
        //        {
        //            var controllerInstance = new PainterShutterController();
        //            controllerInstance.Name = shutterController.Name;

        //            _painterConfig.AddController(controllerInstance);

        //            foreach (var device in shutterController.GetDevices())
        //            {
        //                Type? deviceType = device.GetType();
        //                var deviceInstance = device.ShallowCopy();
        //                controllerInstance.Name = shutterController.Name;

        //                controllerInstance.RegisterDevice(deviceInstance);
        //                controllerInstance?.ConnectDevice(deviceInstance.Name);
        //                _painterConfig.AddDevice(deviceInstance);
        //            }
        //            controllerInstance.CopyState(shutterController);
                    
        //        }

        //        // In the future, hold the Engaged Color Scheme inside of th shutter Devices.
        //        // For now let's just take the first shutter device controller and insert the default engaged/disengage color scheme.

                
        //        positionerControllerInstance = new OpenTKPainter(firstPositionerController.Name,
        //            (Vector3 start, Vector3 end) =>
        //            {
        //                var lineColor = new Vector4(0,0,1,1);
        //                var firstShutterDevice = _painterConfig.GetAllDevices()
        //                    .OfType<IShutter>()
        //                    .ToList()
        //                    .First();
        //                if(firstShutterDevice != null)
        //                {
        //                    bool currentShutterState = firstShutterDevice.State;
        //                    if (currentShutterState is bool state)
        //                    {
        //                        lineColor = state ? _lineColorEngaged : _lineColorNotEngaged;
        //                    }
        //                }

        //                _painter.LineColor = lineColor;
        //                _painter.AddLine(start, end);
        //            });
        //        _painterConfig.AddController(positionerControllerInstance);


        //        foreach (var positionerController in _commandManager.GetAllControllers().OfType<IPositionerController>().ToList())
        //        {
        //            foreach (var device in positionerController.GetDevices())
        //            {
        //                var deviceInstance = device.ShallowCopy();
        //                positionerControllerInstance.RegisterDevice(deviceInstance);
        //                positionerControllerInstance?.ConnectDevice(deviceInstance.Name);
        //                _painterConfig.AddDevice(deviceInstance);
        //            }
        //            positionerControllerInstance.CopyState(positionerController);
        //        }
        //    }

            

        //    positioners = _painterConfig.GetDevicesByType<IPositioner>();
        //    var shutterDevicePainter = _painterConfig.GetDevicesByType<IShutter>().First();

        //    tool = new ToolDevice(positioners,_commandManager.GetTool().GetDependencyStrings(), shutterDevicePainter);
        //    _painterConfig.AddTool(tool);

        //    foreach(var controller in _painterConfig.GetAllControllersByType<OpenTKPainter>())
        //    {
        //        controller.AttachToolPositionCall(_painterConfig.GetTool().CalculateToolPositionUpdate);
        //    }

        //    _painterParser = new TextParserViewModel(_painterConfig, _debugger);

        //    // TODO: I need one for tracker?

        //}

        //private void updateConfigState(SystemConfig configToPaste, SystemConfig configCopy)
        //{
        //    foreach (var controller in configToPaste.GetAllControllers())
        //    {
        //        if (controller is IVirtualController virtualController)
        //        {
        //            var controllerToCopy = configCopy.GetAllControllers().First(controllerCopy => controllerCopy.Name == virtualController.Name);

        //            virtualController.CopyState(controllerToCopy);
        //        }
        //    }
        //}

        //public async void CheckCommandText()
        //{
        //    if(SelectedDocument != null) 
        //    { 
        //        try
        //        {
        //            _IsRendering = false;
        //            _debugger.Start();
        //            _debugger.Document = SelectedDocument;
        //            updateConfigState(_painterConfig, _commandManager);
        //            _painterParser = new TextParserViewModel(_painterConfig, _debugger);

        //            _painter.ClearCollections();
        //            await _painterParser.ParseString(_debugger.Document.CommandText);

        //            _debugger.Stop();
        //            _debugger.UpdateCurrentLine(-1);
        //            OutputMessage += "\n"+_painterParser.Message;
        //        }
        //        catch (Exception ex)
        //        {
        //            OutputMessage += "\n" + ex.Message.ToString();
        //        }
        //        _IsRendering = true;
        //    }
        //}

        //public async void ExecuteCommandTextAsync()
        //{

        //    //
        //    // Create defined function list, using system config
        //    // Visit the tree. This executes the comands one by one.


        //    //if (SelectedDocument != null)
        //    //{
        //    //    try
        //    //    {
        //    //        _debugger.Start();
        //    //        _debugger.Document = SelectedDocument;
        //    //        updateConfigState(_painterConfig, _config);
        //    //        _painterParser = new TextParserViewModel(_painterConfig, _debugger);

        //    //        _painter.ClearCollections();
        //    //        await _painterParser.ParseString(_debugger.Document.CommandText);

        //    //        _debugger.Stop();
        //    //        _debugger.UpdateCurrentLine(-1);
        //    //        OutputMessage += "\n" + _painterParser.Message;
        //    //    }
        //    //    catch (Exception ex)
        //    //    {
        //    //        OutputMessage += "\n" + ex.Message.ToString();
        //    //    }
        //    //}

        //    OnExecutionStart.Invoke();


        //    if (SelectedDocument != null)
        //    {
        //        try
        //        {
        //            _debugger.Start();
        //            await _configParser.ParseString(SelectedDocument.CommandText);
        //            OutputMessage += "\n" + _configParser.Message;
        //            _debugger.Stop();
        //        }
        //        catch (Exception ex)
        //        {
        //            OutputMessage += "\n" + ex.Message.ToString();
        //        }
        //    }
        //    //
        //}

        //public void UpdateRenderer()
        //{
        //    _IsRendering = true;
        //    if (_IsRendering) {
        //        //_toolColor = _defaultToolColor;
        //        // Testing

        //        var positioners = _commandManager.GetDevicesByType<IPositioner>();
        //        foreach(var positioner in positioners)
        //        {
        //            var controller = _commandManager.GetDeviceController<IPositionerController>(positioner);
        //            controller.GetPosition(positioner.Name);
        //        }
        //        //Testing

        //        _commandManager.GetTool().RecalculateToolPosition();
        //        _painter.UpdateToolPoint(_commandManager.GetTool().Position, 20f, _toolColor);
        //        _painter.UpdateCommandLayerCollection();
        //        _painter.RenderCommandLayer();

        //        _painter.UpdateRefLayerCollection();
        //        _painter.RenderRefLayer();
        //    }
        //    //}
        //}


        //public void UpdateCameraSetting(float aspectRatio, float fovy)
        //{
        //    _painter.UpdateCameraSettings(aspectRatio, fovy);
        //}


        //internal void UpdateCameraOrbitAngles(float dx, float dy)
        //{
        //    //CommandPainter.UpdateCameraOrbitAngles(dx, dy);
        //    _painter.UpdateCameraOrbitAngle(dx, dy);
        //}

        //internal void UpdateCameraReference(float dx, float dy)
        //{
        //    //CommandPainter.UpdateCameraRefence(dx, dy);
        //    _painter.UpdateCameraRefence(dx, dy);
        //}

        //internal void UpdateCameraDistance(float dr)
        //{
        //    //CommandPainter.UpdateCameraDistance(dr);
        //    _painter.UpdateCameraDistance(dr);
        //}





    }
}
