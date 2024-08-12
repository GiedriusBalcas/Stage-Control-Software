using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL;
using System.Reflection;
using OpenTK.Windowing.GraphicsLibraryFramework;
using opentk_painter_library.common;

namespace opentk_painter_library
{
    public class Window : GameWindow
    {
        private List<RenderLayer> _renderLayers;
        private OrbitalCamera _camera => _renderLayers[0].Camera;


        private Vector2 _lastMousePosition;
        private bool _isLeftMousePressed;
        private bool _isRightMousePressed;

        private float _mouseSensitivity = 0.1f;
        private float _zoomSensitivity = 1f;

        public Window(List<RenderLayer> renderLayer, int width = 1280, int height = 768, string title = "Game1")
            : base(
                  new GameWindowSettings
                  {
                      UpdateFrequency = 25.0, // Update frequency to match render frequency
                  },
                  new NativeWindowSettings()
                  {
                      Title = title,
                      Size = new Vector2i(width, height),
                      WindowBorder = WindowBorder.Fixed,
                      StartVisible = false,
                      StartFocused = true,
                      API = ContextAPI.OpenGL,
                      Profile = ContextProfile.Core,
                      APIVersion = new Version(3, 3)
                  }
                  )
        {

            this.CenterWindow();
            _renderLayers = renderLayer;
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, e.Width, e.Height);
            base.OnResize(e);
        }

        protected override void OnLoad()
        {

            base.OnLoad();
            this.IsVisible = true;
            GL.ClearColor(new Color4(0.2f, 0.3f, 0.3f, 1.0f));
            GL.Enable(EnableCap.DepthTest);

            foreach (var layer in _renderLayers)
            {
                layer.InitializeCollections();
                layer.InitializeShaders();
            }

            base.OnLoad();
        }
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            foreach (var layer in _renderLayers)
            {
                layer.UpdateUniforms();
                layer.DrawLayer();
            }

            Context.SwapBuffers();
            base.OnRenderFrame(args);
        }

        protected override void OnUnload()
        {
            foreach (var layer in _renderLayers)
            {
                layer.DisposeBuffers();
                layer.DisposeShaderProgram();
            }

            base.OnUnload();
        }


        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            var currentMousePosition = new Vector2(e.X, e.Y);
            var deltaX = currentMousePosition.X - _lastMousePosition.X;
            var deltaY = currentMousePosition.Y - _lastMousePosition.Y;

            if (_isLeftMousePressed)
            {
                _camera.Yaw += deltaX * _mouseSensitivity;
                _camera.Pitch += deltaY * _mouseSensitivity;
            }
            else if (_isRightMousePressed)
            {
                var dx = deltaX / Size.X * _camera.Distance;
                var dy = deltaY / Size.Y * _camera.Distance;
                _camera.ReferencePosition += _camera.Right * dx;
                _camera.ReferencePosition += _camera.Up * dy;
            }

            _lastMousePosition = currentMousePosition;

            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Left)
            {
                _isLeftMousePressed = true;
            }
            else if (e.Button == MouseButton.Right)
            {
                _isRightMousePressed = true;
            }


            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Left)
            {
                _isLeftMousePressed = false;
            }
            else if (e.Button == MouseButton.Right)
            {
                _isRightMousePressed = false;
            }

            base.OnMouseUp(e);
        }


        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            _renderLayers[0].Camera.Distance -= e.OffsetY * _zoomSensitivity;
            base.OnMouseWheel(e);
        }

    }
}