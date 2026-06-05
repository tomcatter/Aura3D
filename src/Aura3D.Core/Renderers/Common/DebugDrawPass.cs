using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 调试绘制渲染通道，用于渲染方向轴、网格等引擎内置调试可视化元素。
/// 直接渲染 <see cref="DebugDrawData"/>，不经过 Mesh/Material 管线。
/// 支持从场景渲染目标拷贝深度缓冲以实现正确的遮挡关系。
/// 该通道在每帧最后执行。
/// </summary>
public class DebugDrawPass : RenderPass
{
    private readonly string? _depthRenderTargetName;

    /// <summary>
    /// 初始化 <see cref="DebugDrawPass"/> 类的新实例。
    /// </summary>
    /// <param name="renderPipeline">所属的渲染管线。</param>
    /// <param name="depthRenderTargetName">
    /// 深度缓冲源渲染目标名称（如 "BaseRenderTarget"）。
    /// 传入非 null 值时，会将场景深度缓冲拷贝到当前 FBO，
    /// 使网格等调试元素能被场景几何体正确遮挡。
    /// 传入 null 时仅清除深度缓冲。
    /// </param>
    public DebugDrawPass(RenderPipeline renderPipeline, string? depthRenderTargetName = null)
        : base(renderPipeline)
    {
        _depthRenderTargetName = depthRenderTargetName;
        this.VertexShader = ShaderResource.DebugVert;
        this.FragmentShader = ShaderResource.DebugFrag;
        ShaderName = nameof(DebugDrawPass);
    }

    /// <inheritdoc />
    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        var size = new Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height);

        if (_depthRenderTargetName != null)
        {
            // 从场景渲染目标 GPU 端拷贝深度缓冲到当前 FBO
            // glBlitFramebuffer 是 GPU DMA 操作，几乎零开销
            var sourceRT = GetRenderTarget(_depthRenderTargetName, size);
            gl.BindFramebuffer(GLEnum.ReadFramebuffer, sourceRT.FrameBufferId);
            gl.BindFramebuffer(GLEnum.DrawFramebuffer, camera.RenderTarget.FrameBufferId);
            gl.BlitFramebuffer(
                0, 0, (int)sourceRT.Width, (int)sourceRT.Height,
                0, 0, (int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height,
                ClearBufferMask.DepthBufferBit, GLEnum.Nearest);
        }
        else
        {
            // 没有深度源时清除深度缓冲
            gl.Clear(ClearBufferMask.DepthBufferBit);
        }

        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);

        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
    }

    /// <summary>
    /// 执行指定相机的调试绘制渲染。
    /// </summary>
    public override void Render(Camera camera)
    {
        var drawables = renderPipeline.DebugDrawables;
        if (drawables.Count == 0)
            return;

        UseShader();
        UseShader_Internal();

        foreach (var dd in drawables)
        {
            if (!dd.Owner.Enable || dd.VertexCount == 0)
                continue;

            ClearTextureUnit();

            UniformMatrix4("modelMatrix", dd.WorldTransform);
            UniformMatrix4("viewMatrix", camera.View);
            UniformMatrix4("projectionMatrix", camera.Projection);
            UniformVector3("uColor", new Vector3(
                dd.Color.R / 255f,
                dd.Color.G / 255f,
                dd.Color.B / 255f));

            gl.BindVertexArray(dd.Vao);

            if (dd.NoDepthTest)
            {
                gl.Disable(EnableCap.DepthTest);
                gl.DepthMask(false);
            }

            var glPrimitive = dd.PrimitiveType switch
            {
                Resources.PrimitiveType.Points => GLEnum.Points,
                Resources.PrimitiveType.Lines => GLEnum.Lines,
                Resources.PrimitiveType.LineStrip => GLEnum.LineStrip,
                Resources.PrimitiveType.LineLoop => GLEnum.LineLoop,
                Resources.PrimitiveType.TriangleStrip => GLEnum.TriangleStrip,
                Resources.PrimitiveType.TriangleFan => GLEnum.TriangleFan,
                _ => GLEnum.Triangles,
            };

            unsafe
            {
                gl.DrawArrays(glPrimitive, 0, (uint)dd.VertexCount);
            }

            if (dd.NoDepthTest)
            {
                gl.Enable(EnableCap.DepthTest);
                gl.DepthMask(true);
            }
        }
    }

    /// <inheritdoc />
    public override void AfterRender(Camera camera)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.Disable(EnableCap.Blend);
    }
}
