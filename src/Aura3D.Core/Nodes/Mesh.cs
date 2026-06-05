using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 网格节点，包含几何体与材质信息，支持静态网格与骨骼网格。
/// </summary>
public class Mesh : Node, IOctreeObject
{
    private Material? material;

    /// <summary>
    /// 获取或设置网格的材质。
    /// </summary>
    public Material? Material 
    { 
        get => material;
        set
        {
            if (value != null && CurrentScene != null)
            {
                foreach (var channel in value.Channels)
                {
                    if (channel.Texture != null && channel.Texture is IGpuResource gpuResource)
                    {
                        CurrentScene.RenderPipeline.AddGpuResource(gpuResource);
                    }
                }
            }
            material = value;
        }
    }

    private Geometry? geometry;

    private BoundingBox? boundingBox;

    /// <summary>
    /// 获取一个值，指示该网格是否为骨骼网格。
    /// </summary>
    [MemberNotNullWhen(returnValue: true, nameof(Model), nameof(Skeleton))]
    public bool IsSkinnedMesh => Model != null && Model.Skeleton != null;

    /// <summary>
    /// 获取一个值，指示该网格是否为静态网格。
    /// </summary>
    [MemberNotNullWhen(returnValue: false, nameof(Model), nameof(Skeleton))]
    public bool IsStaticMesh => !IsSkinnedMesh;

    /// <summary>
    /// 世界空间中的边界框（纯读取，构建由 Geometry/Transform 变更时触发）。
    /// </summary>
    public BoundingBox? BoundingBox => boundingBox;

    /// <summary>
    /// 获取或设置网格的几何体数据。
    /// </summary>
    public Geometry? Geometry
    {
        get => geometry;
        set
        {
            if (value == geometry)
                return;

            if (value != null && CurrentScene != null)
            {
                CurrentScene.RenderPipeline.AddGpuResource(value);
            }
            geometry = value;

            UpdateWorldBoundingBox();

            OnBoundingBoxChanged?.Invoke(this);
        }
    }


    /// <summary>
    /// 获取当前网格使用的 GPU 资源列表。
    /// </summary>
    /// <returns>GPU 资源列表。</returns>
    public override List<IGpuResource> GetGpuResources()
    {
        if (Geometry == null)
        {
            return [];
        }
        var list = new List<IGpuResource>()
        {
            Geometry
        };

        if (Material != null)
        {
            foreach(var channel in Material.Channels)
            {
                if (channel.Texture != null && channel.Texture is IGpuResource gpuResource)
                {
                    list.Add(gpuResource);
                }
            }
        }
        return list;
    }

    /// <summary>
    /// 获取或设置该网格所属的模型。
    /// </summary>
    public Model? Model { get; set; }

    /// <summary>
    /// 获取所属节点列表。
    /// </summary>
    public List<object> BelongingNodes => belongingNodes;

    private List<object> belongingNodes = [];

    /// <summary>
    /// 获取局部空间中的边界框，委托给 Geometry 计算并缓存。
    /// </summary>
    public BoundingBox? LocalBoundingBox => Geometry?.BoundingBox;

    /// <summary>
    /// 当边界框发生变化时触发的事件。
    /// </summary>
    public event Action<IOctreeObject>? OnBoundingBoxChanged = delegate { };

    /// <summary>
    /// 更新世界空间中的边界框
    /// </summary>
    public virtual void UpdateWorldBoundingBox()
    {
        if (LocalBoundingBox == null)
        {
            boundingBox = null;
            return;
        }
        boundingBox = LocalBoundingBox.Transform(WorldTransform);
    }

    /// <summary>
    /// 获取骨骼数据。
    /// </summary>
    public Skeleton? Skeleton => Model?.Skeleton;

    /// <summary>
    /// 获取动画采样器。
    /// </summary>
    public IAnimationSampler? AnimationSampler => Model?.AnimationSampler;

    protected override void OnWorldTransformChanged()
    {
        base.OnWorldTransformChanged();
        UpdateWorldBoundingBox();
        OnBoundingBoxChanged?.Invoke(this);
    }


}
