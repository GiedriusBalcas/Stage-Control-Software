using OpenTK.Graphics.OpenGL;


namespace opentk_painter_library
{
    public class UniformSampler2D : IUniform
    {
        public string Name { get; }
        public int TextureUnitIndex { get; set; }

        public UniformSampler2D(string name, int textureUnitIndex)
        {
            Name = name;
            TextureUnitIndex = textureUnitIndex;
        }

        public void SetUniform(int location)
        {
            // Set the uniform to the texture unit index
            GL.Uniform1(location, TextureUnitIndex);
        }
    }
}
