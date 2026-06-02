using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;
using Texture = Aura3D.Core.Resources.Texture;
using Aura3D.Core;
using Aura3D.Core.Renderers;

namespace Aura3D.Pipeline.CelShading;

public enum CelShadingTextureBit
{
    None = 0,
    BaseColorBit = 1,
    NormalBit = 2,
    ILMBit = 4,
    SDFBit = 8,
    ShadowRampBit = 16,
    SpecularRampBit = 32,
}

public class CelLightPass : RenderPass
{

    private int directionalLightLimit;
    private int pointLightLimit;
    private int spotLightLimit;
    private Texture rampTexture;
    public void UpdateLightNumLimit(int directionalLightLimit, int pointLightLimit, int spotLightLimit)
    {
        FragmentShader = ShaderResource.CelFrag
            .Replace("#define MAX_DIRECTIONAL_LIGHTS 4", "#define MAX_DIRECTIONAL_LIGHTS " + directionalLightLimit)
            .Replace("#define MAX_POINT_LIGHTS 4", "#define MAX_POINT_LIGHTS " + pointLightLimit)
            .Replace("#define MAX_SPOT_LIGHTS 4", "#define MAX_SPOT_LIGHTS " + spotLightLimit)
            .Replace("REPEAT_DL_SHADOW_ASSIGN_4//", "REPEAT_DL_SHADOW_ASSIGN_" + directionalLightLimit)
            .Replace("REPEAT_PL_SHADOW_ASSIGN_4//", "REPEAT_PL_SHADOW_ASSIGN_" + pointLightLimit)
            .Replace("REPEAT_SP_SHADOW_ASSIGN_4//", "REPEAT_SP_SHADOW_ASSIGN_" + spotLightLimit);
        foreach (var (key, shader) in Shaders)
        {
            gl.DeleteProgram(shader.ProgramId);
        }
        Shaders.Clear();

        this.directionalLightLimit = directionalLightLimit;
    }
    

    public float AmbientIntensity = 0.1f;

