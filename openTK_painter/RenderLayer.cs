using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using opentk_painter_library.common;
using opentk_painter_library.render_objects;

namespace opentk_painter_library
{
    public class RenderLayer
    {
        private Shader _shader;
        private UniformMatrix4 _viewUniform;
        private UniformMatrix4 _projectionUniform;
        private Action _preDrawAction;

        public OrbitalCamera Camera;
        public List<IRenderCollection> RenderCollections;
        public bool IsGLInitialized { get; set; } = false;
        public RenderLayer(string vertexShaderSource, string fragmentShaderSource, Action preDrawAction = null)
        {
            RenderCollections = new List<IRenderCollection>();
            Camera = new OrbitalCamera(1, 45);

            _viewUniform = new UniformMatrix4("view", Camera.GetViewMatrix());
            _projectionUniform = new UniformMatrix4("projection", Camera.GetProjectionMatrix());

            _shader = new Shader([_viewUniform, _projectionUniform], vertexShaderSource, fragmentShaderSource);

            _preDrawAction = preDrawAction;
        }

        public void UpdateUniforms()
        {

            _viewUniform.Value = Camera.GetViewMatrix();
            _projectionUniform.Value = Camera.GetProjectionMatrix();
        }

        public List<Vector3> GetCollectionsVerteces()
        {
            var positions = new List<Vector3>();
            foreach (var collection in RenderCollections)
            {
                positions.AddRange(collection.GetVertecesPositions());
            }

            return positions;
        }

        public void AddObjectCollection(IRenderCollection collection)
        {
            RenderCollections.Add(collection);
            InitializeCollections();
        }

        public void ClearCollections()
        {
            RenderCollections.Clear();
        }

        public void InitializeShaders()
        {
            _shader.CreateShaderProgram();
        }

        public void DisposeShaderProgram()
        {
            _shader.DisposeShaders();
        }



        public void InitializeCollections()
        {
            DisposeBuffers();
            RenderCollections = RenderCollections
                        .Where(collection => collection.GetVertexCount() >= 1)
                        .ToList();

            foreach (var collection in RenderCollections)
            {
                collection.InitializeBuffers();
            }
        }

        public void DrawLayer()
        {
            if (IsGLInitialized)
            {
                _preDrawAction?.Invoke();

                GL.Enable(EnableCap.DepthTest);
                _shader.Use();
                _shader.UpdateUniformValues();


                foreach (var collection in RenderCollections)
                {
                    collection.InitializeDraw();

                    GL.BindVertexArray(collection.VAO);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, collection.EBO);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, collection.VBO);

                    GL.DrawElements(collection.PrimitiveType, collection.GetVertexCount(), DrawElementsType.UnsignedInt, 0);
                    GL.BindVertexArray(0);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                }
                GL.UseProgram(0);
            }
        }

        public void DisposeBuffers()
        {
            if (IsGLInitialized)
            {
                foreach (var collection in RenderCollections)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                    GL.DeleteBuffer(collection.VBO);
                    GL.DeleteBuffer(collection.EBO);
                    GL.BindVertexArray(0);
                    GL.DeleteVertexArray(collection.VAO);
                }
            }
        }
    }
}