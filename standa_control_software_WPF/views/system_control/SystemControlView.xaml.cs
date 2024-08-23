using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;
using OpenTK.Graphics.OpenGL;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode;
using ICSharpCode.AvalonEdit;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows.Threading;
using OxyPlot;

namespace standa_control_software_WPF.views.system_control
{
    /// <summary>
    /// Interaction logic for CompilerView.xaml
    /// </summary>
    public partial class SystemControlView : UserControl
    {
        public SystemControlView()
        {
            InitializeComponent();
        }



        //private SystemCompilerViewModel _viewModel;
        //private System.Windows.Point _lastPos;
        //private LineBackgroundTransformer _highlighter;
        //private DispatcherTimer _updateTimer;
        //private int _pendingLineNumberUpdate;
        //private Color4 _backgroundColor = new Color4(0, 0, 0, 1);



        //public SystemCompilerView()
        //{
        //    InitializeComponent();

        //    var settings = new GLWpfControlSettings { MajorVersion = 3, MinorVersion = 3, GraphicsProfile = ContextProfile.Compatability, GraphicsContextFlags = ContextFlags.Debug };
        //    glControl.RegisterToEventsDirectly = false;
        //    glControl.CanInvokeOnHandledEvents = false;
        //    glControl.Start(settings);
        //    glControl.RegisterToEventsDirectly = false;
        //    glControl.CanInvokeOnHandledEvents = false;
        //    Loaded += OnLoaded;

        //}

        ////void UpdateLineHighlight(int lineNumber)
        ////{
        ////    Dispatcher.Invoke(() =>
        ////    {
        ////        _highlighter.LineNumber = lineNumber;
        ////        AvalonTextEditor.TextArea.TextView.Redraw();
        ////    });
        ////}

        //private void OnLoaded(object sender, RoutedEventArgs e)
        //{
        //    glControl.RegisterToEventsDirectly = false;
        //    glControl.CanInvokeOnHandledEvents = false;
        //    _viewModel = DataContext as SystemCompilerViewModel;

        //    if(_viewModel is not null) { 
        //        glControl.Render += glControl_Render;
        //        glControl.MouseMove += glControl_MouseMove;
        //        glControl.MouseWheel += glControl_MouseWheel;

        //    }
        //    if (Application.Current.Resources["DarkBackgroundColorBrush"] is SolidColorBrush darkBrush)
        //    {
        //        var wpfColor = darkBrush.Color;
        //        _backgroundColor = new Color4(wpfColor.R / 255f, wpfColor.G / 255f, wpfColor.B / 255f, 1);
        //    }



        //    //var highlighting = editor.SyntaxHighlighting;
        //    //highlighting.GetNamedColor("StringInterpolation").Foreground = new SimpleHighlightingBrush(Colors.Black);
        //    //highlighting.GetNamedColor("Punctuation").Foreground = new SimpleHighlightingBrush(Colors.Black);
        //    //highlighting.GetNamedColor("NumberLiteral").Foreground = new SimpleHighlightingBrush(Colors.Black);
        //    //highlighting.GetNamedColor("Comment").Foreground = new SimpleHighlightingBrush(Colors.ForestGreen);
        //    //highlighting.GetNamedColor("MethodCall").Foreground = new SimpleHighlightingBrush(Colors.DarkGoldenrod);
        //    //highlighting.GetNamedColor("GetSetAddRemove").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("Visibility").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("ParameterModifiers").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("Modifiers").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("String").Foreground = new SimpleHighlightingBrush(Colors.Brown);
        //    //highlighting.GetNamedColor("Char").Foreground = new SimpleHighlightingBrush(Colors.Red);
        //    //highlighting.GetNamedColor("Preprocessor").Foreground = new SimpleHighlightingBrush(Colors.DarkGray);
        //    //highlighting.GetNamedColor("TrueFalse").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("Keywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("ValueTypeKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("SemanticKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("NamespaceKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("ReferenceTypeKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("ThisOrBaseReference").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("NullOrValueKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("GotoKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("ContextKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("ExceptionKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("CheckedKeyword").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("UnsafeKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("OperatorKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);
        //    //highlighting.GetNamedColor("SemanticKeywords").Foreground = new SimpleHighlightingBrush(Colors.Blue);

