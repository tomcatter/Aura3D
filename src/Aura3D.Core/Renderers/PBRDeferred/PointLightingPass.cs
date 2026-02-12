using Aura3D.Core.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.PBRDeferred;

public class PointLightingPass : RenderPass
{
    string GbufferRenderTargetName;
    public PointLightingPass(RenderPipeline renderPipeline, string gbufferRendertarget) : base(renderPipeline)
    {
        GbufferRenderTargetName = gbufferRendertarget;

        this.VertexShader = ShaderResource.pbr_directionallight_lighting_pass_vert;

        this.FragmentShader = ShaderResource.pbr_directionallight_lighting_pass_frag;
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);
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
