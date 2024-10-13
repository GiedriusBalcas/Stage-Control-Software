using standa_control_software_WPF.view_models.commands;
using standa_controller_software.command_manager;
using standa_controller_software.custom_functions;
using standa_controller_software.device_manager;
using standa_controller_software.painter;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Animation;
using text_parser_library;

namespace standa_control_software_WPF.view_models.system_control
{
    public class SystemControlViewModel : ViewModelBase
    {
        private readonly standa_controller_software.command_manager.CommandManager _commandManager;
        private readonly ControllerManager _controllerManager;
        private readonly FunctionManager _functionDefinitionLibrary;
        private standa_controller_software.command_manager.CommandManager _commandManager_virtual;
        private readonly TextInterpreterWrapper _textInterpreter;
        private readonly PainterManager _painterManager;
        private string _inputText = "";
        private string _outputMessage;


        private DocumentViewModel _selectedDocument;

        public string InputText
        {
            get { return _inputText; }
            set
            {
                _inputText = value;
                OnPropertyChanged(nameof(InputText));
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

        public ObservableCollection<DocumentViewModel> Documents { get; } = new ObservableCollection<DocumentViewModel>();

        public DocumentViewModel SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                InputText = _selectedDocument?.InputText;
                _selectedDocument = value;
                OnPropertyChanged(nameof(SelectedDocument));
            }
        }
        public ICommand AddNewDocumentCommand { get; set; }
        public ICommand OpenDocumentCommand { get; set; }
        public event Action OnExecutionStart;

        

        public ICommand CreateCommandQueueFromInputCommand { get; set; }
        public ICommand ExecuteCommandQueueCommand { get; set; }
        public ICommand ForceStopCommand { get; set; }
        public ICommand ClearOutputMessageCommand { get; set; }


        private int? _highlightedLineNumber;// _debugger.CurrentLine
        public int? HighlightedLineNumber
        {
            get { return _highlightedLineNumber; }
            private set { _highlightedLineNumber = value; }
        }


        public SystemControlViewModel(ControllerManager controllerManager, standa_controller_software.command_manager.CommandManager commandManager)
        {
            _commandManager = commandManager;
            _controllerManager = controllerManager;

            bool isProbing = true;
            UpdateDeviceStates(isProbing);

            _functionDefinitionLibrary = new FunctionManager(_controllerManager, _commandManager);
            _textInterpreter = new TextInterpreterWrapper() { DefinitionLibrary = _functionDefinitionLibrary.Definitions };
            _painterManager = new PainterManager(_commandManager, _controllerManager);

            AddNewDocumentCommand = new RelayCommand(() => AddNewDocument());
            OpenDocumentCommand = new RelayCommand(() => OpenDocument());

            CreateCommandQueueFromInputCommand = new RelayCommand(CreateCommandQueueFromInputAsync);
            ExecuteCommandQueueCommand = new RelayCommand(ExecuteCommandsQueueAsync);
            ForceStopCommand = new RelayCommand(ForceStop);
        }

        private async void UpdateDeviceStates(bool isProbing)
        {
            Task.Run(() => _commandManager.UpdateStatesAsync());
        }

        private async void ForceStop()
        {

            OutputMessage += $"\nStop.";
            _commandManager.Stop();
            OutputMessage += $"\ndone Stop.";
        }

