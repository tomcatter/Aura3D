using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using System.Numerics;
using Aura3D.Core.Resources;
using Aura3D.Core.Math;
using Texture = Aura3D.Core.Resources.Texture;
using System.Reflection;

namespace Aura3D.Core.Renderers;

public class OutlinePass : RenderPass
{
    private float outlineWidth = 2f;
    private Vector4 outlineColor = new Vector4(0.0f, 0.0f, 0.0f, 1f);

    public float AmbientIntensity = 0.1f;

    public OutlinePass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.OutlineVert;
        FragmentShader = ShaderResource.OutlineFrag;
        ShaderName = nameof(OutlinePass);
    }
    public override void Setup()
    {
        // Setup logic for the base pass
        // This can include setting up shaders, buffers, etc.
    }
    public override void BeforeRender(Camera camera)
    {
        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.Enable(EnableCap.CullFace);
        gl.CullFace(TriangleFace.Front);


    }

    public override void Render(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        ClearTextureUnit();

        UseShader();
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), camera.View, camera.Projection);
        

        UseShader("BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), camera.View, camera.Projection);
        

        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), camera.View, camera.Projection);
        

        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderVisibleMeshesInCamera(mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), camera.View, camera.Projection);
    }

    protected void SetupUniform(Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);
        UniformVector3("cameraPosition", view.Inverse().Translation);
        UniformFloat("ambientIntensity", AmbientIntensity);
        UniformFloat("outlineWidth", outlineWidth);
        UniformVector4("BaseColor", outlineColor);
        

    }
    public override void AfterRender(Camera camera)
    {
        gl.Disable(EnableCap.CullFace);
    }

    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();
        SetupUniform(view, projection);
        var normalMatrix = mesh.WorldTransform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        UniformMatrix4("normalMatrix", normalMatrix);

        var normalPrjMatrix = (projection * view * mesh.WorldTransform).Inverse();
        normalPrjMatrix = Matrix4x4.Transpose(normalPrjMatrix);
        UniformMatrix4("normalPrjMatrix", normalPrjMatrix);


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
