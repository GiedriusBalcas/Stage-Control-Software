using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using opentk_painter_library.common;
using opentk_painter_library.render_objects;

namespace opentk_painter_library
{
    public abstract class BaseRenderLayer
    {
        protected Shader? _shader;
        public List<IRenderCollection> RenderCollections;
        protected string? _fragmentShader;
        protected string? _vertexShader;
        protected List<IUniform> _uniforms;
        private List<IRenderCollection> InitializedRenderCollections;

        public bool IsGLInitialized { get; set; } = false;
        public BaseRenderLayer()
        {
            RenderCollections = new List<IRenderCollection>();
            InitializedRenderCollections = new List<IRenderCollection>();
            _uniforms = new List<IUniform>();
        }

        public virtual void InitializeLayer()
        {
        }
        public abstract void UpdateUniforms();
        public abstract void OnRenderFrameStart();

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
        }

        public void ClearCollections()
        {
            RenderCollections.Clear();
        }

        public void InitializeShader()
        {
            if (_shader is null)
                throw new Exception("Trying to use shader without initialization.");
            _shader.CreateShaderProgram();
        }

        public void DisposeShaderProgram()
        {
            if (_shader is null)
                throw new Exception("Trying to use shader without initialization.");
            _shader.DisposeShaders();
        }



        public void InitializeCollections()
        {
            DisposeBuffers();
            InitializedRenderCollections.Clear();

            foreach (var collection in RenderCollections)
            {
                if (collection.GetVertexCount() >= 1)
                {
                    collection.InitializeBuffers();
                    InitializedRenderCollections.Add(collection);
                }
            }
        }

        public void DrawLayer()
        {
            if (IsGLInitialized)
            {
                GL.Enable(EnableCap.DepthTest);
                OnRenderFrameStart();

                if (_shader is null)
                    throw new Exception("Trying to use shader without initialization.");

                _shader.Use();
                _shader.UpdateUniformValues();


                foreach (var collection in InitializedRenderCollections)
                {
                    collection.InitializeDraw();

                    GL.BindVertexArray(collection.VAO);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, collection.EBO);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, collection.VBO);

                    GL.DrawElements(collection.PrimitiveType, collection.GetIndices().Length, DrawElementsType.UnsignedInt, 0);
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
                    
                    collection.VBO = 0;
                    collection.EBO = 0;
                    collection.VAO = 0;
                }
                InitializedRenderCollections.Clear();
            }
        }
    }
}