        private void SaveLog()
        {
            try
            {
                var content = string.Join("\n", _commandManager.GetLog());
                // The name of the file where the content will be saved
                string fileName = "log.txt";

                // Path to save the file in the same project directory
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                File.WriteAllText(filePath, content);
            }
            catch(Exception ex)
            {
                OutputMessage += $"\n{ex.Message}.";
            }

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

        private async void ExecuteCommandsQueueAsync()
        {
            ClearLog();

            OutputMessage += $"\nStop.";
            _commandManager.Stop();
            OutputMessage += $"\ndone Stop.";

            foreach(var (controllerName, controller) in _controllerManager.Controllers)
            {
                if (_controllerManager.ControllerLocks[controllerName].CurrentCount == 0)
                    _controllerManager.ControllerLocks[controllerName].Release();
            }

            _commandManager.ClearQueue();
            foreach (var commandLine in _functionDefinitionLibrary.ExtractCommands())
            {
                _commandManager.EnqueueCommandLine(commandLine);
            }

            //var inputThread = new Thread(async() => await _commandManager.ProcessQueue());
            //inputThread.Start();
            SaveCommandLog();

            //try
            //{
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        SaveLog();
                        await Task.Delay(1000);
                    }
                });
                await Task.Run(() => _commandManager.ProcessQueue());
                OutputMessage += $"\nDone Executing.";
            //}
            //catch(Exception ex)
            //{
            //    OutputMessage += $"\n{ex.Message}";
            //}
            //var highPriorityThread = new Thread(() => _commandManager.ProcessQueue());
            //highPriorityThread.Priority = ThreadPriority.Highest; // Set the priority to Highest
            //highPriorityThread.Start();

            //await Task.Run(() => highPriorityThread.Join()); // Wait for the high-priority thread to finish

            SaveLog();

            //while(_commandManager.CurrentState == CommandManagerState.Processing)
            //{
            //    await Task.Delay(1000);
            //    SaveLog();
            //}
        }

        private void ClearLog()
        {
            _commandManager.ClearLog();

            var content = string.Join("\n", _commandManager.GetLog());
            // The name of the file where the content will be saved
            string fileName = "log.txt";

            // Path to save the file in the same project directory
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            File.WriteAllText(filePath, content);
        }

        private async void CreateCommandQueueFromInputAsync()
        {
            var inputText = SelectedDocument.InputText;
            HighlightedLineNumber = null;
            try
            {
                _functionDefinitionLibrary.ClearCommandQueue();
                _functionDefinitionLibrary.InitializeDefinitions();
                
                _textInterpreter.DefinitionLibrary = _functionDefinitionLibrary.Definitions;
                _textInterpreter.ReadInput(inputText);
                _painterManager.PaintCommandQueue(_functionDefinitionLibrary.ExtractCommands());
            }
            catch (Exception ex)
            {
                OutputMessage += $"\n{ex.Message}";
                if(_textInterpreter.State.CurrentState == ParserState.States.Error)
                {
                    OutputMessage += $"\n{_textInterpreter.State.Message}";
                    HighlightedLineNumber = _textInterpreter.State.LineNumber;
                }
                // update the highlighted line number in red.
            }
        }


        private void AddNewDocument(string content = "")
        {
            var newDoc = new DocumentViewModel
            {
                Name = $"Document {Documents.Count + 1}",
                InputText = content,
            };
            Documents.Add(newDoc);
            newDoc.CloseDocumentRequested += RemoveDocument;
            SelectedDocument = newDoc;
        }

        private void RemoveDocument(DocumentViewModel document)
        {
            if (Documents.Contains(document))
            {
                Documents.Remove(document);
            }
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
                // Extract the file name without extension as the document name
                string documentName = Path.GetFileNameWithoutExtension(filePath);

                // Check if a document with the same name is already open
                int count = Documents.Count(d => d.Name.StartsWith(documentName));

                // If a document with the same name exists, append a number to the new document's name
                if (count > 0)
                {
                    documentName += $" ({count})";
                }

                // Load the file content
                string content;
                using (StreamReader sr = new StreamReader(filePath))
                {
                    content = sr.ReadToEnd();
                }

                // Create a new DocumentViewModel instance with the loaded content and adjusted name
                DocumentViewModel newDocument = new DocumentViewModel
                {
                    Name = documentName,
                    InputText = content, // Assuming CommandText is where you store the document's content
                    FilePath = filePath,
                };

                // Add the new document to the Documents collection
                Documents.Add(newDocument);
                newDocument.CloseDocumentRequested += RemoveDocument;
                // Optionally, set the new document as the currently selected document
                SelectedDocument = newDocument;
                // If you're managing multiple documents, instead of setting CommandText,
                // you might want to create a new DocumentViewModel with the loaded text and add it to your documents collection.
            }
        }

        public List<opentk_painter_library.RenderLayer> GetRenderLayers()
        {
            return _painterManager.GetRenderLayers();
        }

    }
}
