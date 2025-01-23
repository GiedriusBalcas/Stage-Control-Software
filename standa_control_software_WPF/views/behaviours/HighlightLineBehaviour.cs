using System;
using System.Windows;
using ICSharpCode.AvalonEdit;
using Microsoft.Xaml.Behaviors;

namespace standa_control_software_WPF.views.behaviours
{
    public class HighlightLineBehavior : Behavior<TextEditor>
    {
        private HighlightCurrentLineBackgroundRenderer _lineRenderer;

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject != null)
            {
                _lineRenderer = new HighlightCurrentLineBackgroundRenderer(AssociatedObject);
                AssociatedObject.TextArea.TextView.BackgroundRenderers.Add(_lineRenderer);
                AssociatedObject.TextArea.Caret.PositionChanged += CaretOnPositionChanged;
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (AssociatedObject != null)
            {
                AssociatedObject.TextArea.TextView.BackgroundRenderers.Remove(_lineRenderer);
                AssociatedObject.TextArea.Caret.PositionChanged -= CaretOnPositionChanged;
            }
        }

        private void CaretOnPositionChanged(object sender, EventArgs e)
        {
            AssociatedObject.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
        }
    }
}
