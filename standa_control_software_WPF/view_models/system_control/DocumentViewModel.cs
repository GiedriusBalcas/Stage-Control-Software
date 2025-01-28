
using standa_control_software_WPF.view_models.commands;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace standa_control_software_WPF.view_models.system_control
{
    public class DocumentViewModel : ViewModelBase
    {
        /// <summary>
        /// Represents the view model for a document that's used for the user script handling.
        /// Manages document properties, user interactions, and file operations.
        /// </summary>

        private string _name;
        private string _inputText;
        private int _highlightedLineNumber = -1;

        public string Name
        {
            get => _name;
            set 
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        public string InputText 
        {
            get => _inputText; 
            set 
            {
                _inputText = value;
                OnPropertyChanged(nameof(InputText));
            }
        }
        public int HighlightedLineNumber 
        {
            get => _highlightedLineNumber;
            set 
            {
                _highlightedLineNumber = value;
                OnPropertyChanged(nameof(HighlightedLineNumber));
            }
        }
        public string? FilePath { get; internal set; }

        public ICommand CloseDocumentCommand { get; set; }
        public ICommand SaveFileCommand { get; set; }
        public ICommand SaveAsFileCommand { get; set; }

        public event Action<DocumentViewModel>? CloseDocumentRequested;

        public DocumentViewModel(string name, string content)
        {
            _name = name;
            _inputText = content;
            CloseDocumentCommand = new RelayCommand( () => { CloseDocumentRequested?.Invoke(this); } );
            SaveFileCommand = new RelayCommand(SaveFile);
            SaveAsFileCommand = new RelayCommand(SaveAsFile);
        }

        private void SaveFile()
        {
            // Check if the document has an associated file path
            if (!string.IsNullOrEmpty(FilePath))
            {
                // Save the document content to the file
                try 
                { 
                    File.WriteAllText(FilePath, InputText);
                }
                catch(Exception e)
                {
                    MessageBox.Show($"Failed to save a file: {Name} in {FilePath}. {e.Message}");
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