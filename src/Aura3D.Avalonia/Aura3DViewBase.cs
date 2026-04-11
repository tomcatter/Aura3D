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

public abstract class Aura3DViewBase : global::Avalonia.OpenGL.Controls.OpenGlControlBase, ICustomHitTest
{
    public Scene? Scene { get; protected set; }

    Stopwatch Stopwatch;

    int fb = 0;

    protected bool isSizeChanged = true;
    public Aura3DViewBase()
    {
        Stopwatch = new Stopwatch();
    }

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


        RequestNextFrameRendering();
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

    public void AddNode<T>(T node) where T : Node
    {
        Scene?.AddNode(node);
    }

    public void Remove(Node node)
    {
        Scene?.RemoveNode(node);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        isSizeChanged = true;
    }

    public bool HitTest(Point point)
    {
        if (point.X < 0 || point.Y < 0 || point.X > Bounds.Width || point.Y > Bounds.Height)
            return false;
        return true;
    }

    public Camera MainCamera => Scene.MainCamera;
}
