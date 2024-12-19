using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;

namespace opentk_painter_library.common
{
    public readonly struct VertexAttribute
    {
        public readonly string Name;
        public readonly int Index;
        public readonly int ComponentCount;
        public readonly int Offset;

        public VertexAttribute(string name, int index, int componentCount, int offset)
        {
            Name = name;
            Index = index;
            ComponentCount = componentCount;
            Offset = offset;
        }
    }

    public sealed class VertexInfo
    {
        public Type Type;
        public int SizeInBytes;
        public VertexAttribute[] VertexAttributes;

        public VertexInfo(Type type, params VertexAttribute[] vertexAttributes)
        {
            Type = type;
            SizeInBytes = 0;

            VertexAttributes = vertexAttributes;

            for (int i = 0; i < VertexAttributes.Length; i++)
            {
                VertexAttribute attribute = VertexAttributes[i];
                SizeInBytes += attribute.ComponentCount * sizeof(float);
            }
        }
    }

    public readonly struct VertexPositionColor
    {
        // Field order sensitive code !

        public readonly Vector3 Position;
        public readonly Color4 Color;

        public static readonly VertexInfo VertexInfo = new VertexInfo(
            typeof(VertexPositionColor),
            new VertexAttribute("Position", 0, 3, 0),
            new VertexAttribute("Color", 1, 4, 3 * sizeof(float))
            );

        public VertexPositionColor(Vector3 position, Color4 color)
        {
            Position = position;
            Color = color;
        }
    }

    //public readonly struct VertexPositionTexture
    //{
    //    public readonly Vector3 Position;
    //    public readonly Vector3 TexCoord;

    //    public static readonly VertexInfo VertexInfo = new VertexInfo(
    //        typeof(VertexPositionTexture),
    //        new VertexAttribute("Position", 0, 3, 0),
    //        new VertexAttribute("TexCoord", 1, 3, 3 * sizeof(float))
    //        );
    //    public VertexPositionTexture(Vector3 position, Vector3 texCoord)
    //    {
    //        Position = position;
    //        TexCoord = texCoord;
    //    }
    //}

    public readonly struct VertexPositionTexture
    {
        // Field order sensitive code!

        public readonly Vector3 Position;
        public readonly Vector2 TexCoord;

        public static readonly VertexInfo VertexInfo = new VertexInfo(
            typeof(VertexPositionTexture),
            new VertexAttribute("Position", 0, 3, 0),
            new VertexAttribute("TexCoord", 1, 2, 3 * sizeof(float))
        );

        public VertexPositionTexture(Vector3 position, Vector2 texCoord)
        {
            Position = position;
            TexCoord = texCoord;
        }
    }
}