    public CelLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.MeshVert;
        FragmentShader = ShaderResource.CelFrag;
        rampTexture = TextureLoader.LoadTexture(ShaderResource.CelRamp2);
        renderPipeline.AddGpuResource(rampTexture);
    }

    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.CullFace(TriangleFace.Back);


    }

    public override void Render(Camera camera)
    {
		BindOutPutRenderTarget(camera);

        UseShader();
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsStaticMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsSkinnedMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsSkinnedMesh, camera.View, camera.Projection);

    }

    protected void SetupUniform(Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);
        UniformFloat("ambientIntensity", AmbientIntensity);
        UniformVector3("cameraPosition", view.Inverse().Translation);

        UniformTexture("ShadowRamp", rampTexture);

        for(int i = 0; i < directionalLightLimit; i++)
        {
            if (i >= renderPipeline.DirectionalLights.Count)
            {
                UniformVector3($"DirectionalLights[{i}].direction", Vector3.Zero);
                UniformVector3($"DirectionalLights[{i}].color", Vector3.Zero);
                UniformTexture($"DirectionalLightShadowMaps[{i}]", 0);
                UniformMatrix4($"DirectionalLights[{i}].shadowMapMatrix", Matrix4x4.Identity);
            }
            else
            {
                var directionalLight = renderPipeline.DirectionalLights[i];

                UniformVector3($"DirectionalLights[{i}].direction", directionalLight.Forward);
                UniformVector3($"DirectionalLights[{i}].color", new Vector3(directionalLight.LightColor.R / 255f, directionalLight.LightColor.G / 255f, directionalLight.LightColor.B / 255f));

                UniformFloat($"DirectionalLights[{i}].castShadow", directionalLight.CastShadow ? 1.0f : 0.0f);

                var rt = directionalLight.GetPipelineGpuResource<RenderTarget>("ShadowMapRenderTarget");

                if (directionalLight.CastShadow && rt != null)
                {
                    var dlview = Matrix4x4.CreateLookAt(directionalLight.WorldTransform.Translation, directionalLight.WorldTransform.Translation + directionalLight.WorldTransform.ForwardVector(), directionalLight.WorldTransform.UpVector());
                    var dlprojection = Matrix4x4.CreateOrthographic(directionalLight.ShadowConfig.Width, directionalLight.ShadowConfig.Height, directionalLight.ShadowConfig.NearPlane, directionalLight.ShadowConfig.FarPlane);
                   
                    UniformTexture($"DirectionalLightShadowMaps[{i}]", rt.DepthStencilTexture);
                    UniformMatrix4($"DirectionalLights[{i}].shadowMapMatrix", dlview * dlprojection);
                }
                else
                {
                    UniformTexture($"DirectionalLightShadowMaps[{i}]", 0);
                    UniformMatrix4($"DirectionalLights[{i}].shadowMapMatrix", Matrix4x4.Identity);
                }



            }

        }
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        SetupUniform(view, projection);
        //gl.Enable(EnableCap.CullFace);
        //gl.CullFace(TriangleFace.Back);

        // 控制昼夜
        UniformInt("_UseCoolShadowColorOrTex", 1);

        int textureFlags = 0;

        if (mesh.Material != null)
        {
            // 处理卡通渲染扩展参数以及扩展贴图
            //Aura3DCelMaterialExtension matExt = (Aura3DCelMaterialExtension)outVal;
            float tempValue = 0;
            if(mesh.Material.TryGetParameterValue<float>("_RampIndex0", out tempValue))
                UniformFloat("_RampIndex0", tempValue);
            if (mesh.Material.TryGetParameterValue<float>("_RampIndex1", out tempValue))
                UniformFloat("_RampIndex1", tempValue);
            if(mesh.Material.TryGetParameterValue<float>("_RampIndex2", out tempValue))
                UniformFloat("_RampIndex2", tempValue);
            if(mesh.Material.TryGetParameterValue<float>("_RampIndex3", out tempValue))
                UniformFloat("_RampIndex3", tempValue);
            if(mesh.Material.TryGetParameterValue<float>("_RampIndex4", out tempValue))
                UniformFloat("_RampIndex4", tempValue);

            if(mesh.Material.TryGetParameterValue<float>("_BrightFac", out tempValue))
                UniformFloat("_BrightFac", tempValue);
            if(mesh.Material.TryGetParameterValue<float>("_GreyFac", out tempValue))
                UniformFloat("_GreyFac", tempValue);
            if(mesh.Material.TryGetParameterValue<float>("_DarkFac", out tempValue))
                UniformFloat("_DarkFac", tempValue);

            if(mesh.Material.TryGetParameterValue<float>("_FaceShadowOffset", out tempValue))
                UniformFloat("_FaceShadowOffset", tempValue);
            if(mesh.Material.TryGetParameterValue<float>("_BrightAreaShadowFac", out tempValue))
                UniformFloat("_BrightAreaShadowFac", tempValue);
            if(mesh.Material.TryGetParameterValue<float>("_FaceShadowTransitionSoftness", out tempValue))
                UniformFloat("_FaceShadowTransitionSoftness", tempValue);

            Vector4 tempVector4;
            if(mesh.Material.TryGetParameterValue<Vector4>("_LightAreaColorTint", out tempVector4))
                UniformVector4("_LightAreaColorTint", tempVector4);
            if(mesh.Material.TryGetParameterValue<Vector4>("_DarkShadowColor", out tempVector4))
                UniformVector4("_DarkShadowColor", tempVector4);
            if(mesh.Material.TryGetParameterValue<Vector4>("_CoolDarkShadowColor", out tempVector4))
                UniformVector4("_CoolDarkShadowColor", tempVector4);

            foreach (var channel in mesh.Material.Channels)
            {
                switch(channel.Name){
                    case "ILM":
                        if (channel.Texture != null)
                        {
                            UniformTexture("ILMTextures", channel.Texture);
                            textureFlags |= (int)CelShadingTextureBit.ILMBit;
                        }
                        break;
                    case "SDF":
                        if (channel.Texture != null)
                        {
                            UniformTexture("SDFTextures", channel.Texture);
                            textureFlags |= (int)CelShadingTextureBit.SDFBit;
                        }
                        break;
                    case "ShadowRamp":
                        if (channel.Texture != null)
                        {
                            UniformTexture("ShadowRamp", channel.Texture);
                            textureFlags |= (int)CelShadingTextureBit.ShadowRampBit;
                        }
                        break;
                    case "SpecularRamp":
                        if (channel.Texture != null)
                        {
                            UniformTexture("SpecularRamp", channel.Texture);
                            textureFlags |= (int)CelShadingTextureBit.SpecularRampBit;
                        }
                        break;
                    case "BaseColor":
                        if (channel.Texture != null)
                        {
                            UniformTexture("BaseColorTexture", channel.Texture);
                            textureFlags |= (int)CelShadingTextureBit.BaseColorBit;
                        }
                        else
                        {
                            UniformTexture("BaseColorTexture", 0);
                            UniformColor("BaseColor", Color.Red);
                        }
                        break;
                    case "Normal":
                        if (channel.Texture != null)
                        {
                            UniformTexture("NormalTexture", channel.Texture);
                            textureFlags |= (int)CelShadingTextureBit.NormalBit;
                        }
                        else
                        {
                            UniformTexture("NormalTexture", 0);
                        }
                        break;
                }  
            }
            if (mesh.Material.DoubleSided == false)
            {
                gl.Enable(EnableCap.CullFace);
            }
            else
            {
                gl.Disable(EnableCap.CullFace);
            }
            UniformInt("TexturesFlags", textureFlags);
            UniformFloat("alphaCutoff", mesh.Material.AlphaCutoff);
        }

        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);


        if (mesh.IsSkinnedMesh)
        {
            var skinnedMesh = mesh;
            var skeleton = skinnedMesh!.Skeleton!;
            if (skinnedMesh!.Model!.AnimationSampler != null)
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    UniformMatrix4($"BoneMatrices[{i}]", skeleton.Bones[i].InverseWorldMatrix * skinnedMesh!.Model!.AnimationSampler.BonesTransform[i]);
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
        base.RenderMesh(mesh, view, projection);
    }


}
