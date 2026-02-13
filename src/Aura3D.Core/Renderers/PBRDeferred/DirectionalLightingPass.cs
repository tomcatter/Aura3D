using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class DirectionalLightingPass : RenderPass
{
    string GbufferRenderTargetName;
    public DirectionalLightingPass(RenderPipeline renderPipeline, string gbufferRendertarget) : base(renderPipeline)
    {
        GbufferRenderTargetName = gbufferRendertarget;

        this.VertexShader = ShaderResource.pbr_directionallight_lighting_pass_vert;

        this.FragmentShader = ShaderResource.pbr_directionallight_lighting_pass_frag;

        ShaderName = nameof(DirectionalLightingPass);
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

        var gBufferBaseColor = rt.GetTexture("BaseColor");
        var gBufferNormalRoughness = rt.GetTexture("NormalRoughness");
        var gBufferMetallicEmissive = rt.GetTexture("MetallicEmissive");
        var depthTexture = rt.DepthStencilTexture;



        foreach (var dl in renderPipeline.DirectionalLights)
        {
            if (dl.Enable == false)
                continue;
            if (dl.CastShadow == false)
                UseShader("ENABLE_DIR_LIGHT", "ENBALE_DEFERRED_SHADING");
            else
                UseShader("ENABLE_DIR_LIGHT", "ENABLE_SHADOWS", "ENBALE_DEFERRED_SHADING");

            UseShader_Internal(null);
            ClearTextureUnit();
            UniformTexture(nameof(gBufferBaseColor), gBufferBaseColor);
            UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
            UniformTexture(nameof(gBufferMetallicEmissive), gBufferMetallicEmissive);
            UniformTexture(nameof(depthTexture), depthTexture);

            UniformVector3("viewPos", camera.WorldTransform.Translation);
            UniformVector3("dirLightDirection", dl.Forward);
            UniformColor("dirLightColor", dl.LightColor);
            UniformFloat("dirLightIntensity", 1.0f);

            UniformMatrix4("invProjection", camera.Projection.Inverse());
            UniformMatrix4("invView", camera.View.Inverse());

            if (dl.CastShadow == true)
            {
                var shadowView = Matrix4x4.CreateLookAt(dl.WorldTransform.Translation, dl.WorldTransform.Translation + dl.WorldTransform.ForwardVector(), dl.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreateOrthographic(dl.ShadowConfig.Width, dl.ShadowConfig.Height, dl.ShadowConfig.NearPlane, dl.ShadowConfig.FarPlane);

                UniformTexture($"dirLightshadowMap", dl.ShadowMapRenderTarget.DepthStencilTexture);
                UniformMatrix4($"dirLightshadowMapMatrix", shadowView * shadowProjection);

            }
            RenderQuad();
        }

    }

    public override void AfterRender(Camera camera)
    {
        base.AfterRender(camera);
    }
}
