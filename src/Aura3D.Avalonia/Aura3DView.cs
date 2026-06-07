using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Scenes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Avalonia;

public class Aura3DView<T> : Aura3DView where T : IRenderPipelineCreateInstance
{ 
    public Aura3DView()
    {
        CreateRenderPipeline = T.CreateInstance;
    }

}
public class Aura3DView : Aura3DViewBase
{
    public UpdateRoutedEventArgs? updateRoutedEventArgs;

    public Aura3DView()
    {
    }


    public static readonly RoutedEvent<InitializedRoutedEventArgs> SceneInitializedEvent =
      RoutedEvent.Register<Aura3DView, InitializedRoutedEventArgs>(nameof(SceneInitialized), RoutingStrategies.Direct);

    public event EventHandler<InitializedRoutedEventArgs> SceneInitialized
    {
        add => AddHandler(SceneInitializedEvent, value);
        remove => RemoveHandler(SceneInitializedEvent, value);
    }

    public static readonly RoutedEvent<DestroyedRoutedEventArgs> SceneDestroyedEvent =
     RoutedEvent.Register<Aura3DView, DestroyedRoutedEventArgs>(nameof(SceneDestroyed), RoutingStrategies.Direct);


    public event EventHandler<DestroyedRoutedEventArgs> SceneDestroyed
    {
        add => AddHandler(SceneDestroyedEvent, value);
        remove => RemoveHandler(SceneDestroyedEvent, value);
    }

    public static readonly RoutedEvent<UpdateRoutedEventArgs> OnSceneUpdatedEvent =
     RoutedEvent.Register<Aura3DView, UpdateRoutedEventArgs>(nameof(SceneUpdated), RoutingStrategies.Direct);

    public event EventHandler<UpdateRoutedEventArgs> SceneUpdated
    {
        add => AddHandler(OnSceneUpdatedEvent, value);
        remove => RemoveHandler(OnSceneUpdatedEvent, value);
    }
    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
    }

    protected override void OnSceneInitialized()
    {
        updateRoutedEventArgs = new UpdateRoutedEventArgs(OnSceneUpdatedEvent, Scene!);
        RoutedEventArgs args = new InitializedRoutedEventArgs(SceneInitializedEvent, Scene!);
        RaiseEvent(args);
    }

    protected override void OnSceneDestroyed()
    {
        RoutedEventArgs args = new DestroyedRoutedEventArgs(SceneDestroyedEvent, Scene!);
        RaiseEvent(args);
    }

    protected override void OnSceneUpdated(double deltaTime)
    {
        updateRoutedEventArgs!.DeltaTime = deltaTime;
        RaiseEvent(updateRoutedEventArgs);
    }
}


public class UpdateRoutedEventArgs : RoutedEventArgs
{
    public Scene Scene { get; set; }
    public double DeltaTime { get; set; }
    public UpdateRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}

public class InitializedRoutedEventArgs : RoutedEventArgs
{
    public Scene Scene { get; set; }
    public InitializedRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}
public class DestroyedRoutedEventArgs : RoutedEventArgs
{
    public Scene Scene { get; set; }
    public DestroyedRoutedEventArgs(RoutedEvent routedEvent, Scene scene) : base(routedEvent)
    {
        Scene = scene;
    }
}

/// <summary>
/// 物体拾取事件参数，包含被拾取命中的节点信息。
/// </summary>
public class ObjectPickedEventArgs : EventArgs
{
    /// <summary>
    /// 拾取命中的结果。
    /// </summary>
    public PickResult PickResult { get; }

    /// <summary>
    /// 被拾取到的节点（Mesh、Model 或 InstancedMesh）。
    /// </summary>
    public Node Node => PickResult.Node;

    /// <summary>
    /// 命中点在世界空间中的坐标。
    /// </summary>
    public Vector3 WorldPosition => PickResult.WorldPosition;

    /// <summary>
    /// 从射线原点到命中点的距离。
    /// </summary>
    public float Distance => PickResult.Distance;

    /// <summary>
    /// 如果是 InstancedMesh 实例被命中，则为实例索引。
    /// </summary>
    public int? InstanceIndex => PickResult.InstanceIndex;

    /// <summary>
    /// 初始化 <see cref="ObjectPickedEventArgs"/> 类的新实例。
    /// </summary>
    /// <param name="pickResult">拾取结果。</param>
    public ObjectPickedEventArgs(PickResult pickResult)
    {
        PickResult = pickResult;
    }
}