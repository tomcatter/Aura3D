using Aura3D.Core.Resources;

namespace Aura3D.Core.Renderers;

public interface IRenderTarget : IGpuResource
{
    uint FrameBufferId { get; }
    uint Height { get;}
    uint Width { get;}
    float Scale { get;}
}
