using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class SpotLightingPass : RenderPass
{
    string GbufferRenderTargetName;
    public SpotLightingPass(RenderPipeline renderPipeline, string gbufferRendertarget) : base(renderPipeline)
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
        var size = new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height);
        var rt = GetRenderTarget(GbufferRenderTargetName, size);

        var gBufferBaseColorMetalness = rt.GetTexture("BaseColorMetallic");
        var gBufferNormalRoughness = rt.GetTexture("NormalRoughness");
        var gBufferEmissiveOcclusion = rt.GetTexture("EmissiveOcclusion");
        var depthTexture = rt.DepthStencilTexture;

        foreach (var sp in renderPipeline.SpotLights)
        {

            if (sp.CastShadow == false)
                UseShader("ENABLE_SPOT_LIGHT");
            else
                UseShader("ENABLE_SPOT_LIGHT", "ENABLE_SHADOWS");
            UseShader_Internal(null);

            ClearTextureUnit();
            UniformTexture(nameof(gBufferBaseColorMetalness), gBufferBaseColorMetalness);
            UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
            UniformTexture(nameof(gBufferEmissiveOcclusion), gBufferEmissiveOcclusion);
            UniformTexture(nameof(depthTexture), depthTexture);

            UniformVector3("viewPos", camera.WorldTransform.Translation);
            UniformMatrix4("invProjection", camera.Projection.Inverse());
            UniformMatrix4("invView", camera.View.Inverse());

            UniformVector3("spotLightPosition", sp.WorldTransform.Translation);
            UniformVector3("spotLightDirection", sp.Forward);
            UniformColor("spotLightColor", sp.LightColor);
            UniformFloat("spotLightIntensity", 1.0f);
            UniformFloat("spotLightCutOff", MathF.Cos(sp.InnerConeAngleDegree.DegreeToRadians()));
            UniformFloat("spotLightOuterCutOff", MathF.Cos(sp.OuterAngleDegree.DegreeToRadians()));
            UniformFloat("radius", sp.AttenuationRadius);
            UniformFloat("softRatio", sp.SoftRatio);

            if (sp.CastShadow)
            {
                var position = sp.WorldTransform.Translation;
                var shadowView = Matrix4x4.CreateLookAt(position, position + sp.WorldTransform.ForwardVector(), sp.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(sp.OuterAngleDegree.DegreeToRadians(), sp.ShadowMapRenderTarget.Width / (float)sp.ShadowMapRenderTarget.Height, sp.ShadowConfig.NearPlane, sp.ShadowConfig.FarPlane);

                UniformTexture($"spotLightshadowMap", sp.ShadowMapRenderTarget.DepthStencilTexture);
                UniformMatrix4($"spotLightshadowMapMatrix", shadowView * shadowProjection);

            }

            RenderQuad();
        }

    }

    public override void AfterRender(Camera camera)
    {
        base.AfterRender(camera);
    }
}
