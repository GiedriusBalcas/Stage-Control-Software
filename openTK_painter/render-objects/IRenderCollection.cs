using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using opentk_painter_library.common;

namespace opentk_painter_library.render_objects
{
    public interface IRenderCollection
    {
        public int VAO { get; set; }
        public int VBO { get; set; }
        public int EBO { get; set; }
        
        public BufferUsageHint UsageHint { get; set; }
        public VertexInfo VertexInfo { get; }
        public PrimitiveType PrimitiveType { get;}

        public void InitializeBuffers();
        public int[] GetIndices();
        public int GetVertexCount();
        public void ClearCollection();
        public Vector3[] GetVertecesPositions();
        public void InitializeDraw();
    }
}