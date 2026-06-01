using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class DirectionalLightingPass : RenderPass
{
    string gbufferRenderTargetName;
    public DirectionalLightingPass(RenderPipeline renderPipeline, string gbufferRenderTarget) : base(renderPipeline)
    {
        gbufferRenderTargetName = gbufferRenderTarget;

        this.VertexShader = ShaderResource.pbr_directionallight_lighting_pass_vert;

        this.FragmentShader = ShaderResource.pbr_directionallight_lighting_pass_frag;

        ShaderName = nameof(DirectionalLightingPass);
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);
    }

    public override void Render(Camera camera)
    {
        var size = new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height);
        var rt = GetRenderTarget(gbufferRenderTargetName, size);

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

            UseShader_Internal();
            ClearTextureUnit();
            UniformTexture(nameof(gBufferBaseColor), gBufferBaseColor);
            UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
            UniformTexture(nameof(gBufferMetallicEmissive), gBufferMetallicEmissive);
            UniformTexture(nameof(depthTexture), depthTexture);

            UniformVector3("viewPos", camera.WorldTransform.Translation);
            UniformVector3("dirLightDirection", dl.Forward);
            UniformColor("dirLightColor", dl.LightColor);
            UniformFloat("dirLightIntensity", dl.Intensity);

            UniformMatrix4("invProjection", camera.Projection.Inverse());
            UniformMatrix4("invView", camera.View.Inverse());

            var shadowmap = dl.GetPipelineGpuResource<RenderTarget>("ShadowMapRenderTarget");
            if (dl.CastShadow == true && shadowmap != null)
            {
                var shadowView = Matrix4x4.CreateLookAt(dl.WorldTransform.Translation, dl.WorldTransform.Translation + dl.WorldTransform.ForwardVector(), dl.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreateOrthographic(dl.ShadowConfig.Width, dl.ShadowConfig.Height, dl.ShadowConfig.NearPlane, dl.ShadowConfig.FarPlane);

                UniformTexture($"dirLightshadowMap", shadowmap.DepthStencilTexture);
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
