using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using opentk_painter_library.common;

namespace opentk_painter_library.render_objects
{
    public class PointObjectCollection : IRenderCollection
    {
        private BufferHelper _bufferHelper;

        private List<VertexPositionColor> _vertices;
        private List<int> _indices;
        private float _width;

        public int VAO { get; set; }
        public int VBO { get; set; }
        public int EBO { get; set; }

        public BufferUsageHint UsageHint { get; set; }
        public VertexInfo VertexInfo => VertexPositionColor.VertexInfo;
        public PrimitiveType PrimitiveType => PrimitiveType.Points;

        public PointObjectCollection()
        {
            _vertices = new List<VertexPositionColor>();
            _indices = new List<int>();
            UsageHint = BufferUsageHint.DynamicDraw;
        }

        public Vector3[] GetVertecesPositions()
        {
            var positions = new Vector3[_vertices.Count];
            int idx = 0;
            _vertices.ForEach(vertexInfo => positions[idx++] = vertexInfo.Position);

            return positions;
        }

        public int[] GetIndices()
        {
            return _indices.ToArray();
        }

        public void AddPoint(System.Numerics.Vector3 location, float width, System.Numerics.Vector4 color)
        {
            _width = width;
            _vertices.Add(
                new VertexPositionColor(
                    new Vector3(location.X, location.Z, location.Y),
                    new Color4(color.X, color.Y, color.Z, color.W))
                );
            var count = _indices.Count;
            _indices.Add(count+0);
        }

        public void InitializeBuffers()
        {
            _bufferHelper = new BufferHelper();
           
            
            VAO = _bufferHelper.VAO;
            VBO = _bufferHelper.VBO;
            EBO = _bufferHelper.EBO;

            _bufferHelper.SetData(_vertices.ToArray(), _indices.ToArray(), VertexInfo, UsageHint);
        }

        public int GetVertexCount()
        {
            return _vertices.Count;
        }

        public void InitializeDraw()
        {
            GL.PointSize(_width);
        }

        public void ClearCollection()
        {
            _vertices.Clear();
            _indices.Clear();
        }
    }
}
