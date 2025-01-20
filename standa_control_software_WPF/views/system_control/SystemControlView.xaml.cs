using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;
using standa_control_software_WPF.view_models.system_control;
using standa_control_software_WPF.view_models.system_control.control;
using standa_control_software_WPF.views.behaviours;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace standa_control_software_WPF.views.system_control
{
    /// <summary>
    /// Interaction logic for CompilerView.xaml
    /// </summary>
    public partial class SystemControlView : UserControl
    {
        private PainterManagerViewModel? _viewModel;
        private CameraViewModel _cameraViewModel;
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

        private void GlControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _cameraViewModel.AspectRatio = (float)(glControl.ActualWidth / (float)glControl.ActualHeight);

            _cameraViewModel.WindowWidth = (float)glControl.ActualWidth;
            _cameraViewModel.WindowHeight = (float)glControl.ActualHeight;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel = (DataContext as SystemControlViewModel)?.PainterManager;
            //_viewModel = glControl.DataContext as PainterManagerViewModel;
            if (_viewModel is not null)
            {
                _cameraViewModel = _viewModel.CameraViewModel;
                _viewModel.InitializeLayers();

                _cameraViewModel.WindowWidth = (float)glControl.ActualWidth;
                _cameraViewModel.WindowHeight = (float)glControl.ActualHeight;

                glControl.SizeChanged += GlControl_SizeChanged;
                glControl.Render += glControl_Render;
                glControl.MouseMove += glControl_MouseMove;
                glControl.MouseWheel += glControl_MouseWheel;
                glControl.Unloaded += glControl_Unload;
            }

            if (Application.Current.Resources["DarkBackgroundColorBrush"] is SolidColorBrush darkBrush)
            {
                var wpfColor = darkBrush.Color;
                _backgroundColor = new Color4(wpfColor.R / 255f, wpfColor.G / 255f, wpfColor.B / 255f, 1);
            }

        }

        private void glControl_Unload(object sender, RoutedEventArgs e)
        {
            if(_viewModel is not null)
                _viewModel.DeinitializeLayers();

            glControl.Dispose();
            glControl.SizeChanged -= GlControl_SizeChanged;
            glControl.Render -= glControl_Render;
            glControl.MouseMove -= glControl_MouseMove;
            glControl.MouseWheel -= glControl_MouseWheel;
            glControl.Unloaded -= glControl_Unload;
        }

        private void glControl_Render(TimeSpan delta)
        {
            GL.ClearColor(_backgroundColor);
            // Enable blending
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if(_viewModel is not null)
                _viewModel.DrawFrame();
        }

        
        private void glControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                float dx = (float)(pos.X - _lastPos.X);
                float dy = (float)(pos.Y - _lastPos.Y);

                _cameraViewModel.Yaw += dx;
                _cameraViewModel.Pitch += dy;

                glControl.InvalidateVisual(); // Force re-render
            }

            else if (e.RightButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                var dx = (float)(pos.X - _lastPos.X) / (float)glControl.ActualHeight;
                var dy = (float)(pos.Y - _lastPos.Y) / (float)glControl.ActualHeight; // Use 'dz' to represent movement along camera's local Z axis

                _cameraViewModel.ReferencePositionXY = new Vector2(dx, dy);
            }
            _lastPos = e.GetPosition(this);
        }

        private void glControl_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            float delta = e.Delta;
            var dr = Math.Sign(delta) * 1;

            _cameraViewModel.Distance -= dr;
            _cameraViewModel.AspectRatio = (float)(glControl.ActualWidth / (float)glControl.ActualHeight);

            _cameraViewModel.WindowWidth = (float)glControl.ActualWidth;
            _cameraViewModel.WindowHeight = (float)glControl.ActualHeight;
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
