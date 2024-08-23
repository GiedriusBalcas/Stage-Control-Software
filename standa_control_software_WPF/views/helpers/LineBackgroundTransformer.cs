using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace standa_control_software_WPF.views.helpers
{
    public class LineBackgroundTransformer : DocumentColorizingTransformer
    {
        public int LineNumber { get; set; }
        public Brush BackgroundBrush { get; set; }

        public LineBackgroundTransformer()
        {
            LineNumber = -1; // Default to an invalid line number
            BackgroundBrush = Brushes.Transparent; // Default background
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (line.LineNumber == LineNumber)
            {
                ChangeLinePart(line.Offset, line.EndOffset, element =>
                    element.TextRunProperties.SetBackgroundBrush(BackgroundBrush));
            }
        }
    }
}
