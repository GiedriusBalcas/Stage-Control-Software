using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace opentk_painter_library
{
    public class TextureHelper
    {
        public static int LoadTextureFromFile(string filepath)
        {
            if (!File.Exists(filepath))
                throw new FileNotFoundException("Texture file not found.", filepath);

            int textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureId);

            // Load the bitmap
            using (var image = new System.Drawing.Bitmap(filepath))
            {
                // Lock the bitmap's bits
                var data = image.LockBits(
                    new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                // Upload the texture to the GPU
                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0, PixelInternalFormat.Rgba,
                    image.Width, image.Height,
                    0, PixelFormat.Bgra,
                    PixelType.UnsignedByte,
                    data.Scan0);

                image.UnlockBits(data);
            }

            // Set texture parameters
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            return textureId;
        }
    }
   

}
