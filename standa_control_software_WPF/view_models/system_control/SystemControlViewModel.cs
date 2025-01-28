using Microsoft.Extensions.Logging;
using standa_control_software_WPF.view_models.commands;
using standa_control_software_WPF.view_models.system_control.control;
using standa_controller_software.command_manager;
using standa_controller_software.custom_functions;
using standa_controller_software.device_manager;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using text_parser_library;

namespace standa_control_software_WPF.view_models.system_control
{
    public class SystemControlViewModel : ViewModelBase
    {
        /// <summary>
        /// Represents the main view model for system control in the Standa Control Software WPF application.
        /// Manages documents, command queues, parsing operations, and command queue rendering container.
        /// </summary>

        private readonly ILogger<SystemControlViewModel> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly standa_controller_software.command_manager.CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;
        private readonly FunctionManager _functionDefinitionLibrary;
        private readonly TextInterpreterWrapper _textInterpreter;

        private DocumentViewModel? _selectedDocument;
        private CancellationTokenSource? _parsingCts;
        private Stopwatch? _executionStopwatch;
        private TimeSpan _allocatedTime = new();
        private string _outputMessage;
        private bool _updateLoopRunning;
        private bool _isParsing;
        private string _parsingStatusMessage = string.Empty;
        private bool _isOutOfBounds;
        private string _currentStateMessage = "Idle";
        private bool _isExecingCommandQueue;
        private bool _isAllowedOutOfBounds;

