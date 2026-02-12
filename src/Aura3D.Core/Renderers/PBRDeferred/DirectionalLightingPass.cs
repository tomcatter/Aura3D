using Aura3D.Core.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class DirectionalLightingPass : RenderPass
{
    string GbufferRenderTargetName;
    public DirectionalLightingPass(RenderPipeline renderPipeline, string gbufferRendertarget) : base(renderPipeline)
    {
        GbufferRenderTargetName = gbufferRendertarget;
    }

    public override void BeforeRender(Camera camera)
    {
        base.BeforeRender(camera);
    }

    public override void Render(Camera camera)
    {
        RenderQuad();
    }

    public override void AfterRender(Camera camera)
    {
        base.AfterRender(camera);
    }
}
