using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit;
using Microsoft.Xaml.Behaviors;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Windows;
using System.Windows.Threading;
using standa_control_software_WPF.view_models.system_control;

namespace standa_control_software_WPF.views.behaviours
{
    public class HighlightLineBehavior : Behavior<TextEditor>
    {
        private DispatcherTimer _updateTimer;
        private int _pendingLineNumberUpdate;
        private LineBackgroundTransformer _highlighter;
        private DocumentViewModel? _viewModel;
        private Color _highlighterColor = Color.FromArgb(255, 255, 0, 0);

        protected override void OnAttached()
        {
            if (Application.Current.Resources["AccentColorBrush"] is SolidColorBrush accentBrush)
            {
                _highlighterColor = accentBrush.Color;
            }

            base.OnAttached();
            this.AssociatedObject.DataContextChanged += OnDataContextChanged;
            AttachBehavior();
        }

        protected override void OnDetaching()
        {
            DetachFromViewModel();
            this.AssociatedObject.DataContextChanged -= OnDataContextChanged;
            base.OnDetaching();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DetachFromViewModel();
            AttachBehavior();
        }

        private void DetachFromViewModel()
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                if (_highlighter != null)
                {
                    var akaka = AssociatedObject.TextArea.TextView.LineTransformers;
                    AssociatedObject.TextArea.TextView.LineTransformers.Remove(_highlighter);
                    _highlighter = null;
                }
                _viewModel = null;

                _updateTimer.Stop();
            }
        }

        private void AttachBehavior()
        {
            _highlighter = new LineBackgroundTransformer()
            {
                LineNumber = -1, // The line you want to highlight
                BackgroundBrush = new SolidColorBrush(_highlighterColor) // Light red background
            };

            _viewModel = AssociatedObject.DataContext as DocumentViewModel;
            if (_viewModel != null)
            {
                AssociatedObject.TextArea.TextView.LineTransformers.Add(_highlighter);

                _viewModel.PropertyChanged += ViewModel_PropertyChanged;


                _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                _updateTimer.Start(); // Start the timer
                _updateTimer.Tick += (s, args) =>
                {
                    if (_pendingLineNumberUpdate != -1)
                    {
                        UpdateLineHighlight(_pendingLineNumberUpdate);
                        _pendingLineNumberUpdate = -1;
                    }
                };
            }
        }

        private void UpdateLineHighlight(int lineNumber)
        {
            Dispatcher.Invoke(() =>
            {
                _highlighter.LineNumber = lineNumber;
                AssociatedObject.TextArea.TextView.Redraw();
            });
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DocumentViewModel.HighlightedLineNumber))
            {
                _pendingLineNumberUpdate = _viewModel.HighlightedLineNumber;
            }
        }



    }


}




