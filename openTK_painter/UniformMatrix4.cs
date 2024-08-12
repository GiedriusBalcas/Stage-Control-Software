using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace opentk_painter_library
{
    public class UniformMatrix4 : IUniform
    {
        public Matrix4 Value;
        private readonly string _name;

        public string Name
        {
            get { return _name; }
        }

        public UniformMatrix4(string name, Matrix4 value)
        {
            _name = name;
            Value = value;
        }

        public void SetUniform(int location)
        {
            GL.UniformMatrix4(location, false, ref Value);
        }
    }
}
