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
using standa_control_software_WPF.view_models.system_control;
using standa_control_software_WPF.views.behaviours;
using opentk_painter_library;
using Antlr4.Runtime.Misc;
using opentk_painter_library.common;

namespace standa_control_software_WPF.views.system_control
{
    /// <summary>
    /// Interaction logic for CompilerView.xaml
    /// </summary>
    public partial class SystemControlView : UserControl
    {
       
        private SystemControlViewModel _viewModel;
        private List<RenderLayer> _renderLayers;
        private OrbitalCamera _camera => _renderLayers[0].Camera;
        private System.Windows.Point _lastPos;
        private LineBackgroundTransformer _highlighter;
        private DispatcherTimer _updateTimer;
        private int _pendingLineNumberUpdate;
        private Color4 _backgroundColor = new Color4(0, 0, 0, 1);



        public SystemControlView()
        {
            InitializeComponent();

            var settings = new GLWpfControlSettings { MajorVersion = 3, MinorVersion = 3, GraphicsProfile = ContextProfile.Compatability, GraphicsContextFlags = ContextFlags.Debug };
            glControl.RegisterToEventsDirectly = false;
            glControl.CanInvokeOnHandledEvents = false;
            glControl.Start(settings);
            Loaded += OnLoaded;

        }

        //void UpdateLineHighlight(int lineNumber)
        //{
        //    Dispatcher.Invoke(() =>
        //    {
        //        _highlighter.LineNumber = lineNumber;
        //        AvalonTextEditor.TextArea.TextView.Redraw();
        //    });
        //}

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            
            _viewModel = DataContext as SystemControlViewModel;
            _renderLayers = _viewModel.GetRenderLayers();

            if (_viewModel is not null)
            {
                _renderLayers.ForEach(layer => layer.IsGLInitialized = true);
                glControl.Render += glControl_Render;
                glControl.MouseMove += glControl_MouseMove;
                glControl.MouseWheel += glControl_MouseWheel;
                glControl.Unloaded += glControl_Unload;

                foreach (var layer in _renderLayers)
                {
                    layer.InitializeCollections();
                    layer.InitializeShaders();
                }

            }
            if (Application.Current.Resources["DarkBackgroundColorBrush"] is SolidColorBrush darkBrush)
            {
                var wpfColor = darkBrush.Color;
                _backgroundColor = new Color4(wpfColor.R / 255f, wpfColor.G / 255f, wpfColor.B / 255f, 1);
            }

        }

        private void glControl_Unload(object sender, RoutedEventArgs e)
        {
            foreach (var layer in _renderLayers)
            {
                layer.DisposeBuffers();
                layer.DisposeShaderProgram();
            }
            _renderLayers.ForEach(layer => layer.IsGLInitialized = false);
            glControl.Dispose();
        }

        private void glControl_Render(TimeSpan delta)
        {
            //GL.ClearColor(_backgroundColor);
            //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);


            //float aspectRatio = (float)(glControl.ActualWidth / (float)glControl.ActualHeight);
            //float fovy = 45;

            //_viewModel.UpdateCameraSetting(aspectRatio, fovy);
            //_viewModel.UpdateRenderer();

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            foreach (var layer in _renderLayers)
            {
                layer.UpdateUniforms();
                layer.DrawLayer();
            }

            //Context.SwapBuffers();
            //base.OnRenderFrame(args);

        }

        
        private void glControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                float dx = (float)(pos.X - _lastPos.X);
                float dy = (float)(pos.Y - _lastPos.Y);

                _camera.Yaw += dx;
                _camera.Pitch += dy;
                
                glControl.InvalidateVisual(); // Force re-render
            }

            else if (e.RightButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                var dx = (float)(pos.X - _lastPos.X) / (float)glControl.ActualHeight;
                var dy = (float)(pos.Y - _lastPos.Y) / (float)glControl.ActualHeight; // Use 'dz' to represent movement along camera's local Z axis

                _camera.ReferencePosition += _camera.Right * dx *200;
                _camera.ReferencePosition += _camera.Up * dy *200;

            }
            _lastPos = e.GetPosition(this);
        }

        private void glControl_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            float dr = e.Delta *1;
            _renderLayers[0].Camera.Distance -= dr;
            _renderLayers[0].Camera.AspectRatio = (float)(glControl.ActualWidth / (float)glControl.ActualHeight);
        }

        private void avalonEditor_Loaded(object sender, RoutedEventArgs e)
        {
            var editor = sender as TextEditor;
            if (editor != null)
            {

                var highlighting = editor.SyntaxHighlighting;
                highlighting.GetNamedColor("StringInterpolation").Foreground = new SimpleHighlightingBrush(Colors.White);
                highlighting.GetNamedColor("Punctuation").Foreground = new SimpleHighlightingBrush(Colors.White);
                highlighting.GetNamedColor("NumberLiteral").Foreground = new SimpleHighlightingBrush(Colors.White);
                highlighting.GetNamedColor("Comment").Foreground = new SimpleHighlightingBrush(Colors.LightGreen);
                highlighting.GetNamedColor("MethodCall").Foreground = new SimpleHighlightingBrush(Colors.LightGoldenrodYellow);
                highlighting.GetNamedColor("GetSetAddRemove").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("Visibility").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("ParameterModifiers").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("Modifiers").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("String").Foreground = new SimpleHighlightingBrush(Colors.SandyBrown);
                highlighting.GetNamedColor("Char").Foreground = new SimpleHighlightingBrush(Colors.OrangeRed);
                highlighting.GetNamedColor("Preprocessor").Foreground = new SimpleHighlightingBrush(Colors.White);
                highlighting.GetNamedColor("TrueFalse").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("Keywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("ValueTypeKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("SemanticKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("NamespaceKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("ReferenceTypeKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("ThisOrBaseReference").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("NullOrValueKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("GotoKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("ContextKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("ExceptionKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("CheckedKeyword").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("UnsafeKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("OperatorKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);
                highlighting.GetNamedColor("SemanticKeywords").Foreground = new SimpleHighlightingBrush(Colors.LightBlue);

                foreach (var color in highlighting.NamedHighlightingColors)
                {
                    color.FontWeight = null;
                }
                editor.SyntaxHighlighting = null;
                editor.SyntaxHighlighting = highlighting;
            }


        }
    }
}