        public PainterManagerViewModel PainterManager { get; private set; }
        public ObservableCollection<DocumentViewModel> Documents { get; } = [];
        public DocumentViewModel? SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                _selectedDocument = value;
                OnPropertyChanged(nameof(SelectedDocument));
            }
        }
       
        public string OutputMessage
        {
            get
            {
                return _outputMessage;
            }
            set
            {
                _outputMessage = value;
                OnPropertyChanged(nameof(OutputMessage));
            }
        }
        public bool IsParsing
        {
            get => _isParsing;
            set
            {
                _isParsing = value;
                OnPropertyChanged(nameof(IsParsing));
            }
        }
        public bool IsParsingStatusMessageNotEmty => ParsingStatusMessage != "";
        public string ParsingStatusMessage
        {
            get => _parsingStatusMessage;
            set
            {
                _parsingStatusMessage = value;
                OnPropertyChanged(nameof(ParsingStatusMessage));
                OnPropertyChanged(nameof(IsParsingStatusMessageNotEmty));
            }
        }
        public bool IsOutOfBounds
        {
            get => _isOutOfBounds;
            set
            {
                _isOutOfBounds = value;
                OnPropertyChanged(nameof(IsOutOfBounds));
            }
        }
        public string CurrentStateMessage
        {
            get => _currentStateMessage;
            set
            {
                if (value != _currentStateMessage)
                {
                    _currentStateMessage = value;
                    OnPropertyChanged(nameof(CurrentStateMessage));
                }
            }
        }
        public bool IsAllowedOutOfBounds
        {
            get => _isAllowedOutOfBounds;
            set
            {
                _isAllowedOutOfBounds = value;
                OnPropertyChanged(nameof(IsAllowedOutOfBounds));
            }
        }

        public ICommand AddNewDocumentCommand { get; set; }
        public ICommand OpenDocumentCommand { get; set; }
        public ICommand CreateCommandQueueFromInput { get; set; }
        public ICommand CancelCommandQueueParsing { get; set; }
        public ICommand ExecuteCommandQueueCommand { get; set; }
        public ICommand ForceStopCommand { get; set; }
        public ICommand ClearOutputMessageCommand { get; set; }

        public SystemControlViewModel(ControllerManager controllerManager, standa_controller_software.command_manager.CommandManager commandManager, ILogger<SystemControlViewModel> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _commandManager = commandManager;
            _controllerManager = controllerManager;
            _outputMessage = "";


            _functionDefinitionLibrary = new FunctionManager(_controllerManager, _loggerFactory);
            _textInterpreter = new TextInterpreterWrapper() { DefinitionLibrary = _functionDefinitionLibrary.Definitions };
            PainterManager = new PainterManagerViewModel(_controllerManager, _loggerFactory);

            AddNewDocumentCommand = new RelayCommand(() => AddNewDocument());
            OpenDocumentCommand = new RelayCommand(() => OpenDocument());

            CreateCommandQueueFromInput = new RelayCommand(
                async () => await CreateCommandQueueFromInputAsync(),
                CanParseText
            );
            CancelCommandQueueParsing = new RelayCommand(ExecuteCancelCommandParsing);

            ExecuteCommandQueueCommand = new RelayCommand(async () => await ExecuteCommandsQueueAsync(), CanExecuteCommandQueue);
            ForceStopCommand = new RelayCommand(async () => await ExecuteForceStopCommand());

            ClearOutputMessageCommand = new RelayCommand(() => OutputMessage = "");

            _commandManager.OnStateChanged += CommandManager_OnStateChanged;
            _controllerManager.ToolInformation!.OutOfBoundsChanged += async (value) => await Tool_OutOfBoundsChanged(value);

        }

        private async Task Tool_OutOfBoundsChanged(bool isOutOfBounds)
        {
            IsOutOfBounds = isOutOfBounds;

            if (isOutOfBounds)
            {

                if (!IsAllowedOutOfBounds)
                {
                    CurrentStateMessage = "Out of Bounds";

                    await ForceStop();
                    CurrentStateMessage = "Out of Bounds";

                }
            }
            else
            {
                CurrentStateMessage = "Idle";
                IsAllowedOutOfBounds = false;
            }


        }
        private void CommandManager_OnStateChanged(CommandManagerState newState)
        {
            // Used to track the duration of command queue execution.
            if (newState == CommandManagerState.Processing)
            {
                ParsingStatusMessage = $"Executing movement";

                // Start a new stopwatch
                _executionStopwatch = new Stopwatch();
                _executionStopwatch.Start();

                // Start the asynchronous UI update loop (if not already running)
                if (!_updateLoopRunning)
                {
                    _updateLoopRunning = true;
                    _ = UpdateExecutionTimerLoop();
                }
            }
            else if (newState == CommandManagerState.Waiting)
            {
                _executionStopwatch?.Stop();
                ParsingStatusMessage = "";

            }
        }
        private async Task UpdateExecutionTimerLoop()
        {
            try
            {
                while (_commandManager.CurrentState == CommandManagerState.Processing)
                {
                    if (_executionStopwatch != null)
                    {
                        if (_isExecingCommandQueue)
                        {
                            var currentTime = _executionStopwatch.Elapsed.ToString(@"hh\:mm\:ss");
                            var allocatedTime = _allocatedTime.ToString(@"hh\:mm\:ss");
                            ParsingStatusMessage = $"Estimated time: {allocatedTime} | Time elapsed: {currentTime}";
                        }
                    }
                    await Task.Delay(1000);
                }
            }
            finally
            {
                _updateLoopRunning = false;
            }
        }
        private void ExecuteCancelCommandParsing()
        {
            _parsingCts?.Cancel();
            _functionDefinitionLibrary.ClearCommandQueue();
        }
        private bool CanExecuteCommandQueue()
        {
            if (IsParsing)
                return false;

            if (_commandManager.CurrentState != CommandManagerState.Waiting)
                return false;

            if (!_functionDefinitionLibrary.ExtractCommands().Any())
                return false;

            return true;
        }
        private bool CanParseText()
        {
            if (SelectedDocument is null)
                return false;

            var input = SelectedDocument.InputText;

            if (input is null || input == string.Empty)
                return false;

            if (_commandManager.CurrentState != CommandManagerState.Waiting)
                return false;

            return true;
        }
        private async Task ForceStop()
        {
            await _commandManager.Stop();
        }
        private async Task ExecuteForceStopCommand()
        {
            CurrentStateMessage = "Stoppping";
            await ForceStop();
            CurrentStateMessage = "Idle";

        }
        private void SaveCommandLog()
        {
            var content = string.Join("\n", _commandManager.GetCommandQueueAsString());
            // The name of the file where the content will be saved
            string fileName = "command_log.txt";

            // Path to save the file in the same project directory
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

            File.WriteAllText(filePath, content);

        }
        private async Task ExecuteCommandsQueueAsync()
        {

            if (_commandManager.CurrentState != CommandManagerState.Processing)
            {
                _isExecingCommandQueue = true;
                CurrentStateMessage = "Executing Commands";
                _commandManager.ClearQueue();
                foreach (var commandLine in _functionDefinitionLibrary.ExtractCommands())
                {
                    _commandManager.EnqueueCommandLine(commandLine);
                }

                SaveCommandLog();

                await Task.Run(() => _commandManager.ProcessQueue());
                OutputMessage += $"\nExecution of commands finalized.";
                CurrentStateMessage = "Idle";
                _isExecingCommandQueue = false;
            }

        }
        private async Task CreateCommandQueueFromInputAsync()
        {
            try
            {
                if (SelectedDocument is null)
                    return;
                CurrentStateMessage = "Parsing Text";
                // Prepare for parse
                _parsingCts = new CancellationTokenSource();
                IsParsing = true;

                var inputText = SelectedDocument.InputText;
                var fileName = SelectedDocument.Name;

                // Further operation requires that the document would hava a filepath.
                if(SelectedDocument.FilePath is null)
                    SelectedDocument.SaveAsFileCommand.Execute(null);

                var filePatch = SelectedDocument.FilePath?? ""; 
                // Clear any prior state
                _functionDefinitionLibrary.ClearCommandQueue();
                _functionDefinitionLibrary.InitializeDefinitions();
                _textInterpreter.DefinitionLibrary = _functionDefinitionLibrary.Definitions;
                // Actually do the parse on background thread
                await Task.Run(() =>
                {
                    // Provide a callback for status updates
                    _textInterpreter.ReadInput(
                        inputText, fileName, filePatch,
                        _parsingCts.Token,
                        statusUpdate => { ParsingStatusMessage = statusUpdate; }
                    );
                });

                // If parse succeeds, do next steps (extract commands, etc.)
                var commandList = _functionDefinitionLibrary.ExtractCommands();
                var allocatedTime_s = commandList.Sum(cmdLine => cmdLine.Max(cmd => cmd.EstimatedTime));
                _allocatedTime = TimeSpan.FromSeconds(allocatedTime_s);

                OutputMessage += $"\nParsed successfully. Estimated time: {_allocatedTime.ToString("hh':'mm':'ss")}";

                CurrentStateMessage = "Rendering Commands";
                await PainterManager.PaintCommandQueue(commandList);

                ParsingStatusMessage = $"Estimated time: {_allocatedTime.ToString("hh':'mm':'ss")}";
            }
            catch (OperationCanceledException)
            {
                OutputMessage += "\nParsing was canceled.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                if (_textInterpreter.State.CurrentState == ParserState.States.Error)
                {
                    if(SelectedDocument is not null && _textInterpreter.State.LineNumber is int lineNumberValue)
                        SelectedDocument.HighlightedLineNumber = lineNumberValue;
                    ParsingStatusMessage = "Fault Encountered While Parsing.";
                    OutputMessage += $"\nFault Encountered While Parsing. Line: {_textInterpreter.State.LineNumber}. Message: {_textInterpreter.State.Message}.";

                }
            }
            finally
            {
                IsParsing = false;
                CurrentStateMessage = "Idle";
                _textInterpreter.State.ClearMessage();
                _textInterpreter.State.Reset();
                ((RelayCommand)ExecuteCommandQueueCommand).RaiseCanExecuteChanged();
            }
        }
        private void AddNewDocument(string content = "")
        {
            var newDoc = new DocumentViewModel($"Document {Documents.Count + 1}", content);
            
            Documents.Add(newDoc);
            newDoc.CloseDocumentRequested += RemoveDocument;
            SelectedDocument = newDoc;
        }
        private void RemoveDocument(DocumentViewModel document)
        {
            Documents.Remove(document);
        }
        private void OpenDocument()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text file (*.txt)|*.txt",
                DefaultExt = "*.txt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = openFileDialog.FileName;
                string documentName = Path.GetFileNameWithoutExtension(filePath);

                int count = Documents.Count(d => d.Name.StartsWith(documentName));
                if (count > 0)
                {
                    documentName += $" ({count})";
                }

                string content;
                using (StreamReader sr = new(filePath))
                {
                    content = sr.ReadToEnd();
                }

                DocumentViewModel newDocument = new(documentName, content)
                {
                    FilePath = filePath,
                };

                Documents.Add(newDocument);
                newDocument.CloseDocumentRequested += RemoveDocument;
                SelectedDocument = newDocument;
            }
        }
    }
}
