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

        ShaderName = nameof(SpotLightingPass);
    }
    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);
    }

    public override void Render(Camera camera)
    {
        var size = new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height);
        var rt = GetRenderTarget(GbufferRenderTargetName, size);

        var gBufferBaseColor = rt.GetTexture("BaseColor");
        var gBufferNormalRoughness = rt.GetTexture("NormalRoughness");
        var gBufferMetallicEmissive = rt.GetTexture("MetallicEmissive");
        var depthTexture = rt.DepthStencilTexture;

        foreach (var sl in renderPipeline.SpotLights)
        {
            if (sl.Enable == false)
                continue;

            if (sl.CastShadow == false)
                UseShader("ENABLE_SPOT_LIGHT", "ENBALE_DEFERRED_SHADING");
            else
                UseShader("ENABLE_SPOT_LIGHT", "ENABLE_SHADOWS", "ENBALE_DEFERRED_SHADING");
            UseShader_Internal(null);

            ClearTextureUnit();
            UniformTexture(nameof(gBufferBaseColor), gBufferBaseColor);
            UniformTexture(nameof(gBufferNormalRoughness), gBufferNormalRoughness);
            UniformTexture(nameof(gBufferMetallicEmissive), gBufferMetallicEmissive);
            UniformTexture(nameof(depthTexture), depthTexture);

            UniformVector3("viewPos", camera.WorldTransform.Translation);
            UniformMatrix4("invProjection", camera.Projection.Inverse());
            UniformMatrix4("invView", camera.View.Inverse());

            UniformVector3("spotLightPosition", sl.WorldTransform.Translation);
            UniformVector3("spotLightDirection", sl.Forward);
            UniformColor("spotLightColor", sl.LightColor);
            UniformFloat("spotLightIntensity", 1.0f);
            UniformFloat("spotLightCutOff", MathF.Cos(sl.InnerConeAngleDegree.DegreeToRadians()));
            UniformFloat("spotLightOuterCutOff", MathF.Cos(sl.OuterAngleDegree.DegreeToRadians()));
            UniformFloat("radius", sl.AttenuationRadius);
            UniformFloat("softRatio", sl.SoftRatio);

            if (sl.CastShadow)
            {
                var position = sl.WorldTransform.Translation;
                var shadowView = Matrix4x4.CreateLookAt(position, position + sl.WorldTransform.ForwardVector(), sl.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(sl.OuterAngleDegree.DegreeToRadians(), sl.ShadowMapRenderTarget.Width / (float)sl.ShadowMapRenderTarget.Height, sl.ShadowConfig.NearPlane, sl.ShadowConfig.FarPlane);

                UniformTexture($"spotLightshadowMap", sl.ShadowMapRenderTarget.DepthStencilTexture);
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
