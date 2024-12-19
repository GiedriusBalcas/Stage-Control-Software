using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using opentk_painter_library.common;

namespace opentk_painter_library.render_objects
{
    public class TextObjectCollection : IRenderCollection
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

        private string _currentText = "";
        private float _posX;
        private float _posY;

        // The constructor takes a FontAtlas and a position (x,y) where text will start.
        public TextObjectCollection(FontAtlas atlas, float startX = 0f, float startY = 0f)
        {
            _atlas = atlas;
            _vertices = new List<VertexPositionTexture>();
            _indices = new List<int>();
            _posX = startX;
            _posY = startY;
        }

        public void SetString(string text, float posX = float.NaN, float posY = float.NaN)
        {
            if (!float.IsNaN(posY))
                _posY = posY;

            if (!float.IsNaN(posX))
                _posX = posX;

            _currentText = text;
            RebuildGeometry();
        }

        private void RebuildGeometry()
        {
            _vertices.Clear();
            _indices.Clear();

            float x = _posX;
            float y = _posY;

            int startIndex = 0;

            foreach (var c in _currentText)
            {
                if (!_atlas.GlyphInfos.TryGetValue(c, out var glyph))
                    continue; // or use a fallback glyph

                // Calculate positions
                float x0 = x + glyph.OffsetX;
                float y0 = y - glyph.OffsetY;
                float x1 = x0 + glyph.Width;
                float y1 = y0 + glyph.Height;

                // Texture coords from glyph
                float u0 = glyph.U0;
                float v0 = glyph.V1;
                float u1 = glyph.U1;
                float v1 = glyph.V0;

                _vertices.Add(new VertexPositionTexture(new Vector3(x0, y0, 0f), new Vector2(u0, v0)));
                _vertices.Add(new VertexPositionTexture(new Vector3(x1, y0, 0f), new Vector2(u1, v0)));
                _vertices.Add(new VertexPositionTexture(new Vector3(x1, y1, 0f), new Vector2(u1, v1)));
                _vertices.Add(new VertexPositionTexture(new Vector3(x0, y1, 0f), new Vector2(u0, v1)));

                _indices.Add(startIndex + 0);
                _indices.Add(startIndex + 1);
                _indices.Add(startIndex + 2);
                _indices.Add(startIndex + 2);
                _indices.Add(startIndex + 3);
                _indices.Add(startIndex + 0);

                startIndex += 4;

                // Advance pen position
                x += glyph.AdvanceX;
            }
        }

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
        }
    }
}
