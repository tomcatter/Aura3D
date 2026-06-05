using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 调试绘制渲染通道，使用即时模式（Begin/Vertex/End）直接渲染
/// 坐标轴、网格等引擎内置调试可视化元素。
/// 渲染到自有 RenderTarget（带颜色+深度附件），支持从场景渲染目标拷贝深度缓冲以实现正确的遮挡关系，
/// 最后将结果写回相机 FBO。
/// 该通道在每帧最后执行。
/// </summary>
public class DebugDrawPass : RenderPass
{
    private const string DebugOutputName = "DebugOutput";
    private readonly string? _depthRenderTargetName;

    /// <summary>
    /// 初始化 <see cref="DebugDrawPass"/> 类的新实例。
    /// </summary>
    /// <param name="renderPipeline">所属的渲染管线。</param>
    /// <param name="depthRenderTargetName">
    /// 深度缓冲源渲染目标名称（如 "BaseRenderTarget"）。
    /// 传入非 null 值时，会将场景深度缓冲拷贝到调试 RenderTarget，
    /// 使网格等调试元素能被场景几何体正确遮挡。
    /// 传入 null 时仅清除深度缓冲。
    /// </param>
    public DebugDrawPass(RenderPipeline renderPipeline, string? depthRenderTargetName = null)
        : base(renderPipeline)
    {
        _depthRenderTargetName = depthRenderTargetName;

        renderPipeline.RegisterRenderTarget(DebugOutputName)
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(renderPipeline.Settings.DepthFormat);

        SetOutPutRenderTarget(DebugOutputName);

        this.VertexShader = ShaderResource.DebugVert;
        this.FragmentShader = ShaderResource.DebugFrag;
        ShaderName = nameof(DebugDrawPass);
    }

