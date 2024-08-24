
using standa_control_software_WPF.view_models.commands;
using System.IO;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.system_control
{
    public class DocumentViewModel : ViewModelBase
    {
        // Include the HighlightedLineNumber property and other properties here
        private string _name;
        public string Name
        {
            get => _name;
            set 
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        private string _commandText = "kakams mamakakas \n mamama \n kadasda \n asjdkaskdasd";
        public string InputText 
        {
            get => _commandText; 
            set 
            {
                _commandText = value;
                OnPropertyChanged(nameof(InputText));
            }
        }

        private int _highlightedLineNumber = -1;
        //private BreakpointsManager _breakpointsManager = new BreakpointsManager();

        public int HighlightedLineNumber 
        {
            get => _highlightedLineNumber;
            set 
            {
                _highlightedLineNumber = value;
                OnPropertyChanged(nameof(HighlightedLineNumber));
            }
        }

        public string FilePath { get; internal set; }

        //public BreakpointsManager BreakpointsManager
        //{
        //    get => _breakpointsManager;
        //    set
        //    {
        //        _breakpointsManager = value;
        //        OnPropertyChanged(nameof(BreakpointsManager));
        //    }
        //}
        public ICommand CloseDocumentCommand { get; set; }
        public ICommand SaveFileCommand { get; set; }
        public ICommand SaveAsFileCommand { get; set; }
        public event Action<DocumentViewModel> CloseDocumentRequested;

        public DocumentViewModel()
        {
            CloseDocumentCommand = new RelayCommand( () => { CloseDocumentRequested?.Invoke(this); } );
            SaveFileCommand = new RelayCommand(SaveFile);
            SaveAsFileCommand = new RelayCommand(SaveAsFile);
        }

        private void SaveFile()
        {
            // Check if the document has an associated file path
            if (!string.IsNullOrEmpty(this.FilePath))
            {
                // Save the document content to the file
                try 
                { 
                    File.WriteAllText(this.FilePath, this.InputText);
                }
                catch(Exception e)
                {
                    //throw e;
                }
            }
            else
            {
                // If there's no file path, use Save As functionality instead
                SaveAsFile();
            }
        }

        private void SaveAsFile()
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt",
                DefaultExt = "*.txt",
                FileName = this.Name,
                AddExtension = true
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                this.FilePath = saveFileDialog.FileName;
                this.Name = Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
                File.WriteAllText(saveFileDialog.FileName, InputText);
            }
        }
    }
}