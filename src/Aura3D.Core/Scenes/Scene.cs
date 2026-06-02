using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using OneOf;
using System.Drawing;

namespace Aura3D.Core.Scenes;

/// <summary>
/// 场景类，负责管理场景中的所有节点、渲染管线、相机以及空间索引结构。
/// </summary>
public class Scene
{
    /// <summary>
    /// 获取场景中的所有节点集合。
    /// </summary>
    public IReadOnlySet<Node> Nodes => _nodes;

    private readonly HashSet<Node> _nodes = [];

    private readonly HashSet<Node> _dirtyNodes = [];

    /// <summary>
    /// 获取场景的主相机。
    /// </summary>
    public Camera MainCamera { get; private set; }

    /// <summary>
    /// 获取或设置场景的静态网格八叉树空间索引。
    /// </summary>
    public Octree<Mesh> StaticMeshOctree { get; set; }

    /// <summary>
    /// 获取或设置场景的渲染管线。
    /// </summary>
    public RenderPipeline RenderPipeline { get; set; }

    /// <summary>
    /// 获取或设置场景的背景，可以是立方体贴图或普通纹理。
    /// </summary>
    public OneOf<CubeTexture, Texture> Background
    {
        get => _background;
        set
        {
            var oldValue = _background;

            if (oldValue.IsT0 && oldValue.AsT0 != null)
            {
                this.RenderPipeline.RemoveGpuResource(oldValue.AsT0);
            }
            else if (oldValue.IsT1 && oldValue.AsT1 != null)
            {
                this.RenderPipeline.RemoveGpuResource(oldValue.AsT1);
            }

            _background = value;

            if (value.IsT0 && value.AsT0 != null)
            {
                this.RenderPipeline.AddGpuResource(value.AsT0);
            }
            else if (value.IsT1 && value.AsT1 != null)
            {
                this.RenderPipeline.AddGpuResource(value.AsT1);
            }
        }
    }

    private OneOf<CubeTexture, Texture> _background;

    /// <summary>
    /// 初始化 <see cref="Scene"/> 类的新实例。
    /// </summary>
    /// <param name="createRenderPipeline">用于创建渲染管线的委托函数。</param>
    public Scene(Func<Scene, RenderPipeline> createRenderPipeline)
    {
        RenderPipeline = createRenderPipeline(this);

        StaticMeshOctree = new Octree<Mesh>(new System.Numerics.Vector3(100, 100, 100), 5);

        MainCamera = new Camera();

        Background = Texture.CreateFromColor(Color.AliceBlue);

        AddNode(MainCamera);
    }

    /// <summary>
    /// 获取场景中所有控制渲染目标的集合。
    /// </summary>
    public HashSet<ControlRenderTarget> ControlRenderTargets { get; } = new HashSet<ControlRenderTarget>();

    /// <summary>
    /// 将节点添加到场景中，并递归添加其所有子节点。
    /// </summary>
    /// <param name="node">要添加的节点。</param>
    /// <exception cref="InvalidOperationException">当节点已添加到场景或已存在时抛出。</exception>
    public void AddNode(Node node)
    {
        if (node.CurrentScene != null)
            throw new InvalidOperationException("Node already add to scene");

        if (Nodes.Contains(node))
            throw new InvalidOperationException("Node already exits");

        _nodes.Add(node);

        node.CurrentScene = this;

        RenderPipeline.AddNode(node);

        if (node is IOctreeObject otreeObject)
        {
            otreeObject.OnBoundingBoxChanged += OnBoundingBoxChanged;
        }

        if (node is Mesh mesh)
        {
            StaticMeshOctree.Add(mesh);
        }

        foreach (var child in node.Children)
        {
            AddNode(child);
        }
    }

    /// <summary>
    /// 从场景中移除节点，并递归移除其所有子节点。
    /// </summary>
    /// <param name="node">要移除的节点。</param>
    /// <exception cref="InvalidOperationException">当节点未附加到场景或不存在于当前场景时抛出。</exception>
    public void RemoveNode(Node node)
    {
        if (node.CurrentScene == null)
            throw new InvalidOperationException("Node is not attached to any scene.");

        if (Nodes.Contains(node) == false)
            throw new InvalidOperationException("Node does not exist in this scene.");

        _nodes.Remove(node);

        node.CurrentScene = null;

        RenderPipeline.RemoveNode(node);


        if (node is Camera camera)
        {
            if (camera.RenderTarget != null && camera.RenderTarget is ControlRenderTarget controlRenderTarget)
            {
                ControlRenderTargets.Remove(controlRenderTarget);
            }
        }

        if (node is IOctreeObject otreeObject)
        {
            otreeObject.OnBoundingBoxChanged -= OnBoundingBoxChanged;
        }


        if (node is Mesh mesh)
        {
            StaticMeshOctree.Remove(mesh);
        }

        foreach (var child in node.Children)
        {
            RemoveNode(child);
        }
        node.ClearPipelineGpuResources();
    }

    /// <summary>
    /// 将变换发生变化的节点标记为脏节点，以便后续更新其空间索引。
    /// </summary>
    /// <param name="node">变换发生变化的节点。</param>
    public void AddNodeTransformDirty(Node node)
    {
        if (_nodes.Contains(node) == false)
            return;
        if (_dirtyNodes.Contains(node) == true)
            return;
        _dirtyNodes.Add(node);
    }

    /// <summary>
    /// 处理包围盒变化事件的回调方法。
    /// </summary>
    /// <param name="otreeObject">包围盒发生变化的八叉树对象。</param>
    private void OnBoundingBoxChanged(IOctreeObject otreeObject)
    {
        if (otreeObject is not Node node)
            return;
        AddNodeTransformDirty(node);
    }

    /// <summary>
    /// 更新场景中的所有节点，并处理脏节点的空间索引更新。
    /// </summary>
    /// <param name="deltaTime">自上一帧以来的时间增量（秒）。</param>
    public void Update(double deltaTime)
    {

        // 快照避免节点 Update 过程中增删子节点导致集合被修改
        var nodesSnapshot = new List<Node>(_nodes);
        foreach(var node in nodesSnapshot)
        {
            if (!_nodes.Contains(node))
                continue;

            node.Update(deltaTime);
            if (node is Mesh mesh)
            {
                if (mesh.IsSkinnedMesh && mesh.AnimationSampler != null && mesh.EnableSkeletonBoundingBox == true)
                {
                    mesh.CalcSkeletalMeshBoundingBoxInPlayAnimation();
                    StaticMeshOctree.Update(mesh);
                }

            }
        }

        foreach (var node in _dirtyNodes)
        {
            if (_nodes.Contains(node) == false)
                continue;
            if (node is Mesh mesh)
            {
                StaticMeshOctree.Update(mesh);
            }
        }
        _dirtyNodes.Clear();
    }
}
