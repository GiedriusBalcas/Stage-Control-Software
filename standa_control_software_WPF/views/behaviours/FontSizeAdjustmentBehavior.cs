using ICSharpCode.AvalonEdit;
using Microsoft.Xaml.Behaviors;
using System.Windows.Input;

namespace standa_control_software_WPF.views.behaviours
{
    public class FontSizeAdjustmentBehavior : Behavior<TextEditor>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            this.AssociatedObject.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        protected override void OnDetaching()
        {
            this.AssociatedObject.PreviewMouseWheel -= OnPreviewMouseWheel;
            base.OnDetaching();
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                const double fontSizeChange = 1;
                var editor = sender as TextEditor;

                if (editor != null)
                {
                    if (e.Delta > 0)
                    {
                        editor.FontSize += fontSizeChange;
                    }
                    else if (e.Delta < 0)
                    {
                        editor.FontSize = Math.Max(editor.FontSize - fontSizeChange, 1);
                    }

                    e.Handled = true;
                }
            }
        }
    }
}
