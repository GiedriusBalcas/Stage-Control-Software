using ICSharpCode.AvalonEdit;
using System;
using System.Windows;

namespace standa_control_software_WPF.view_models.system_control.control
{
    public static class TextEditorExtensions
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached(
                "Text",
                typeof(string),
                typeof(TextEditorExtensions),
                new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public static void SetText(UIElement element, string value)
        {
            element.SetValue(TextProperty, value);
        }

        public static string GetText(UIElement element)
        {
            return (string)element.GetValue(TextProperty);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextEditor textEditor)
            {
                textEditor.TextChanged -= TextEditor_TextChanged; // Detach to prevent loop during assignment
                string newText = (string)e.NewValue ?? string.Empty; // Use empty string if null to prevent null reference exception

                // Only update the editor's text if it's actually different to prevent unnecessary loops
                if (textEditor.Text != newText)
                {
                    textEditor.Text = newText;
                }

                textEditor.TextChanged += TextEditor_TextChanged; // Reattach after updating
            }
        }

        private static void TextEditor_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextEditor textEditor)
            {
                string currentText = textEditor.Text;
                // This ensures we're only updating the dependency property if needed
                if (GetText(textEditor) != currentText)
                {
                    SetText(textEditor, currentText);
                }
            }
        }
    }
}
