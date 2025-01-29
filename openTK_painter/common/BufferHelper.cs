using OpenTK.Graphics.OpenGL;
using OpenTK;
using OpenTK.Mathematics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace opentk_painter_library.common
{
    public class BufferHelper
    {
        public static readonly int MinVertexCount = 1;
        public static readonly int MaxVertexCount = 100_000;

        private bool _disposed;

        public readonly int VBO;
        public readonly int VAO;
        public readonly int EBO;
        public VertexInfo? VertexInfo;

        public BufferUsageHint UsageHint;

        public BufferHelper()
        {
            _disposed = false;


            VAO = GL.GenVertexArray();
            VBO = GL.GenBuffer();
            EBO = GL.GenBuffer();
        }

        ~BufferHelper()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            //GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            //GL.DeleteBuffer(VBO);
            //GL.DeleteBuffer(EBO);

            //_disposed = true;
            //GC.SuppressFinalize(this);
        }

        public void SetData<T>(T[] verticesdata, int[] indices, VertexInfo vertexInfo, BufferUsageHint usageHint) where T : struct
        {

            VertexInfo = vertexInfo;
            UsageHint = usageHint;

            if (typeof(T) != VertexInfo.Type)
                throw new ArgumentException("Generic type 'T' does not match the vertex type of the vertex object buffer");

            if (verticesdata == null)
                throw new ArgumentNullException(nameof(verticesdata));

            if (verticesdata.Length <= 0)
                throw new ArgumentOutOfRangeException(nameof(verticesdata));

            GL.BindVertexArray(VAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, verticesdata.Length * VertexInfo.SizeInBytes, verticesdata, UsageHint);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, UsageHint);

            var attributes = VertexInfo.VertexAttributes;
            var size = VertexInfo.SizeInBytes;

            foreach (var attr in attributes)
            {
                GL.VertexAttribPointer(attr.Index, attr.ComponentCount, VertexAttribPointerType.Float, false, size, attr.Offset);
                GL.EnableVertexAttribArray(attr.Index);
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.BindVertexArray(0);
        }

    }
}
