using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using opentk_painter_library.common;

namespace opentk_painter_library.render_objects
{
    public class RectangleTextureObjectCollection : IRenderCollection
    {
        private BufferHelper _bufferHelper;
        private List<VertexPositionTexture> _vertices;
        private List<int> _indices;

        public int VAO { get; set; }
        public int VBO { get; set; }
        public int EBO { get; set; }

        public BufferUsageHint UsageHint { get; set; }
        public VertexInfo VertexInfo => VertexPositionTexture.VertexInfo;
        public PrimitiveType PrimitiveType => PrimitiveType.Triangles;

        private int _textureID;

        /// <summary>
        /// Creates a new texture-based rectangle collection.
        /// textureID is the OpenGL texture handle you want to draw with.
        /// </summary>
        public RectangleTextureObjectCollection(int textureID)
        {
            _vertices = new List<VertexPositionTexture>();
            _indices = new List<int>();
            UsageHint = BufferUsageHint.DynamicDraw;
            _textureID = textureID;
        }

        /// <summary>
        /// Add a textured rectangle. The rectangle will be composed of two triangles.
        /// Positions should be provided in your world/camera coordinate system.
        /// The coordinates are mapped similar to your existing classes: (X, Z, Y)
        /// for Position.
        ///
        /// topLeftUV, topRightUV, bottomRightUV, bottomLeftUV define how the texture
        /// is mapped onto the rectangle.
        /// 
        /// Example UV mapping:
        /// topLeftUV = (0,0)
        /// topRightUV = (1,0)
        /// bottomRightUV = (1,1)
        /// bottomLeftUV = (0,1)
        /// </summary>
        public void AddRectangle(System.Numerics.Vector3 topLeft,
                                 System.Numerics.Vector3 topRight,
                                 System.Numerics.Vector3 bottomRight,
                                 System.Numerics.Vector3 bottomLeft,
                                 System.Numerics.Vector2 topLeftUV,
                                 System.Numerics.Vector2 topRightUV,
                                 System.Numerics.Vector2 bottomRightUV,
                                 System.Numerics.Vector2 bottomLeftUV)
        {
            int startIndex = _vertices.Count;

            // Convert from System.Numerics.Vector3 to OpenTK.Vector3 and reorder (X,Y,Z)->(X,Z,Y)
            var vTopLeft = new Vector3(topLeft.X, topLeft.Z, topLeft.Y);
            var vTopRight = new Vector3(topRight.X, topRight.Z, topRight.Y);
            var vBottomRight = new Vector3(bottomRight.X, bottomRight.Z, bottomRight.Y);
            var vBottomLeft = new Vector3(bottomLeft.X, bottomLeft.Z, bottomLeft.Y);

            var vtopLeftUV = new Vector2(topLeftUV.X, topLeftUV.Y);
            var vtopRightUV = new Vector2(topRightUV.X, topRightUV.Y);
            var vbottomRightUV = new Vector2(bottomRightUV.X, bottomRightUV.Y);
            var vbottomLeftUV = new Vector2(bottomLeftUV.X, bottomLeftUV.Y);

            // Add vertices
            _vertices.Add(new VertexPositionTexture(vTopLeft, vtopLeftUV));       // v0
            _vertices.Add(new VertexPositionTexture(vTopRight, vtopRightUV));     // v1
            _vertices.Add(new VertexPositionTexture(vBottomRight, vbottomRightUV));// v2
            _vertices.Add(new VertexPositionTexture(vBottomLeft, vbottomLeftUV)); // v3

            // Indices for two triangles forming the rectangle
            // Triangle 1: v0, v1, v2
            _indices.Add(startIndex + 0);
            _indices.Add(startIndex + 1);
            _indices.Add(startIndex + 2);

            // Triangle 2: v2, v3, v0
            _indices.Add(startIndex + 2);
            _indices.Add(startIndex + 3);
            _indices.Add(startIndex + 0);
        }

        public void InitializeBuffers()
        {
            _bufferHelper = new BufferHelper();
            VAO = _bufferHelper.VAO;
            VBO = _bufferHelper.VBO;
            EBO = _bufferHelper.EBO;

            _bufferHelper.SetData(_vertices.ToArray(), _indices.ToArray(), VertexInfo, UsageHint);
        }

        public int[] GetIndices()
        {
            return _indices.ToArray();
        }

        public Vector3[] GetVertecesPositions()
        {
            var positions = new Vector3[_vertices.Count];
            for (int i = 0; i < _vertices.Count; i++)
            {
                positions[i] = _vertices[i].Position;
            }
            return positions;
        }

        public int GetVertexCount()
        {
            return _vertices.Count;
        }

        public void InitializeDraw()
        {
            // Bind the texture before drawing
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _textureID);
        }

        public void ClearCollection()
        {
            _vertices.Clear();
            _indices.Clear();
        }
    }
}
