using ICSharpCode.AvalonEdit;
using System.Windows;

namespace standa_control_software_WPF.view_models.system_control.control
{
    /// <summary>
    /// Provides attached properties and behaviors for the <see cref="TextEditor"/> control from AvalonEdit.
    /// Enables two-way binding of the text content between the <see cref="TextEditor"/> and the ViewModel.
    /// </summary>
    public static class TextEditorExtensions
    {
        /// <summary>
        /// Identifies the <see cref="Text"/> attached dependency property.
        /// This property allows binding the text content of a <see cref="TextEditor"/> to a ViewModel property.
        /// </summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached(
                "Text",
                typeof(string),
                typeof(TextEditorExtensions),
                new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        /// <summary>
        /// Sets the value of the <see cref="Text"/> attached property on a specified <see cref="UIElement"/>.
        /// </summary>
        /// <param name="element">The UI element on which to set the property.</param>
        /// <param name="value">The text value to set.</param>
        public static void SetText(UIElement element, string value)
        {
            element.SetValue(TextProperty, value);
        }
        /// <summary>
        /// Gets the value of the <see cref="Text"/> attached property from a specified <see cref="UIElement"/>.
        /// </summary>
        /// <param name="element">The UI element from which to read the property.</param>
        /// <returns>The current text value.</returns>
        public static string GetText(UIElement element)
        {
            return (string)element.GetValue(TextProperty);
        }
        /// <summary>
        /// Handles changes to the <see cref="Text"/> attached property.
        /// Synchronizes the text content of the <see cref="TextEditor"/> with the bound ViewModel property.
        /// </summary>
        /// <param name="d">The dependency object on which the property changed.</param>
        /// <param name="e">Event data that contains information about the property change.</param>
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
        /// <summary>
        /// Handles the <see cref="TextEditor.TextChanged"/> event.
        /// Updates the <see cref="Text"/> attached property to reflect changes made in the <see cref="TextEditor"/>.
        /// </summary>
        /// <param name="sender">The source of the event, expected to be a <see cref="TextEditor"/>.</param>
        /// <param name="e">Event data for the text change.</param>
        private static void TextEditor_TextChanged(object? sender, EventArgs e)
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