    /// <inheritdoc />
    public override void BeforeRender(Camera camera)
    {
        int w = (int)camera.RenderTarget.Width;
        int h = (int)camera.RenderTarget.Height;

        // 绑定调试 RenderTarget（带深度附件）
        BindOutPutRenderTarget(camera);
        var debugRT = GetRenderTarget(DebugOutputName, new Size(w, h));

        // 1. 拷贝相机 FBO 颜色到调试 RenderTarget（保留场景画面）
        gl.BindFramebuffer(GLEnum.ReadFramebuffer, camera.RenderTarget.FrameBufferId);
        gl.BindFramebuffer(GLEnum.DrawFramebuffer, debugRT.FrameBufferId);
        gl.BlitFramebuffer(0, 0, w, h, 0, 0, w, h,
            ClearBufferMask.ColorBufferBit, GLEnum.Nearest);

        // 2. 拷贝场景深度到调试 RenderTarget
        if (_depthRenderTargetName != null)
        {
            var size = new Size(w, h);
            var sourceRT = GetRenderTarget(_depthRenderTargetName, size);
            gl.BindFramebuffer(GLEnum.ReadFramebuffer, sourceRT.FrameBufferId);
            gl.BindFramebuffer(GLEnum.DrawFramebuffer, debugRT.FrameBufferId);
            gl.BlitFramebuffer(0, 0, w, h, 0, 0, w, h,
                ClearBufferMask.DepthBufferBit, GLEnum.Nearest);
        }
        else
        {
            gl.BindFramebuffer(GLEnum.Framebuffer, debugRT.FrameBufferId);
            gl.Clear(ClearBufferMask.DepthBufferBit);
        }

        // 3. 重新绑定调试 RenderTarget（blit 可能改变了绑定）
        gl.BindFramebuffer(GLEnum.Framebuffer, debugRT.FrameBufferId);

        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Lequal);

        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
    }

    /// <summary>
    /// 执行指定相机的调试绘制渲染。
    /// </summary>
    public override void Render(Camera camera)
    {
        UseShader();
        UseShader_Internal();

        UniformMatrix4("viewMatrix", camera.View);
        UniformMatrix4("projectionMatrix", camera.Projection);

        var axisGizmo = Scene.AxisGizmo;
        if (axisGizmo != null && axisGizmo.Enable)
        {
            DrawAxisGizmo(axisGizmo);
        }

        var grid = Scene.Grid;
        if (grid != null && grid.Enable)
        {
            DrawGrid(grid);
        }

        if (renderPipeline.Settings.ShowBoundingBox)
        {
            DrawBoundingBoxes();
        }
    }

    private void DrawAxisGizmo(AxisGizmo axis)
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);
        gl.Disable(EnableCap.DepthTest);
        gl.DepthMask(false);

        DrawAxisLine(new Vector3(1, 0, 0), axis.AxisLength, axis.ArrowheadSize, System.Drawing.Color.Red);
        DrawAxisLine(new Vector3(0, 1, 0), axis.AxisLength, axis.ArrowheadSize, System.Drawing.Color.Green);
        DrawAxisLine(new Vector3(0, 0, 1), axis.AxisLength, axis.ArrowheadSize, System.Drawing.Color.Blue);

        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
    }

    private void DrawAxisLine(Vector3 direction, float length, float arrowheadSize, System.Drawing.Color color)
    {
        UniformVector3("uColor", new Vector3(color.R / 255f, color.G / 255f, color.B / 255f));

        var dir = Vector3.Normalize(direction);
        var tip = dir * length;

        Begin();
        Line(Vector3.Zero, tip);

        var (perp1, perp2) = GetPlaneBasis(dir);
        var basePt = tip - dir * arrowheadSize;
        float w = arrowheadSize * 0.35f;

        Line(tip, basePt + perp1 * w);
        Line(tip, basePt - perp1 * w);
        Line(tip, basePt + perp2 * w);
        Line(tip, basePt - perp2 * w);

        End();
    }

    private void DrawGrid(Grid grid)
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);

        float halfSize = grid.Size * 0.5f;
        float step = grid.Size / System.Math.Max(1, grid.Divisions);

        UniformVector3("uColor", new Vector3(
            grid.LineColor.R / 255f, grid.LineColor.G / 255f, grid.LineColor.B / 255f));

        Begin();
        for (int i = 0; i <= grid.Divisions; i++)
        {
            float t = -halfSize + i * step;

            if (MathF.Abs(t) < step * 0.01f)
                continue;

            Line(t, 0, -halfSize, t, 0, halfSize);
            Line(-halfSize, 0, t, halfSize, 0, t);
        }
        End();

        UniformVector3("uColor", new Vector3(
            grid.CenterLineColor.R / 255f, grid.CenterLineColor.G / 255f, grid.CenterLineColor.B / 255f));

        Begin();
        Line(-halfSize, 0, 0, halfSize, 0, 0);
        Line(0, 0, -halfSize, 0, 0, halfSize);
        End();
    }

    private void DrawBoundingBoxes()
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);
        UniformVector3("uColor", new Vector3(0.25f, 1.0f, 0.25f));

        Begin();
        foreach (var mesh in Meshes)
        {
            if (!mesh.Enable)
                continue;
            var bb = mesh.BoundingBox;
            if (bb == null)
                continue;
            WireBox(bb.Min, bb.Max);
        }

        foreach (var im in renderPipeline.InstancedMeshes)
        {
            if (!im.Enable)
                continue;
            var bb = im.WorldBoundingBox;
            if (bb == null)
                continue;
            WireBox(bb.Min, bb.Max);
        }
        End();
    }

    /// <inheritdoc />
    public override void AfterRender(Camera camera)
    {
        int w = (int)camera.RenderTarget.Width;
        int h = (int)camera.RenderTarget.Height;

        var debugRT = GetRenderTarget(DebugOutputName, new Size(w, h));

        // 将调试 RenderTarget 颜色拷贝回相机 FBO
        gl.BindFramebuffer(GLEnum.ReadFramebuffer, debugRT.FrameBufferId);
        gl.BindFramebuffer(GLEnum.DrawFramebuffer, camera.RenderTarget.FrameBufferId);
        gl.BlitFramebuffer(0, 0, w, h, 0, 0, w, h,
            ClearBufferMask.ColorBufferBit, GLEnum.Nearest);

        // 恢复状态
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.Disable(EnableCap.Blend);
    }
}
