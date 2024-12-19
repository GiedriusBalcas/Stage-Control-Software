using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenTK.Graphics.OpenGL;

namespace opentk_painter_library.common
{
    public struct GlyphInfo
    {
        public char Character;
        public float AdvanceX;
        public float OffsetX, OffsetY; // You can leave these as 0 if you just want simple positioning
        public float Width, Height;
        public float U0, V0, U1, V1;
    }

    public class FontAtlas
    {
        public int AtlasTextureId { get; private set; }
        public Dictionary<char, GlyphInfo> GlyphInfos { get; private set; }
        public float FontSize { get; private set; }
        public int AtlasWidth { get; private set; }
        public int AtlasHeight { get; private set; }

        /// <summary>
        /// Creates a font atlas using System.Drawing to render glyphs.
        /// </summary>
        /// <param name="fontName">The name of the font family (e.g. "Arial")</param>
        /// <param name="fontSize">The font size in points</param>
        /// <param name="characters">A string containing all characters to include in the atlas</param>
        public FontAtlas(string fontName, float fontSize, string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!?,. ")
        {
            FontSize = fontSize;
            GlyphInfos = new Dictionary<char, GlyphInfo>();

            using (var font = new Font(fontName, fontSize, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel))
            {
                CreateAtlas(font, characters);
            }
        }

        private void CreateAtlas(Font font, string characters)
        {
            // First measure max character size
            int maxW = 0;
            int maxH = 0;

            using (Bitmap measureBmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(measureBmp))
            {
                // AntiAlias can provide better quality
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                foreach (char c in characters)
                {
                    string s = c.ToString();
                    SizeF size = g.MeasureString(s, font);
                    maxW = Math.Max(maxW, (int)Math.Ceiling(size.Width));
                    maxH = Math.Max(maxH, (int)Math.Ceiling(size.Height));
                }
            }

            // Arrange characters in a grid
            int charsPerRow = 16;
            int rows = (int)Math.Ceiling((double)characters.Length / charsPerRow);
            AtlasWidth = charsPerRow * maxW;
            AtlasHeight = rows * maxH;

            using (Bitmap atlasBmp = new Bitmap(AtlasWidth, AtlasHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(atlasBmp))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                int charIndex = 0;

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < charsPerRow; c++)
                    {
                        if (charIndex >= characters.Length)
                            break;

                        char ch = characters[charIndex++];
                        string s = ch.ToString();

                        float xPos = c * maxW;
                        float yPos = r * maxH;

                        // Draw the character
                        g.DrawString(s, font, Brushes.White, xPos, yPos);

                        // Store glyph info
                        // For simplicity, assume AdvanceX ~ character width
                        SizeF charSize = g.MeasureString(s, font);
                        float cw = charSize.Width * 0.8f;
                        float chH = charSize.Height;

                        // Compute UV
                        float u0 = xPos / (float)AtlasWidth;
                        float v0 = yPos / (float)AtlasHeight;
                        float u1 = (xPos + cw) / (float)AtlasWidth;
                        float v1 = (yPos + chH) / (float)AtlasHeight;

                        GlyphInfo info = new GlyphInfo
                        {
                            Character = ch,
                            AdvanceX = cw,
                            OffsetX = 0, // If needed, adjust offsets
                            OffsetY = 0,
                            Width = cw,
                            Height = chH,
                            U0 = u0,
                            V0 = v0,
                            U1 = u1,
                            V1 = v1
                        };

                        GlyphInfos[ch] = info;
                    }
                }

                // Upload atlas to OpenGL
                System.Drawing.Imaging.BitmapData data = atlasBmp.LockBits(new Rectangle(0, 0, AtlasWidth, AtlasHeight),
                    ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                AtlasTextureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, AtlasTextureId);
                GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                              AtlasWidth, AtlasHeight, 0,
                              OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                              PixelType.UnsignedByte, data.Scan0);

                atlasBmp.UnlockBits(data);

                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }
        }
    }
}
