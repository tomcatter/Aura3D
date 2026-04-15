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

    private bool _enableSkeletonBoundingBox = false;

    /// <summary>
    /// 获取或设置一个值，指示是否启用骨骼包围盒。
    /// </summary>
    public bool EnableSkeletonBoundingBox
    {
        get => _enableSkeletonBoundingBox;

        set
        {
            if (_enableSkeletonBoundingBox != value)
            {
                _enableSkeletonBoundingBox = value;
                localBoundingBox = null;
                boundingBox = null;
                OnBoundingBoxChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// 网格的边界框
    /// </summary>
    public BoundingBox? BoundingBox
    {
        get
        {
            if (boundingBox == null)
            {
                UpdateWorldBoundingBox();
            }
            return boundingBox;
        }
    }

    /// <summary>
    /// 获取或设置网格的几何体数据。
    /// </summary>
    public Geometry? Geometry 
    { 
        get => geometry;
        set
        {
            if (value != null && CurrentScene != null)
            {
                CurrentScene.RenderPipeline.AddGpuResource(value);
            }
            geometry = value;

            localBoundingBox = null;

            boundingBox = null;

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
    /// 局部空间中的边界框
    /// </summary>
    private BoundingBox? localBoundingBox;

    /// <summary>
    /// 获取局部空间中的边界框。
    /// </summary>
    public BoundingBox? LocalBoundingBox
    {
        get
        {
            if (localBoundingBox == null)
                initLocalBoundingBox();
            return localBoundingBox;
        }
    }

    /// <summary>
    /// 当边界框发生变化时触发的事件。
    /// </summary>
    public event Action<IOctreeObject>? OnBoundingBoxChanged = delegate { };

    /// <summary>
    /// 更新边界框
    /// </summary>
    private void initLocalBoundingBox()
    {
        if (Geometry == null)
        {
            localBoundingBox = null;
            boundingBox = null;
            return;
        }
        
        if (IsSkinnedMesh == false || EnableSkeletonBoundingBox == false)
        {
            CalcStaticMeshBoundingBox();
        }
        else
        {
            calcSkeletalMeshBoundingBox();
        }
    }
    
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

    private Dictionary<int, BoundingBox> SkeletalMeshBoundingBox = new();

    private List<BoundingBox> SkeletalMeshBoundingBox2 = new();

    protected override void OnWorldTransformChanged()
    {
        base.OnWorldTransformChanged();
        UpdateWorldBoundingBox();
        OnBoundingBoxChanged?.Invoke(this);
    }

    private void CalcStaticMeshBoundingBox()
    {
        if (Geometry == null)
            return;

        // 获取顶点位置数据
        var positionData = Geometry.GetAttributeData(BuildInVertexAttribute.Position);
        if (positionData == null || positionData.Count < 3)
        {
            localBoundingBox = null;
            boundingBox = null;
            return;
        }

        // 将float列表转换为Vector3列表
        var positions = new List<Vector3>();
        for (int i = 0; i < positionData.Count; i += 3)
        {
            if (i + 2 < positionData.Count)
            {
                positions.Add(new Vector3(
                    positionData[i],
                    positionData[i + 1],
                    positionData[i + 2]
                ));
            }
        }

        localBoundingBox = BoundingBox.CreateFromPoints(positions);

        // 骨骼网格体 加大BoundingBox以减少误差
        if (IsSkinnedMesh)
        {
            var size = localBoundingBox.Size;

            var center = localBoundingBox.Center;

            float length = MathF.Max(size.X, MathF.Max(size.Y, size.Z));

            localBoundingBox = new BoundingBox(center - new Vector3(length / 2), center + new Vector3(length / 2));

        }
    }

    private void calcSkeletalMeshBoundingBox()
    {
        if (IsSkinnedMesh == false)
            return;

        SkeletalMeshBoundingBox.Clear();

        Dictionary<int, List<Vector3>> JointPoints = new Dictionary<int, List<Vector3>>();

        var mesh = this;

        if (mesh.Geometry == null)
            return;

        var positions = mesh.Geometry.GetAttributeData(BuildInVertexAttribute.Position);

        var joints = mesh.Geometry.GetAttributeData(BuildInVertexAttribute.Joints_0);

        var weights = mesh.Geometry.GetAttributeData(BuildInVertexAttribute.Weights_0);

        if (positions != null && joints != null && weights != null)
        {
            for (var i = 0; i < positions.Count / 3; i++)
            {
                var position = new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
                for (var j = 0; j < 4; j++)
                {
                    if (weights[i * 4 + j] > 0.3f)
                    {
                        var jointIndex = (int)joints[i * 4 + j];
                        if (JointPoints.TryGetValue(jointIndex, out _) == false)
                            JointPoints[jointIndex] = new();
                        JointPoints[jointIndex].Add(position);
                    }
                }

            }
        }
        foreach (var (index, points) in JointPoints)
        {
            var boundingBox = BoundingBox.CreateFromPoints(points);
            SkeletalMeshBoundingBox.Add(index, boundingBox);

            localBoundingBox = BoundingBox.CreateMerged(SkeletalMeshBoundingBox.Values);
        }
    }

    /// <summary>
    /// 在动画播放过程中计算骨骼网格的边界框。
    /// </summary>
    public void CalcSkeletalMeshBoundingBoxInPlayAnimation()
    {
        if (IsSkinnedMesh == false)
            return;
        if (AnimationSampler == null)
            return;

        SkeletalMeshBoundingBox2.Clear();

        foreach (var (index, boundingBox) in SkeletalMeshBoundingBox)
        {
            if (index < AnimationSampler.BonesTransform.Count)
            {
                SkeletalMeshBoundingBox2.Add(boundingBox.Transform(Skeleton.Bones[index].InverseWorldMatrix * AnimationSampler.BonesTransform[index]));
            }
        }

        localBoundingBox = BoundingBox.CreateMerged(SkeletalMeshBoundingBox2);

        boundingBox = null;
    }
}
