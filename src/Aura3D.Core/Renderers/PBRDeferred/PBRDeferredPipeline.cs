using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers.Common;
using Aura3D.Core.Scenes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.PBRDeferred;

public class PBRDeferredPipeline : RenderPipeline, IRenderPipelineCreateInstance
{

    public Resources.Texture DefaultBaseColor { get; private set; }

    public Resources.Texture DefaultNormal { get; private set; }

    public Resources.Texture DefaultMetallicRoughness { get; private set; }

    public Resources.Texture DefaultEmissive { get; private set; }

    public Resources.Texture DefaultOcclusion { get; private set; }

    private RenderTarget _brdfLutRenderTarget;

    public PBRDeferredPipeline(Scene scene) : base(scene)
    {
        _brdfLutRenderTarget = new RenderTarget();

        _brdfLutRenderTarget.AddRenderTexture("BrdfLut", TextureFormat.Rgb16f)
            .SetDepthTexture(TextureFormat.Depth24Stencil8)
            .SetSize(512, 512);


        var shadowPass = new ShadowMapPass(this);

        shadowPass.UpdateLightNumLimit(10, 10, 10);

        RegisterRenderPass(shadowPass, RenderPassGroup.Once);

        RegisterRenderPass(new BrdfLutPass(this, _brdfLutRenderTarget), RenderPassGroup.Once);

        RegisterRenderPass(new IrradianceMapPass(this), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PrefilteredEnvironmentMapPass(this), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PrefilteredEnvironmentMapPass(this), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new BasePass(this).SetOutPutRenderTarget("GBuffer"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new IBLAmbientPass(this, "GBuffer", _brdfLutRenderTarget).SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new DirectionalLightingPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new SpotLightingPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new PointLightingPass(this, "GBuffer").SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new BackgroundPass(this).SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new CopyPass(this, "BaseRenderTarget", "Color").SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new TranslucentConstantAmbientPass(this, "GBuffer").SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new TranslucentPass(this, "GBuffer").SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new ToneMappingPass(this, "BackgroundRenderTarget", "Color").SetOutPutRenderTarget("GammaOutput"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new GammaCorrectionPass(this, "GammaOutput", "Color").SetOutPutRenderTarget("BackgroundRenderTarget"), RenderPassGroup.EveryCamera);

        RegisterRenderPass(new FxaaPass(this, "BackgroundRenderTarget", "Color"), RenderPassGroup.EveryCamera);

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
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(TextureFormat.DepthComponent16);

        DefaultBaseColor = Resources.Texture.CreateFromColor(Color.White);


        DefaultNormal = Resources.Texture.CreateFromColor(Color.FromArgb(128, 128, 255));


        DefaultMetallicRoughness = Resources.Texture.CreateFromColor(Color.FromArgb(0, 127, 0));


        DefaultEmissive = Resources.Texture.CreateFromColor(Color.Black);

        DefaultOcclusion = Resources.Texture.CreateFromColor(Color.White);

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

    public override void Setup()
    {
        if (gl == null)
            return;
        DefaultBaseColor.Upload(gl);
        DefaultNormal.Upload(gl);
        DefaultMetallicRoughness.Upload(gl);
        DefaultEmissive.Upload(gl);
        DefaultOcclusion.Upload(gl);
    }

}
