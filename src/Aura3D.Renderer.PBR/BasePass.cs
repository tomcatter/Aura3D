using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class BasePass : RenderPass <PBRDeferredPipeline>
{
    Resources.Texture defaultBaseColor => RenderPipeline.DefaultBaseColor;

    Resources.Texture defaultNormal => RenderPipeline.DefaultNormal;

    Resources.Texture defaultMetallicRoughness => RenderPipeline.DefaultMetallicRoughness;

    Resources.Texture defaultEmissive => RenderPipeline.DefaultEmissive;

    Resources.Texture defaultOcclusion => RenderPipeline.DefaultOcclusion;


    public BasePass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.MeshVert;

        FragmentShader = ShaderResource.DeferredMeshFrag;

        ShaderName = nameof(BasePass);

    }


    public override void BeforeRender(Camera camera)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.Disable(EnableCap.Blend);
        base.BeforeRender(camera);
    }

    public override void Render(Camera camera)
    {
        BindOutPutRenderTarget(camera);
        gl.DepthMask(true);

        gl.ClearDepth(1.0f);
        gl.ClearColor(0, 0, 0, 0);

        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        UseShader();

        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsStaticMesh, camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsStaticMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && mesh.IsSkinnedMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && mesh.IsSkinnedMesh, camera.View, camera.Projection);
    }

    public override void AfterRender(Camera camera)
    {
        base.AfterRender(camera);
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

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

        base.RenderMesh(mesh, view, projection);


    }

}
