using Aura3D.Core.Nodes;
using Silk.NET.OpenGLES;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Renderers;

public class TranslucentPass : LightPass
{
    public TranslucentPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        ShaderName = nameof(TranslucentPass);
    }

    public override void BeforeRender(Camera camera)
    {
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.DepthMask(false);
    }

    public override void Render(Camera camera)
    {

        var rt = GetRenderTarget("BaseRenderTarget",
            new System.Drawing.Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height));

        gl.BindFramebuffer(GLEnum.Framebuffer, rt.FrameBufferId);

        UseShader("BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Translucent) && mesh.IsStaticMesh, camera.View, camera.Projection);
        

        UseShader("SKINNED_MESH", "BLENDMODE_TRANSLUCENT");
        RenderVisibleMeshesInCamera(mesh => IsMaterialBlendMode(mesh, BlendMode.Translucent) && mesh.IsSkinnedMesh, camera.View, camera.Projection);
        
   
    }
}
