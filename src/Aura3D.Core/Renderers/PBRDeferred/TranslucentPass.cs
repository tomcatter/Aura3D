using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class TranslucentPass : RenderPass
{
    Resources.Texture defaultBaseColor;

    Resources.Texture defaultNormal;

    Resources.Texture defaultMetallicRoughness;

    Resources.Texture defaultEmissive;

    Resources.Texture defaultOcclusion;
    string GbufferRenderTargetName;
    public TranslucentPass(RenderPipeline renderPipeline, string gbufferRendertarget) : base(renderPipeline)
    {
        GbufferRenderTargetName = gbufferRendertarget;

        VertexShader = ShaderResource.MeshVert;

        FragmentShader = ShaderResource.pbr_directionallight_lighting_pass_frag;

        defaultBaseColor = Resources.Texture.CreateFromColor(Color.White);


        defaultNormal = Resources.Texture.CreateFromColor(Color.FromArgb(255, 128, 128, 255));


        defaultMetallicRoughness = Resources.Texture.CreateFromColor(Color.FromArgb(255, 0, 127, 0));



        defaultEmissive = Resources.Texture.CreateFromColor(Color.Black);


        defaultOcclusion = Resources.Texture.CreateFromColor(Color.White);


        ShaderName = nameof(TranslucentPass);
    }
    public override void Setup()
    {
        defaultBaseColor.Upload(gl);
        defaultNormal.Upload(gl);
        defaultMetallicRoughness.Upload(gl);
        defaultEmissive.Upload(gl);
        defaultOcclusion.Upload(gl);
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        if (outputRenderTargetName == null)
            throw new Exception();

        var gbuffer = GetRenderTarget(GbufferRenderTargetName,
                new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height));

        gl.FramebufferTexture2D(GLEnum.Framebuffer, gbuffer.DepthTextureFormat.ToGlAttachment(), GLEnum.Texture2D, gbuffer.DepthStencilTexture.TextureId, 0);

        var error = gl.GetError();
        
        gl.Enable(EnableCap.DepthTest);

        gl.DepthMask(false);

        gl.Enable(EnableCap.Blend);

        gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);

        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);

    }
    public override void AfterRender(Camera camera)
    {
        if (outputRenderTargetName == null)
            throw new Exception();
        var outputRt = GetRenderTarget(outputRenderTargetName,
                new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height));

        gl.BindFramebuffer(GLEnum.Framebuffer, outputRt.FrameBufferId);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, outputRt.DepthTextureFormat.ToGlAttachment(), GLEnum.Texture2D, outputRt.DepthStencilTexture.TextureId, 0);

        var error = gl.GetError();

        gl.DepthMask(true);


    }

    public override void Render(Camera camera)
    {

        foreach(var mesh in VisibleMeshesInCamera)
        {
            if (IsMaterialBlendMode(mesh, BlendMode.Translucent))
            {
                RenderTranslucentMesh(mesh, camera.View, camera.Projection);
            }
        }

    }


    public void RenderTranslucentMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        bool isFirstLight = true;

        List<string> defines = [];
        if (mesh.IsSkinnedMesh)
            defines = ["SKINNED_MESH"];
        foreach(var dl in renderPipeline.DirectionalLights)
        {
            if (dl.Enable == false)
                continue;

            UseShader("ENABLE_DIR_LIGHT", "BLENDMODE_TRANSLUCENT");

            if (isFirstLight == true)
            {
                AddDefines("IS_FIRST_LIGHT");
                isFirstLight = false;
            }
            if (mesh.IsSkinnedMesh)
                AddDefines("SKINNED_MESH");
            UseShader_Internal(mesh);

            ClearTextureUnit();
            SetupUpMeshUniforms(mesh, view, projection);

            UniformVector3("viewPos", view.Inverse().Translation);
            UniformVector3("dirLightDirection", dl.Forward);
            UniformColor("dirLightColor", dl.LightColor);
            UniformFloat("dirLightIntensity", dl.Intensity);

            if (dl.CastShadow == true)
            {
                var shadowView = Matrix4x4.CreateLookAt(dl.WorldTransform.Translation, dl.WorldTransform.Translation + dl.WorldTransform.ForwardVector(), dl.WorldTransform.UpVector());
                var shadowProjection = Matrix4x4.CreateOrthographic(dl.ShadowConfig.Width, dl.ShadowConfig.Height, dl.ShadowConfig.NearPlane, dl.ShadowConfig.FarPlane);

                UniformTexture($"dirLightshadowMap", dl.ShadowMapRenderTarget.DepthStencilTexture);
                UniformMatrix4($"dirLightshadowMapMatrix", shadowView * shadowProjection);

            }
            base.RenderMesh(mesh, view, projection);
        }

        Span<Matrix4x4> ShadowViews = stackalloc Matrix4x4[6];

        foreach (var pl in renderPipeline.PointLights)
        {
            if (pl.Enable == false)
                continue;

            UseShader("ENABLE_POINT_LIGHT", "BLENDMODE_TRANSLUCENT");

            if (isFirstLight == true)
            {
                AddDefines("IS_FIRST_LIGHT");
                isFirstLight = false;
            }

            if (mesh.IsSkinnedMesh)
                AddDefines("SKINNED_MESH");
            UseShader_Internal(mesh);

            ClearTextureUnit();
            SetupUpMeshUniforms(mesh, view, projection);

            UniformVector3("viewPos", view.Inverse().Translation);

            UniformVector3("pointLightPosition", pl.WorldTransform.Translation);
            UniformColor("pointLightColor", pl.LightColor);
            UniformFloat("pointLightIntensity", pl.Intensity);
            UniformFloat("radius", pl.AttenuationRadius);
            UniformFloat("softRatio", pl.SoftRatio);

            if (pl.CastShadow)
            {
                var position = pl.WorldTransform.Translation;

                ShadowViews[0] = Matrix4x4.CreateLookAt(position, position + new Vector3(1, 0, 0), new Vector3(0, -1, 0));
                ShadowViews[1] = Matrix4x4.CreateLookAt(position, position + new Vector3(-1, 0, 0), new Vector3(0, -1, 0));
                ShadowViews[2] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
                ShadowViews[3] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, -1, 0), new Vector3(0, 0, -1));
                ShadowViews[4] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, 1), new Vector3(0, -1, 0));
                ShadowViews[5] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, -1), new Vector3(0, -1, 0));


                var shadowProjection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), pl.ShadowMapRenderTarget.Width / (float)pl.ShadowMapRenderTarget.Height, pl.ShadowConfig.NearPlane, pl.ShadowConfig.FarPlane);


                UniformTextureCubeMap("pointLightShadowMap", pl.ShadowMapRenderTarget.DepthStencilTexture);
                for (int i = 0; i < 6; i++)
                {
                    UniformMatrix4($"pointShadowMapMatrices[{i}]", ShadowViews[i] * shadowProjection);
                }
            }
            base.RenderMesh(mesh, view, projection);
        }

        foreach(var sl in renderPipeline.SpotLights)
        {
            if (sl.Enable == false)
                continue;

            UseShader("ENABLE_SPOT_LIGHT", "BLENDMODE_TRANSLUCENT");

            if (isFirstLight == true)
            {
                AddDefines("IS_FIRST_LIGHT");
                isFirstLight = false;
            }

            if (mesh.IsSkinnedMesh)
                AddDefines("SKINNED_MESH");
            UseShader_Internal(mesh);

            ClearTextureUnit();
            SetupUpMeshUniforms(mesh, view, projection);

            UniformVector3("viewPos", view.Inverse().Translation);
            UniformVector3("spotLightPosition", sl.WorldTransform.Translation);
            UniformVector3("spotLightDirection", sl.Forward);
            UniformColor("spotLightColor", sl.LightColor);
            UniformFloat("spotLightIntensity", sl.Intensity);
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

            base.RenderMesh(mesh, view, projection);
        }


    }

    public void SetupUpMeshUniforms(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {

        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);


        {

            var baseColor = mesh.Material?.GetTexture("BaseColor") ?? defaultBaseColor;
            UniformTexture("Texture_BaseColor", baseColor);


            var normal = mesh.Material?.GetTexture("Normal") ?? defaultNormal;
            UniformTexture("Texture_Normal", normal);

            var metallicRoughness = mesh.Material?.GetTexture("MetallicRoughness") ?? defaultMetallicRoughness;
            UniformTexture("Texture_MetallicRoughness", metallicRoughness);


            var occlusion = mesh.Material?.GetTexture("Occlusion") ?? defaultOcclusion;
            UniformTexture("Texture_Occlusion", occlusion);

            var emissive = mesh.Material?.GetTexture("Emissive") ?? defaultEmissive;
            UniformTexture("Texture_Emissive", emissive);
        }


        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);


        if (mesh.IsSkinnedMesh)
        {
            var skeleton = mesh.Skeleton;
            if (mesh.Model.AnimationSampler != null)
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    UniformMatrix4($"BoneMatrices[{i}]", skeleton.Bones[i].InverseWorldMatrix * mesh.Model.AnimationSampler.BonesTransform[i]);
                }
            }
            else
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    UniformMatrix4($"BoneMatrices[{i}]", skeleton.Bones[i].InverseWorldMatrix * skeleton.Bones[i].WorldMatrix);
                }
            }
        }
    }
}
