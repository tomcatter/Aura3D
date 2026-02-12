using Aura3D.Core.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class SpotLightingPass : RenderPass
{
    string GbufferRenderTargetName;
    public SpotLightingPass(RenderPipeline renderPipeline, string gbufferRendertarget) : base(renderPipeline)
    {
        GbufferRenderTargetName = gbufferRendertarget;
    }
    public override void BeforeRender(Camera camera)
    {
        base.BeforeRender(camera);
    }

    public override void Render(Camera camera)
    {
        base.Render(camera);

    }

    public override void AfterRender(Camera camera)
    {
        base.AfterRender(camera);
    }
}
