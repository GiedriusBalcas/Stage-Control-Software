using OpenTK.Mathematics;

namespace CommandPainter
{
    public class LinePrimitive
    {
        public Vector3 Start { get; set; }
        public Vector3 End { get; set; }
        public Color4 Color { get; set; }

        public LinePrimitive(System.Numerics.Vector3 start, System.Numerics.Vector3 end, System.Numerics.Vector4 color)
        {
            Start = new Vector3
            {
                X = start.X,
                Y = start.Z,
                Z = start.Y,
            };

            End = new Vector3
            {
                X = end.X,
                Y = end.Z,
                Z = end.Y,
            };

            Color = new Color4
            {
                R = color.X,
                G = color.Y,
                B = color.Z,
                A = color.W,
            };
        }
    }
}
