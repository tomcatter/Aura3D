using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System.Numerics;

namespace Aura3D.Core.Renderers.PBRDeferred;

internal class BasePass : RenderPass
{

    public BasePass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.MeshVert;
        FragmentShader = ShaderResource.DeferredMeshFrag;
    }

    public override void BeforeRender(Camera camera)
    {
        base.BeforeRender(camera);
    }

    public override void Render(Camera camera)
    {
        UseShader();
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque) && !mesh.IsSkinnedMesh, camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked) && !mesh.IsSkinnedMesh, camera.View, camera.Projection);


        UseShader("SKINNED_MESH");
        RenderSkinnedMeshes(mesh => IsMaterialBlendMode(mesh, BlendMode.Opaque), camera.View, camera.Projection);


        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderSkinnedMeshes(mesh => IsMaterialBlendMode(mesh, BlendMode.Masked), camera.View, camera.Projection);
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

        if (mesh.Material != null)
        {
            foreach(var channel in mesh.Material.Channels)
            {
                if (channel.Texture != null)
                {
                    UniformTexture(channel.Name, channel.Texture);
                }
            }
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
