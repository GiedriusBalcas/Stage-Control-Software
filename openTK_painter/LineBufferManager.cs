using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using CommandPainter.Common;
using System.IO;

namespace CommandPainter
{
    public class LineBufferManager
    {
        private List<float> _vertexLineData = new List<float>();

        public void AddLine(LinePrimitive line)
        {
            // Convert line data to vertex data
            _vertexLineData.AddRange(new float[]
            {
            line.Start.X, line.Start.Y, line.Start.Z, line.Color.R, line.Color.G, line.Color.B, line.Color.A,
            line.End.X, line.End.Y, line.End.Z, line.Color.R, line.Color.G, line.Color.B, line.Color.A,
            });
        }

        public void Clear()
        {
            this._vertexLineData.Clear();
        }

        public List<float> GetVerticesData()
        {
            return _vertexLineData;
        }
    }
}
