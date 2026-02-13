    using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

public class NoLightPass : RenderPass
{
    Resources.Texture defaultBaseColor;
    public NoLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        this.FragmentShader = ShaderResource.NoLightFrag;
        this.VertexShader = ShaderResource.NoLightVert;
        ShaderName = nameof(NoLightPass);
    }

    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest); 

        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        defaultBaseColor = Resources.Texture.CreateFromColor(Color.White);

    }

    public override void Setup()
    {
        defaultBaseColor.Upload(gl);
    }
    public override void Render(Camera camera)
    {
        UseShader();
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), camera.View, camera.Projection);

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), camera.View, camera.Projection);

        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), camera.View, camera.Projection);

        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), camera.View, camera.Projection);


        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);

        UseShader("BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);


        UseShader("SKINNED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Translucent), camera.View, camera.Projection);

    }

    public override void AfterRender(Camera camera)
    {
        
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        UniformTexture("BaseColorTexture", mesh.Material?.GetTexture("BaseColor") ?? defaultBaseColor);

        if (mesh.Material != null)
        {

            if (mesh.Material.DoubleSided == false)
            {
                gl.Enable(EnableCap.CullFace);
            }
            else
            {
                gl.Disable(EnableCap.CullFace);
            }

            UniformFloat("alphaCutoff", mesh.Material.AlphaCutoff);
        }
        else
        {
            gl.Enable(EnableCap.CullFace);
            UniformFloat("alphaCutoff", 0.0f);

        }

        if (mesh.IsSkinnedMesh)
        {
            var skinnedMesh = mesh;
            var skeleton = skinnedMesh.Skeleton;
            if (skinnedMesh.Model.AnimationSampler != null)
            {
                for (int i = 0; i < skeleton.Bones.Count; i++)
                {
                    UniformMatrix4($"BoneMatrices[{i}]", skeleton.Bones[i].InverseWorldMatrix * skinnedMesh.Model.AnimationSampler.BonesTransform[i]);
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
