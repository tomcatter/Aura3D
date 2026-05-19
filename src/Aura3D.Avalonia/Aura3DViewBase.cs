using Aura3D.Core.Nodes;
using Aura3D.Core.Scenes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.Rendering;
using System.Diagnostics;
using Aura3D.Core.Resources;
using Aura3D.Core.Renderers;
using Aura3D.Core;
using Avalonia.VisualTree;

namespace Aura3D.Avalonia;

/// <summary>
/// Avalonia OpenGL 渲染控件的基类，负责管理场景生命周期、渲染循环以及节点操作。
/// </summary>
public abstract class Aura3DViewBase : global::Avalonia.OpenGL.Controls.OpenGlControlBase, ICustomHitTest
{
    /// <summary>
    /// 获取或设置当前关联的 3D 场景。
    /// </summary>
    public Scene? Scene { get; protected set; }

    Stopwatch Stopwatch;

    int fb = 0;

    public bool AutoRequestNextFrameRendering { get; set; } = true;

    protected bool isSizeChanged = true;
    /// <summary>
    /// 初始化 <see cref="Aura3DViewBase"/> 类的新实例。
    /// </summary>
    public Aura3DViewBase()
    {
        Stopwatch = new Stopwatch();
    }

    /// <summary>
    /// 创建渲染管线的委托，默认使用 <see cref="BlinnPhongPipeline"/>。
    /// </summary>
    public Func<Scene, RenderPipeline> CreateRenderPipeline = scene => new BlinnPhongPipeline(scene);

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        Camera.ControlRenderTarget = controlRenderTarget;

        UpdateControlRenderTargetsSize();

        Scene = new Scene(CreateRenderPipeline);

        Scene.RenderPipeline.Initialize(gl.GetProcAddress);

        Stopwatch.Start();

        UpdateControlRenderTargetsSize();

        OnSceneInitialized();
        Camera.ControlRenderTarget = null;
    }

    private ControlRenderTarget controlRenderTarget = new ControlRenderTarget();
    private void UpdateControlRenderTargetsSize()
    {
        if (isSizeChanged == true)
        {
            var source = this.GetPresentationSource();

            uint width = (uint)Bounds.Width;
            uint height = (uint)Bounds.Height;

            if (source != null)
            {
                width = (uint)(Bounds.Width * source.RenderScaling);
                height = (uint)(Bounds.Height * source.RenderScaling);
            }

            controlRenderTarget.Width = width;
            controlRenderTarget.Height = height;

            isSizeChanged = false;
        }
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (Scene == null)
            return;

        var deltaTime = Stopwatch.Elapsed.TotalSeconds;

        Stopwatch.Restart();

        Scene.RenderPipeline.DefaultFramebuffer = (uint)fb;

        UpdateControlRenderTargetsSize();

        if (this.fb != fb)
        {
            this.fb = fb;
            controlRenderTarget.FrameBufferId = (uint)fb;
        }

        Scene.RenderPipeline.Render();

        Scene.Update(deltaTime);

        Camera.ControlRenderTarget = controlRenderTarget;
        OnSceneUpdated(deltaTime);
        Camera.ControlRenderTarget = null;

        if (AutoRequestNextFrameRendering)
        {
            RequestNextFrameRendering();
        }

    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        base.OnOpenGlDeinit(gl);
        
        if (Scene == null) 
            return;

        Scene?.RenderPipeline.Destroy();

        OnSceneDestroyed();

        Stopwatch.Stop();

    }

    protected abstract void OnSceneInitialized();

    protected abstract void OnSceneDestroyed();

    protected abstract void OnSceneUpdated(double deltaTime);

    /// <summary>
    /// 向场景中添加指定节点。
    /// </summary>
    /// <typeparam name="T">节点类型。</typeparam>
    /// <param name="node">要添加的节点。</param>
    public void AddNode<T>(T node) where T : Node
    {
        Scene?.AddNode(node);
    }

    /// <summary>
    /// 从场景中移除指定节点。
    /// </summary>
    /// <param name="node">要移除的节点。</param>
    public void Remove(Node node)
    {
        Scene?.RemoveNode(node);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        isSizeChanged = true;
    }

    /// <summary>
    /// 对指定点进行命中测试，判断其是否位于控件边界内。
    /// </summary>
    /// <param name="point">要测试的点。</param>
    /// <returns>如果点在边界内，则为 <c>true</c>；否则为 <c>false</c>。</returns>
    public bool HitTest(Point point)
    {
        if (point.X < 0 || point.Y < 0 || point.X > Bounds.Width || point.Y > Bounds.Height)
            return false;
        return true;
    }

    /// <summary>
    /// 获取场景的主相机。
    /// </summary>
    public Camera MainCamera => Scene.MainCamera;
}
