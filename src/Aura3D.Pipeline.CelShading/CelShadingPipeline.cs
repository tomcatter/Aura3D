using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;
using Aura3D.Core;
using Aura3D.Core.Renderers;

namespace Aura3D.Pipeline.CelShading;

public class CelShadingPipeline : RenderPipeline, IRenderPipelineCreateInstance
{

    public CelShadingPipeline(Scene scene) : base(scene)
    {
        var shadowMapPass = new ShadowMapPass(this);
        LightLimitChangedEvent += shadowMapPass.UpdateLightNumLimit;
        RegisterRenderPass(shadowMapPass, RenderPassGroup.Once);


        RegisterRenderPass(new BackgroundPass(this).SetOutPutRenderTarget("BaseRenderTarget"), RenderPassGroup.EveryCamera);

        var basePass = (CelLightPass)new CelLightPass(this).SetOutPutRenderTarget("BaseRenderTarget");
		LightLimitChangedEvent += basePass.UpdateLightNumLimit;
        RegisterRenderPass(basePass, RenderPassGroup.EveryCamera);

        var outlinePass = (OutlinePass)new OutlinePass(this).SetOutPutRenderTarget("BaseRenderTarget");
        RegisterRenderPass(outlinePass, RenderPassGroup.EveryCamera);


        var translucentPass = (CelTranslucentPass)new CelTranslucentPass(this).SetOutPutRenderTarget("BaseRenderTarget");
        RegisterRenderPass(translucentPass, RenderPassGroup.EveryCamera);
		LightLimitChangedEvent += translucentPass.UpdateLightNumLimit;


        RegisterRenderPass(new GammaCorrectionPass(this, "BaseRenderTarget", "Color").SetOutPutRenderTarget("GammaOutput"), RenderPassGroup.EveryCamera);
        RegisterRenderPass(new FxaaPass(this, "GammaOutput", "Color"), RenderPassGroup.EveryCamera);

        // 调试绘制通道（方向轴、网格等），最后渲染以覆盖在所有内容之上
        RegisterRenderPass(new DebugDrawPass(this), RenderPassGroup.EveryCamera);

        RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(TextureFormat.DepthComponent16);

        RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(TextureFormat.DepthComponent16);

        

    }

    public override void BeforeCameraRender(Camera camera)
    {
        if (gl == null)
            return;
        SortMeshes(Meshes, camera);
        gl.Viewport(0, 0, camera.RenderTarget.Width, camera.RenderTarget.Height);

    }


    public override void AfterCameraRender(Camera camera)
    {


    }

    public override void AfterRender()
    {
        
    }


    public override void BeforeRender()
    {

    }

    public static RenderPipeline CreateInstance(Scene scene) => new CelShadingPipeline(scene);
}
