using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace opentk_painter_library
{
    public class RendererWrapper
    {
        private List<RenderLayer> _renderLayers;

        public RendererWrapper(List<RenderLayer> renderLayers)
        {
            _renderLayers = renderLayers;
        }

        public void OnLoad()
        {
            GL.ClearColor(new Color4(0.2f, 0.3f, 0.3f, 1.0f));
            GL.Enable(EnableCap.DepthTest);

            foreach (var layer in _renderLayers)
            {
                layer.InitializeCollections();
                layer.InitializeShaders();
            }
        }
        public void OnRenderFrame()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            foreach (var layer in _renderLayers)
            {
                layer.UpdateUniforms();
                layer.DrawLayer();
            }
        }

        public void OnUnLoad()
        {
            foreach (var layer in _renderLayers)
            {
                layer.DisposeBuffers();
                layer.DisposeShaderProgram();
            }
        }

    }
}
