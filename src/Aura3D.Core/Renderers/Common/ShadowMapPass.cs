using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

public class ShadowMapPass : RenderPass
{
    public ShadowMapPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        VertexShader = ShaderResource.ShadowMapVert;
        FragmentShader = ShaderResource.ShadowMapFrag;
        ShaderName = nameof(ShadowMapPass);
    }

    public override void Setup()
    {
        // Setup logic for the shadow map pass
        // This can include setting up shaders, buffers, etc.
    }

    public override void BeforeRender()
    {
        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.CullFace(TriangleFace.Front);
    }
    public override void AfterRender()
    {
        gl.CullFace(TriangleFace.Back);
    }


    public override void Render()
    {
        Span<Matrix4x4> views = stackalloc Matrix4x4[6];

        int index = 0;
        foreach (var pointLight in renderPipeline.PointLights)
        {
            if (pointLight.Enable == false)
                continue;
            if (pointLight.CastShadow == false)
                continue;
            if (index++ >= renderPipeline.Settings.PointLightLimit)
                break;

            var rt = pointLight.GetPipelineGpuResource<CubeRenderTarget>("ShadowMapRenderTarget");

            if (rt == null)
            {
                rt = new CubeRenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(1024, 1024);
            }

            gl.Viewport(0, 0, rt.Width, rt.Height);
            gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);

            var position = pointLight.WorldTransform.Translation;

            views[0] = Matrix4x4.CreateLookAt(position, position + new Vector3(1, 0, 0), new Vector3(0, -1, 0));
            views[1] = Matrix4x4.CreateLookAt(position, position + new Vector3(-1, 0, 0), new Vector3(0, -1, 0));
            views[2] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            views[3] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, -1, 0), new Vector3(0, 0, -1));
            views[4] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, 1), new Vector3(0, -1, 0));
            views[5] = Matrix4x4.CreateLookAt(position, position + new Vector3(0, 0, -1), new Vector3(0, -1, 0));


            var projection = Matrix4x4.CreatePerspectiveFieldOfView(90f.DegreeToRadians(), rt.Width/(float)rt.Height, pointLight.ShadowConfig.NearPlane, pointLight.ShadowConfig.FarPlane);
           
            for (int i = 0; i < 6; i++)
            {
                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.TextureCubeMapPositiveX + i, rt.DepthStencilTexture.TextureId, 0);


                gl.Clear(ClearBufferMask.DepthBufferBit);
                gl.ClearDepth(1.0f);

                RenderMesh(views[i], projection);

            }

        }

        index = 0;
        foreach (var spotLight in renderPipeline.SpotLights)
        {
            if (spotLight.Enable == false)
                continue;
            if (spotLight.CastShadow == false)
                continue;
            if (index++ >= renderPipeline.Settings.SpotLightLimit)
                break;

            var rt = spotLight.GetPipelineGpuResource<RenderTarget>("ShadowMapRenderTarget");

            if (rt == null)
            {
                rt = new RenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(1024, 1024);
                rt.Upload(gl);
                spotLight.SetPipelineGpuResource("ShadowMapRenderTarget", rt);
            }

            gl.Viewport(0, 0, rt.Width, rt.Height);
            gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);

            gl.Clear(ClearBufferMask.DepthBufferBit);
            gl.ClearDepth(1.0f);

            var position = spotLight.WorldTransform.Translation;
            var view = Matrix4x4.CreateLookAt(position, position + spotLight.WorldTransform.ForwardVector(), spotLight.WorldTransform.UpVector());
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(spotLight.OuterAngleDegree.DegreeToRadians(), rt.Width / (float)rt.Height, spotLight.ShadowConfig.NearPlane, spotLight.ShadowConfig.FarPlane);

            RenderMesh(view, projection);
        }

        index = 0;
        foreach (var directionalLight in renderPipeline.DirectionalLights)
        {
            if (directionalLight.Enable == false)
                continue;
            if (directionalLight.CastShadow == false)
                continue;
            if (index++ >= renderPipeline.Settings.DirectionalLightLimit)
                break;

            var rt = directionalLight.GetPipelineGpuResource<RenderTarget>("ShadowMapRenderTarget");

            if (rt == null)
            {
                rt = new RenderTarget().SetDepthTexture(TextureFormat.DepthComponent24).SetSize(1024, 1024);
                rt.Upload(gl);
                directionalLight.SetPipelineGpuResource("ShadowMapRenderTarget", rt);
            }

            gl.Viewport(0, 0, rt.Width, rt.Height);

            gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);
            gl.Clear(ClearBufferMask.DepthBufferBit);
            gl.ClearDepth(1.0f);

            var view = Matrix4x4.CreateLookAt(directionalLight.WorldTransform.Translation, directionalLight.WorldTransform.Translation + directionalLight.WorldTransform.ForwardVector(), directionalLight.WorldTransform.UpVector());
            var projection = Matrix4x4.CreateOrthographic(directionalLight.ShadowConfig.Width , directionalLight.ShadowConfig.Height, directionalLight.ShadowConfig.NearPlane, directionalLight.ShadowConfig.FarPlane);
            
            RenderMesh(view, projection);
        }
    }

    List<Mesh> meshes = new List<Mesh>();
    public void RenderMesh(Matrix4x4 view, Matrix4x4 projection)
    {
        meshes.Clear();

        renderPipeline.UpdateVisibleMeshesInCamera(view, projection, meshes);

        if (meshes.Count == 0)
            return;

        UseShader();
        RenderMeshesFromList(meshes, mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), view, projection);


        UseShader("BLENDMODE_MASKED");
        RenderMeshesFromList(meshes, mesh => mesh.IsStaticMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), view, projection);


        UseShader("SKINNED_MESH");
        RenderMeshesFromList(meshes, mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Opaque), view, projection);


        UseShader("SKINNED_MESH", "BLENDMODE_MASKED");
        RenderMeshesFromList(meshes, mesh => mesh.IsSkinnedMesh && IsMaterialBlendMode(mesh, BlendMode.Masked), view, projection);

        UseShader("INSTANCED_MESH");
        RenderInstancedMeshes(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Opaque), view, projection);

        UseShader("INSTANCED_MESH", "BLENDMODE_MASKED");
        RenderInstancedMeshes(instancedMesh => IsMaterialBlendMode(instancedMesh.Material, BlendMode.Masked), view, projection);
    }

    void setupUniform(Material? material, Matrix4x4 view, Matrix4x4 projection)
    {
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        if (material != null && material.BlendMode == BlendMode.Masked)
        {
            ClearTextureUnit();

            foreach (var channel in material.Channels)
            {
                if (channel.Name == "BaseColor")
                {
                    if (channel.Texture != null)
                    {
                        UniformTexture("BaseColorTexture", channel.Texture);
                    }

                }
            }
            UniformFloat("alphaCutoff", material.AlphaCutoff);
        }
    }

    public override void RenderInstancedMesh(InstancedMesh instancedMesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        setupUniform(instancedMesh.Material, view, projection);

        base.RenderInstancedMesh(instancedMesh, view, projection);
    }
    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        setupUniform(mesh.Material, view, projection);

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
