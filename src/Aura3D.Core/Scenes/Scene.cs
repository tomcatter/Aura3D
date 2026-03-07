using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using OneOf;
using System.Drawing;

namespace Aura3D.Core.Scenes;

public class Scene
{
    public IReadOnlySet<Node> Nodes => _nodes;

    private readonly HashSet<Node> _nodes = [];

    private readonly HashSet<Node> _dirtyNodes = [];

    public Camera MainCamera { get; private set; }

    public Octree<Mesh> StaticMeshOctree { get; set; }

    public RenderPipeline RenderPipeline { get; set; }

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

    public Scene(Func<Scene, RenderPipeline> createRenderPipeline)
    {
        RenderPipeline = createRenderPipeline(this);

        StaticMeshOctree = new Octree<Mesh>(new System.Numerics.Vector3(100, 100, 100), 5);

        MainCamera = new Camera();

        Background = Texture.CreateFromColor(Color.AliceBlue);

        AddNode(MainCamera);
    }

    public HashSet<ControlRenderTarget> ControlRenderTargets { get; } = new HashSet<ControlRenderTarget>();

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
            otreeObject.OnBoudingBoxChanged += OnBoudingBoxChanged;
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
            otreeObject.OnBoudingBoxChanged -= OnBoudingBoxChanged;
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

    public void AddNodeTransformDirty(Node node)
    {
        if (_nodes.Contains(node) == false)
            return;
        if (_dirtyNodes.Contains(node) == true)
            return;
        _dirtyNodes.Add(node);
    }
    void OnBoudingBoxChanged(IOctreeObject otreeObject)
    {
        if (otreeObject is not Node node)
            return;
        AddNodeTransformDirty(node);
    }
    public void Update(double deltaTime)
    {
      
        foreach(var node in Nodes)
        {
            node.Update(deltaTime);
            if (node is Mesh mesh)
            {
                if (mesh.IsSkinnedMesh && mesh.AnimationSampler != null && mesh.EnableSkeletonBoudingBox == true)
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
