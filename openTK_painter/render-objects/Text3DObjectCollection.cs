using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using opentk_painter_library.common;

namespace opentk_painter_library.render_objects
{
    public class Text3DObjectCollection : IRenderCollection
    {
        private BufferHelper _bufferHelper;
        private List<VertexPositionTexture> _vertices;
        private List<int> _indices;
        private FontAtlas _atlas;

        public int VAO { get; set; }
        public int VBO { get; set; }
        public int EBO { get; set; }

        public BufferUsageHint UsageHint { get; set; } = BufferUsageHint.DynamicDraw;
        public VertexInfo VertexInfo => VertexPositionTexture.VertexInfo;
        public PrimitiveType PrimitiveType => PrimitiveType.Triangles;

        // Structure to hold each string's data
        private struct TextEntry
        {
            public string Text;
            public Vector3 StartPos;
            public Vector3 Direction;
        }

        private List<TextEntry> _textEntries;

        public Text3DObjectCollection(FontAtlas atlas)
        {
            _atlas = atlas;
            _vertices = new List<VertexPositionTexture>();
            _indices = new List<int>();
            _textEntries = new List<TextEntry>();
        }

        /// <summary>
        /// Adds a string to the collection with a specified starting position and direction.
        /// </summary>
        /// <param name="text">The text to render.</param>
        /// <param name="startPos">The starting position (left bottom corner) in 3D space.</param>
        /// <param name="direction">The direction vector pointing to the bottom right corner.</param>
        public void AddString(string text, System.Numerics.Vector3 startPos0, System.Numerics.Vector3 direction0)
        {
            Vector3 startPos = new Vector3(startPos0.X, startPos0.Y, startPos0.Z);
            Vector3 direction = new Vector3(direction0.X, direction0.Y, direction0.Z);

            // Normalize the direction to get the right vector
            Vector3 right = Vector3.Normalize(direction);

            // Calculate the up vector. If direction is parallel to world up, choose another up vector
            Vector3 worldUp = Vector3.UnitY;
            Vector3 up = Vector3.Cross(right, worldUp);
            if (up.LengthSquared < 0.0001f)
            {
                // Direction is parallel to world up, choose a different up vector
                worldUp = Vector3.UnitZ;
                up = Vector3.Cross(right, worldUp);
            }
            up = Vector3.Normalize(up);

            // Store the text entry
            _textEntries.Add(new TextEntry
            {
                Text = text,
                StartPos = startPos,
                Direction = right, // Using right as the normalized direction
            });

            // Rebuild geometry to include the new string
            RebuildGeometry();
            //UpdateBuffers();
        }

        /// <summary>
        /// Clears all added strings from the collection.
        /// </summary>
        public void ClearStrings()
        {
            _textEntries.Clear();
            RebuildGeometry();
            //UpdateBuffers();
        }

        private void RebuildGeometry()
        {
            _vertices.Clear();
            _indices.Clear();

            int startIndex = 0;

            foreach (var entry in _textEntries)
            {
                string text = entry.Text;
                Vector3 startPos = entry.StartPos;
                Vector3 right = entry.Direction;
                Vector3 worldUp = Vector3.UnitY;
                Vector3 up = Vector3.Cross(right, worldUp);
                if (up.LengthSquared < 0.0001f)
                {
                    // Direction is parallel to world up, choose a different up vector
                    worldUp = Vector3.UnitZ;
                    up = Vector3.Cross(right, worldUp);
                }
                up = Vector3.Normalize(up);

                Vector3 currentPos = startPos;

                foreach (var c in text)
                {
                    if (!_atlas.GlyphInfos.TryGetValue(c, out var glyph))
                        continue; // or use a fallback glyph

                    // Calculate the position in 3D space
                    // Bottom-left corner of the glyph
                    Vector3 glyphPos = currentPos + (right * glyph.OffsetX) + (up * (-glyph.OffsetY));

                    // Define the four corners of the glyph quad
                    Vector3 bl = glyphPos;
                    Vector3 br = glyphPos + (right * glyph.Width);
                    Vector3 tr = glyphPos + (right * glyph.Width) + (up * glyph.Height);
                    Vector3 tl = glyphPos + (up * glyph.Height);

                    // Texture coordinates from glyph
                    float u0 = glyph.U0;
                    float v0 = glyph.V1;
                    float u1 = glyph.U1;
                    float v1 = glyph.V0;

                    // Add vertices
                    _vertices.Add(new VertexPositionTexture(bl, new Vector2(u0, v0)));
                    _vertices.Add(new VertexPositionTexture(br, new Vector2(u1, v0)));
                    _vertices.Add(new VertexPositionTexture(tr, new Vector2(u1, v1)));
                    _vertices.Add(new VertexPositionTexture(tl, new Vector2(u0, v1)));

                    // Add indices for two triangles
                    _indices.Add(startIndex + 0);
                    _indices.Add(startIndex + 1);
                    _indices.Add(startIndex + 2);
                    _indices.Add(startIndex + 2);
                    _indices.Add(startIndex + 3);
                    _indices.Add(startIndex + 0);

                    startIndex += 4;

                    // Advance pen position
                    currentPos += right * glyph.AdvanceX;
                }
            }
        }

        //private void UpdateBuffers()
        //{
        //    if (_bufferHelper == null)
        //    {
        //        InitializeBuffers();
        //    }
        //    else
        //    {
        //        _bufferHelper.SetData(_vertices.ToArray(), _indices.ToArray(), VertexInfo, UsageHint);
        //    }
        //}

        public void InitializeBuffers()
        {
            _bufferHelper = new BufferHelper();
            VAO = _bufferHelper.VAO;
            VBO = _bufferHelper.VBO;
            EBO = _bufferHelper.EBO;
            _bufferHelper.SetData(_vertices.ToArray(), _indices.ToArray(), VertexInfo, UsageHint);
        }

        public int[] GetIndices() => _indices.ToArray();

        public Vector3[] GetVertecesPositions()
        {
            var positions = new Vector3[_vertices.Count];
            for (int i = 0; i < _vertices.Count; i++)
            {
                positions[i] = _vertices[i].Position;
            }
            return positions;
        }

        public int GetVertexCount() => _vertices.Count;

        public void InitializeDraw()
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _atlas.AtlasTextureId);
        }

        public void ClearCollection()
        {
            _vertices.Clear();
            _indices.Clear();
            _textEntries.Clear();
            //UpdateBuffers();
        }
    }
}