        //    //foreach (var color in highlighting.NamedHighlightingColors)
        //    //{
        //    //    color.FontWeight = null;
        //    //}
        //    //editor.SyntaxHighlighting = null;
        //    //editor.SyntaxHighlighting = highlighting;

        //}


        //private void glControl_Render(TimeSpan delta)
        //{
        //    GL.ClearColor(_backgroundColor);
        //    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);


        //    float aspectRatio = (float)(glControl.ActualWidth / (float)glControl.ActualHeight);
        //    float fovy = 45;

        //    _viewModel.UpdateCameraSetting(aspectRatio, fovy);
        //    _viewModel.UpdateRenderer();
        //}

        //private void glControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        //{
        //    if (e.LeftButton == MouseButtonState.Pressed)
        //    {
        //        var pos = e.GetPosition(this);
        //        float dx = (float)(pos.X - _lastPos.X);
        //        float dy = (float)(pos.Y - _lastPos.Y);
        //        _viewModel.UpdateCameraOrbitAngles(dx, dy);

        //        glControl.InvalidateVisual(); // Force re-render
        //    }

        //    else if (e.RightButton == MouseButtonState.Pressed)
        //    {
        //        var pos = e.GetPosition(this);
        //        var dx = (float)(pos.X - _lastPos.X) / (float)glControl.ActualHeight;
        //        var dy = (float)(pos.Y - _lastPos.Y) / (float)glControl.ActualHeight; // Use 'dz' to represent movement along camera's local Z axis

        //        _viewModel.UpdateCameraReference(dx, dy);

        //    }
        //    _lastPos = e.GetPosition(this);
        //}

        //private void glControl_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        //{
        //    float dr = e.Delta;
        //    _viewModel.UpdateCameraDistance(dr);
        //}

        //private void AvalonTextEditor_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        //{

        //}

        //private void avalonEditor_Loaded(object sender, RoutedEventArgs e)
        //{
        //    var editor = sender as TextEditor;
        //    if (editor != null)
        //    {

        //        var highlighting = editor.SyntaxHighlighting;
        //        highlighting.GetNamedColor("StringInterpolation").Foreground = new SimpleHighlightingBrush(Colors.White);
        //        highlighting.GetNamedColor("Punctuation").Foreground = new SimpleHighlightingBrush(Colors.White);
        //        highlighting.GetNamedColor("NumberLiteral").Foreground = new SimpleHighlightingBrush(Colors.White);
        //        highlighting.GetNamedColor("Comment").Foreground = new SimpleHighlightingBrush(Colors.LightGreen);
        //        highlighting.GetNamedColor("MethodCall").Foreground = new SimpleHighlightingBrush(Colors.LightGoldenrodYellow);
        //        highlighting.GetNamedColor("GetSetAddRemove").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("Visibility").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("ParameterModifiers").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("Modifiers").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("String").Foreground = new SimpleHighlightingBrush(Colors.SandyBrown);
        //        highlighting.GetNamedColor("Char").Foreground = new SimpleHighlightingBrush(Colors.OrangeRed);
        //        highlighting.GetNamedColor("Preprocessor").Foreground = new SimpleHighlightingBrush(Colors.White);
        //        highlighting.GetNamedColor("TrueFalse").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("Keywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("ValueTypeKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("SemanticKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("NamespaceKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("ReferenceTypeKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("ThisOrBaseReference").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("NullOrValueKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("GotoKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("ContextKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("ExceptionKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("CheckedKeyword").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("UnsafeKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("OperatorKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
        //        highlighting.GetNamedColor("SemanticKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);

        //        foreach (var color in highlighting.NamedHighlightingColors)
        //        {
        //            color.FontWeight = null;
        //        }
        //        editor.SyntaxHighlighting = null;
        //        editor.SyntaxHighlighting = highlighting;
        //    }


        //}
    }
}
