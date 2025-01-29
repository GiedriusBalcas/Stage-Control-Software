using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using standa_control_software_WPF.view_models.system_control;
using standa_control_software_WPF.view_models.system_control.control;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace standa_control_software_WPF.views.system_control
{
    /// <summary>
    /// Interaction logic for CompilerView.xaml
    /// </summary>
    public partial class SystemControlView : System.Windows.Controls.UserControl
    {
        private PainterManagerViewModel? _viewModel;
        private CameraViewModel? _cameraViewModel;
        private Color4 _backgroundColor = new Color4(0, 0, 0, 1);

        private GLControl? _glControl; // declare a field if you need to reference it later
        private System.Windows.Forms.Timer? _timer;
        private System.Drawing.Point _lastPos;

        public SystemControlView()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {

            if (_viewModel is not null)
                _viewModel.DeinitializeLayers();

            if (_glControl != null)
            {
                _timer?.Stop();
                _glControl.SizeChanged -= glControl_SizeChanged; ;
                _glControl.MouseWheel -= glControl_MouseWheel;
                _glControl.MouseMove -= glControl_MouseMove;
                _glControl.Load -= glControl_Load;
                _glControl.Dispose();
            }

        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

            if (System.Windows.Application.Current.Resources["MidCustomColorBrush"] is SolidColorBrush darkBrush)
            {
                var wpfColor = darkBrush.Color;
                _backgroundColor = new Color4(wpfColor.R / 255f, wpfColor.G / 255f, wpfColor.B / 255f, 1);
            }

            _glControl = new GLControl();
            _glControl.Dock = DockStyle.Fill; 
            _glControl.API = OpenTK.Windowing.Common.ContextAPI.OpenGL;
            _glControl.APIVersion = new System.Version(3, 3, 0, 0);
            _glControl.Flags = OpenTK.Windowing.Common.ContextFlags.Debug;
            _glControl.IsEventDriven = false;
            _glControl.Name = "glControl";
            _glControl.Profile = OpenTK.Windowing.Common.ContextProfile.Core;
            _glControl.SharedContext = null;
            _glControl.Size = new System.Drawing.Size(433, 281);

            _glControl.Text = "glControl1";
            _glControl.Load += glControl_Load;

            windowsFormsHost.Child = _glControl;
            _glControl.Size = new System.Drawing.Size(_glControl.ClientSize.Width, _glControl.ClientSize.Height);


            if (_glControl.Context is not null && !_glControl.Context.IsCurrent)
                _glControl.MakeCurrent();


            _viewModel = (DataContext as SystemControlViewModel)?.PainterManager;
            if (_viewModel is not null)
            {
                _cameraViewModel = _viewModel.CameraViewModel;
                _viewModel.InitializeLayers();

                _cameraViewModel.WindowWidth = (float)_glControl.Width;
                _cameraViewModel.WindowHeight = (float)_glControl.Height;
            }


            glControl_SizeChanged(_glControl, EventArgs.Empty);


        }

        private void glControl_Load(object? sender, EventArgs e)
        {
            if (_glControl is null)
                return;

            _glControl.SizeChanged += glControl_SizeChanged; ;
            _glControl.MouseWheel += glControl_MouseWheel;
            _glControl.MouseMove += glControl_MouseMove;
            
            // Redraw the screen every 1/20 of a second.
            _timer = new System.Windows.Forms.Timer();
            _timer.Tick += (sender, e) =>
            {
                Render();
            };
            _timer.Interval = 20;   // 1000 ms per sec / 50 ms per frame = 20 FPS
            _timer.Start();

        }

        private void glControl_MouseMove(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_cameraViewModel is null || _glControl is null)
                return;

            if (e.Button == MouseButtons.Left)
            {
                var pos = e.Location;
                float dx = (float)(pos.X - _lastPos.X);
                float dy = (float)(pos.Y - _lastPos.Y);

                _cameraViewModel.Yaw += dx * 0.3f;
                _cameraViewModel.Pitch += dy * 0.3f;
            }

            else if (e.Button == MouseButtons.Right)
            {
                var pos = e.Location;
                var dx = (float)(pos.X - _lastPos.X) / (float)_glControl.Height;
                var dy = (float)(pos.Y - _lastPos.Y) / (float)_glControl.Height; // Use 'dz' to represent movement along camera's local Z axis

                _cameraViewModel.ReferencePositionXY = new Vector2(dx, dy);
            }
            _lastPos = e.Location;
        }

        private void glControl_MouseWheel(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_cameraViewModel is null || _glControl is null)
                return;

            float delta = e.Delta;
            var dr = Math.Sign(delta) * 1;

            _cameraViewModel.Distance -= dr;
            _cameraViewModel.AspectRatio = (float)(_glControl.Width / (float)_glControl.Height);

            _cameraViewModel.WindowWidth = (float)_glControl.Width;
            _cameraViewModel.WindowHeight = (float)_glControl.Height;
        }

        private void Render()
        {
            if(_glControl is not null && _viewModel is not null)
            {
                GL.ClearColor(_backgroundColor);
                // Enable blending
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                _viewModel.DrawFrame();

                _glControl.SwapBuffers();
            }

        }

        private void glControl_SizeChanged(object? sender, EventArgs e)
        {
            if (_glControl is null)
                return;

            if (_glControl.Context is not null &&!_glControl.Context.IsCurrent)
                _glControl.MakeCurrent();

            if (_glControl.ClientSize.Height == 0)
                _glControl.ClientSize = new System.Drawing.Size(_glControl.ClientSize.Width, 1);

            GL.Viewport(0, 0, _glControl.ClientSize.Width, _glControl.ClientSize.Height);

            if(_cameraViewModel is not null)
            {
                float aspect_ratio = Math.Max(_glControl.Width, 1) / (float)Math.Max(_glControl.Height, 1);
                _cameraViewModel.AspectRatio = aspect_ratio;

                _cameraViewModel.WindowWidth = (float)Math.Max(_glControl.Width, 1);
                _cameraViewModel.WindowHeight = (float)Math.Max(_glControl.Height, 1);
            }
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
