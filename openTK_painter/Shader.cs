using OpenTK.Graphics.OpenGL;
using System.Reflection.Metadata;

namespace opentk_painter_library
{
    public class Shader
    {
        public int ShaderHandle { get; set; }
        public List<IUniform> Uniforms;
        private readonly string _vertexShaderSource;
        private readonly string _fragmentShaderSource;

        public Shader(List<IUniform> uniforms, string vertexSource, string fragmentSource)
        {
            Uniforms = uniforms;
            _vertexShaderSource = vertexSource;
            _fragmentShaderSource = fragmentSource;
        }

        public void CreateShaderProgram()
        {
            int vertexShader = CompileShader(ShaderType.VertexShader, _vertexShaderSource);
            int fragmentShader = CompileShader(ShaderType.FragmentShader, _fragmentShaderSource);

            int program = GL.CreateProgram();

            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, fragmentShader);
            GL.LinkProgram(program);

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var code);
            if (code != (int)All.True)
            {
                var infoLog = GL.GetProgramInfoLog(program);
                throw new Exception($"Error occurred whilst linking Program({program}).\n\n{infoLog}");
            }

            GL.DetachShader(program, vertexShader);
            GL.DetachShader(program, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            ShaderHandle = program;
        }

        private int CompileShader(ShaderType type, string sourcePath)
        {
            var source = File.ReadAllText(sourcePath);
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out var code);
            if (code != (int)All.True)
            {
                var infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception($"Error occurred whilst compiling Shader({shader}).\n\n{infoLog}");
            }

            return shader;
        }

        public void Use()
        {
            GL.UseProgram(ShaderHandle);
        }

        public void UpdateUniformValues()
        {
            foreach (var uniform in Uniforms)
            {
                int location = GL.GetUniformLocation(ShaderHandle, uniform.Name);
                uniform.SetUniform(location);
            }
        }

        public void DisposeShaders()
        {
            GL.DeleteProgram(ShaderHandle);
        }
    }
}
