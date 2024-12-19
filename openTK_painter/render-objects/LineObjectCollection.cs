using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using System.Collections.Concurrent;
using opentk_painter_library.common;


namespace opentk_painter_library.render_objects
{
    public class LineObjectCollection : IRenderCollection
    {
        private BufferHelper _bufferHelper;

        private ConcurrentBag<VertexPositionColor> _vertices;
        private ConcurrentBag<int> _indices;

        public int VAO { get; set; }
        public int VBO { get; set; }
        public int EBO { get; set; }

        public BufferUsageHint UsageHint { get; set; }
        public VertexInfo VertexInfo => VertexPositionColor.VertexInfo;
        public PrimitiveType PrimitiveType => PrimitiveType.Lines;
        public float lineWidth = 1f;

        public LineObjectCollection()
        {
            _vertices = new ConcurrentBag<VertexPositionColor>();
            _indices = new ConcurrentBag<int>();
            UsageHint = BufferUsageHint.StaticDraw;

        }

        public int[] GetIndices()
        {
            return _indices.ToArray();
        }

        public Vector3[] GetVertecesPositions()
        {
            var positions = new Vector3[_vertices.Count];
            if(positions.Length > 1) { 
                int idx = 0;
                foreach (var vertexInfo in _vertices)
                {
                    if(idx < positions.Length)
                        positions[idx++] = vertexInfo.Position;
                }
            }
            
            return positions;
        }
        
        public void AddLine(System.Numerics.Vector3 start, System.Numerics.Vector3 end, System.Numerics.Vector4 color)
        {
            _vertices.Add(
                new VertexPositionColor(new Vector3(start.X, start.Z, start.Y), new Color4(color.X, color.Y, color.Z, color.W))
                );
            _vertices.Add(
                new VertexPositionColor(new Vector3(end.X, end.Z, end.Y), new Color4(color.X, color.Y, color.Z, color.W))
                );
            var currentCount = _indices.Count();
            _indices.Add(0 + currentCount);
            _indices.Add(1 + currentCount);
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
            GL.LineWidth(lineWidth);
        }

        public void ClearCollection()
        {
            _vertices.Clear();
            _indices.Clear();
        }
    }

}
