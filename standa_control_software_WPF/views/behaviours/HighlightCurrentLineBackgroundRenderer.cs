using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using OpenTK.Mathematics;
using System.Linq;
using System.Windows;
using System.Windows.Media;

public class HighlightCurrentLineBackgroundRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private Pen _borderPen;

    public HighlightCurrentLineBackgroundRenderer(TextEditor editor)
    {
        _editor = editor;

        _borderPen = new Pen(Brushes.Gray, 1.0);
        // Example: a 1px yellow border. Adjust color/thickness as you wish:
        if (System.Windows.Application.Current.Resources["LightCustomColorBrush"] is SolidColorBrush darkBrush)
        {
            _borderPen = new Pen(darkBrush, 0.5);
        }
        _borderPen.Freeze();
    }

    // Draw on the same layer as the text selection so it's behind text but above the editor background
    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {

        // If there are no visual lines (e.g. empty document), nothing to draw
        if (textView.VisualLines.Count == 0)
            return;

        // Current caret line in the document
        int caretLine = _editor.TextArea.Caret.Line;
        if (caretLine < 1 || caretLine > _editor.Document.LineCount)
            return;

        // Find the visual line that contains the caret
        var visualLine = textView.VisualLines
            .FirstOrDefault(vl => vl.FirstDocumentLine.LineNumber <= caretLine
                               && vl.LastDocumentLine.LineNumber >= caretLine);
        if (visualLine == null)
            return;

        // ---------------------------------------
        // 1) Clip to the visible editor region
        // ---------------------------------------
        drawingContext.PushClip(
            new RectangleGeometry(new Rect(0, 0, textView.ActualWidth, textView.ActualHeight))
        );

        // ---------------------------------------
        // 2) Apply negative scroll transform
        //    to align unscrolled coordinates
        //    with what's currently visible
        // ---------------------------------------
        var scrollOffset = textView.ScrollOffset; // X and Y
        drawingContext.PushTransform(
            new TranslateTransform(-scrollOffset.X, -scrollOffset.Y)
        );

        // visualLine.VisualTop is the Y in "unscrolled" coords
        double lineTop = visualLine.VisualTop;
        double lineHeight = visualLine.Height;

        // If you want the border to span the entire text area (including the margin),
        // use 0 as the left edge. 
        // Or if you want to skip the line-number margin: use textView.VisualEdgeLeft.
        double lineLeft = scrollOffset.X - 10;

        // If you want the border to extend across the entire document width:
        double lineWidth = textView.ActualWidth + scrollOffset.X +20;

        // Construct a rectangle from (lineLeft, lineTop) spanning (lineWidth x lineHeight)
        var highlightRect = new Rect(lineLeft, lineTop, lineWidth, lineHeight);

        // ---------------------------------------
        // 3) Draw only the border (no fill)
        // ---------------------------------------
        drawingContext.DrawRectangle(
            brush: null,      // no background fill
            pen: _borderPen,  // use our border pen
            rectangle: highlightRect
        );

        // ---------------------------------------
        // 4) Cleanup transforms / clip
        // ---------------------------------------
        drawingContext.Pop(); // Pop the translate transform
        drawingContext.Pop(); // Pop the clipping region
    }
}
