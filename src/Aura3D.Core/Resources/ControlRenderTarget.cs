using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aura3D.Core.Renderers;
using Silk.NET.OpenGLES;

namespace Aura3D.Core;

public class ControlRenderTarget : IRenderTarget
{
    public uint FrameBufferId { get; set; }
    public uint Height { get; set; }

    public uint Width { get; set; }

    public bool NeedsUpload { get; set; }

    public void Destroy(GL gl)
    {
    }

    public void Upload(GL gl)
    {
    }
}
