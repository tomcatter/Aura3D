using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class DirectionalLightingPass : RenderPass
{
    string GbufferRenderTargetName;
    public DirectionalLightingPass(RenderPipeline renderPipeline, string gbufferRendertarget) : base(renderPipeline)
    {
        GbufferRenderTargetName = gbufferRendertarget;

        this.VertexShader = ShaderResource.pbr_directionallight_lighting_pass_vert;

        this.FragmentShader = ShaderResource.pbr_directionallight_lighting_pass_frag;
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        gl.Disable(EnableCap.DepthTest);

        gl.Enable(EnableCap.Blend);

        gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);

        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);

        gl.ClearColor(0, 0, 0, 0);
        gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public override void Render(Camera camera)
    {
        var size = new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height);
        var rt = GetRenderTarget(GbufferRenderTargetName, size);

        var gBufferBaseColorMetalness = rt.GetTexture("BaseColorMetallic");
        var gBufferNormalRoughness = rt.GetTexture("NormalRoughness");
        var gBufferEmissiveOcclusion = rt.GetTexture("EmissiveOcclusion");
        var depthTexture = rt.DepthStencilTexture;


        UseShader("ENABLE_DIR_LIGHT");
        UseShader_Internal(null);

        foreach (var dl in renderPipeline.DirectionalLights)
        {
            ClearTextureUnit();
            UniformTexture(nameof(gBufferBaseColorMetalness), gBufferBaseColorMetalness);
            UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
            UniformTexture(nameof(gBufferEmissiveOcclusion), gBufferEmissiveOcclusion);
            UniformTexture(nameof(depthTexture), depthTexture);

            UniformVector3("viewPos", camera.WorldTransform.Translation);
            UniformVector3("dirLightDirection", dl.Forward);
            UniformColor("dirLightColor", dl.LightColor);
            UniformFloat("dirLightIntensity", 1.0f);

            UniformMatrix4("invProjection", camera.Projection.Inverse());
            UniformMatrix4("invView", camera.View.Inverse());

            RenderQuad();
        }
    }

    public override void AfterRender(Camera camera)
    {
        base.AfterRender(camera);
    }
}
