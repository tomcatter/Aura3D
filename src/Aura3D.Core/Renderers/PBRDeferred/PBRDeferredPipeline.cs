using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers.Common;
using Aura3D.Core.Scenes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.PBRDeferred;

public class PBRDeferredPipeline : RenderPipeline, IRenderPipelineCreateInstance
{
    public PBRDeferredPipeline(Scene scene) : base(scene)
    {
        var shadowPass = new ShadowMapPass(this);
        shadowPass.UpdateLightNumLimit(10, 10, 10);
        RegisterRenderPass(shadowPass, RenderPassGroup.Once);
        RegisterRenderPass(new BasePass(this).SetOutPutRenderTarget("GBuffer"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new DirectionalLightingPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new SpotLightingPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PointLightingPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new TranslucentPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);


        RegisterRenderPass(new BackgroundPass(this).SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new CopyPass(this, "BaseRenderTarget", "Color").SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, "BackgroundRenderTarget", "Color").SetOutPutRenderTarget("GammaOutput"), RenderPassGroup.EveryCamera);







        RegisterRenderPass(new FxaaPass(this, "GammaOutput", "Color"), RenderPassGroup.EveryCamera);

        RegisterRenderTarget("GBuffer")
            .AddTexture("BaseColor", TextureFormat.Rgba8)
            .AddTexture("NormalRoughness", TextureFormat.Rgba8)
            .AddTexture("MetallicEmissive", TextureFormat.Rgba8)
            .SetDepthTexture(TextureFormat.DepthComponent16);


        RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(TextureFormat.DepthComponent16);

        RegisterRenderTarget("BackgroundRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(TextureFormat.DepthComponent16);

        RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgb16f)
            .SetDepthTexture(TextureFormat.DepthComponent16);
    }

    public static RenderPipeline CreateInstance(Scene scene) => new PBRDeferredPipeline(scene);

    public override void BeforeCameraRender(Camera camera)
    {
        base.BeforeCameraRender(camera);
        if (gl == null)
            return;
        SortMeshes(VisibleMeshesInCamera, camera);
        gl.Viewport(0, 0, camera.RenderTarget.Width, camera.RenderTarget.Height);
    }
}
