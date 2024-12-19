using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace opentk_painter_library
{
    public interface IRenderGroup
    {
        RenderLayer RenderLayer { get; }
        Shader Shader { get; }

    }
}
