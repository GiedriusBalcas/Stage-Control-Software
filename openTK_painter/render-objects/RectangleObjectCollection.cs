using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using opentk_painter_library.common;

namespace opentk_painter_library.render_objects
{
    public class RectangleObjectCollection : IRenderCollection
    {
        private BufferHelper _bufferHelper;

        private List<VertexPositionColor> _vertices;
        private List<int> _indices;

        public int VAO { get; set; }
        public int VBO { get; set; }
        public int EBO { get; set; }

        public BufferUsageHint UsageHint { get; set; }
        public VertexInfo VertexInfo => VertexPositionColor.VertexInfo;
        public PrimitiveType PrimitiveType => PrimitiveType.Triangles;

        public RectangleObjectCollection()
        {
            _vertices = new List<VertexPositionColor>();
            _indices = new List<int>();
            UsageHint = BufferUsageHint.DynamicDraw;
        }

        /// <summary>
        /// Adds a rectangle defined by four corner points and a color.
        /// The rectangle is formed by two triangles:
        /// Triangle 1: v0, v1, v2
        /// Triangle 2: v2, v3, v0
        /// </summary>
        /// <param name="topLeft">Top-left corner of the rectangle</param>
        /// <param name="topRight">Top-right corner of the rectangle</param>
        /// <param name="bottomRight">Bottom-right corner of the rectangle</param>
        /// <param name="bottomLeft">Bottom-left corner of the rectangle</param>
        /// <param name="color">Color of the rectangle</param>
        public void AddRectangle(System.Numerics.Vector3 topLeft,
                                 System.Numerics.Vector3 topRight,
                                 System.Numerics.Vector3 bottomRight,
                                 System.Numerics.Vector3 bottomLeft,
                                 System.Numerics.Vector4 color)
        {
            // Convert from System.Numerics.Vector3/4 to OpenTK.Vector3/Color4
            var c = new Color4(color.X, color.Y, color.Z, color.W);

            // In some coordinate systems you might want to reorder the Y/Z
            // components, depending on how you've arranged your camera.
            // Here we follow the pattern from the existing classes:
            // Position = new Vector3(X, Z, Y)
            // This seems to be a specific orientation used in the given code.
            var v0 = new VertexPositionColor(new Vector3(topLeft.X, topLeft.Z, topLeft.Y), c);
            var v1 = new VertexPositionColor(new Vector3(topRight.X, topRight.Z, topRight.Y), c);
            var v2 = new VertexPositionColor(new Vector3(bottomRight.X, bottomRight.Z, bottomRight.Y), c);
            var v3 = new VertexPositionColor(new Vector3(bottomLeft.X, bottomLeft.Z, bottomLeft.Y), c);

            int startIndex = _vertices.Count;
            _vertices.AddRange(new[] { v0, v1, v2, v3 });

            // Indices for the two triangles: 
            // First triangle: (v0, v1, v2)
            // Second triangle: (v2, v3, v0)
            _indices.Add(startIndex + 0);
            _indices.Add(startIndex + 1);
            _indices.Add(startIndex + 2);

            _indices.Add(startIndex + 0);
            _indices.Add(startIndex + 2);
            _indices.Add(startIndex + 3);
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
            return _indices.Count;
        }

        public void InitializeDraw()
        {
            // No special draw initialization required for rectangles,
            // but you could set polygon mode or other states here if desired.
            // For example: GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }

        public void ClearCollection()
        {
            _vertices.Clear();
            _indices.Clear();
        }
    }
}
