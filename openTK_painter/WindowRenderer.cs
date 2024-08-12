using CommandPainter.Common;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace CommandPainter
{
    public class WindowRenderer
    {

        private ShaderHelper _shaderHelper;
        private readonly string vertexShaderSource;
        private readonly string fragmentShaderSource;
        private int _shaderProgram;

        private List<float> _vertexData = new List<float>();
        private int _vao;
        private int _vbo;
        private Matrix4 _projectionMatrix;
        private Matrix4 _viewMatrix;


        public WindowRenderer()
        {
            _shaderHelper = new ShaderHelper();

            vertexShaderSource = File.ReadAllText("DefaultShaders/VertexShader.vert");
            fragmentShaderSource = File.ReadAllText("DefaultShaders/FragmentShader.frag");
        }

        public void UpdateVerticeData(List<float> vertexData)
        {
            _vertexData = vertexData;
            InitializeVertices();
        }

        public void InitializeVertices()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            _shaderProgram = _shaderHelper.CreateShaderProgram(vertexShaderSource, fragmentShaderSource);


            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertexData.Count * sizeof(float), _vertexData.ToArray(), BufferUsageHint.StaticDraw);

            // Position attribute
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Color attribute
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

        }

        public void Draw()
        {
            GL.UseProgram(_shaderProgram);

            int viewLocation = GL.GetUniformLocation(_shaderProgram, "view");
            int projectionLocation = GL.GetUniformLocation(_shaderProgram, "projection");

            GL.UniformMatrix4(viewLocation, false, ref _viewMatrix);
            GL.UniformMatrix4(projectionLocation, false, ref _projectionMatrix);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _vertexData.Count / 7);
            GL.BindVertexArray(0);
            GL.UseProgram(0); // Reset to default shader program after drawing
        }


        internal void Dispose()
        {
            GL.DeleteProgram(_shaderProgram);
        }


        public void UpdateTransformMatrices(Matrix4 projectionMatrix, Matrix4 viewMatrix)
        {
            _projectionMatrix = projectionMatrix;
            _viewMatrix = viewMatrix;
        }

    }
}
