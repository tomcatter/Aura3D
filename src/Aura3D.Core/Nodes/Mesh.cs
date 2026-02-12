using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Aura3D.Core.Nodes;

public class Mesh : Node, IOctreeObject
{
    private Material? material;
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

    [MemberNotNullWhen(returnValue: true, nameof(Model), nameof(Skeleton))]
    public bool IsSkinnedMesh => Model != null && Model.Skeleton != null;

    [MemberNotNullWhen(returnValue: false, nameof(Model), nameof(Skeleton))]
    public bool IsStaticMesh => !IsSkinnedMesh;

    private bool _enbaleSkeletonBoudingBox = false;
    public bool EnableSkeletonBoudingBox
    {
        get => _enbaleSkeletonBoudingBox;

        set
        {
            if (_enbaleSkeletonBoudingBox != value)
            {
                _enbaleSkeletonBoudingBox = value;
                localBoundingBox = null;
                boundingBox = null;
                OnBoudingBoxChanged?.Invoke(this);
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

            OnBoudingBoxChanged?.Invoke(this);
        }
    }


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

    public Model? Model { get; set; }
    public List<object> BelongingNodes => belongingNodes;

    private List<object> belongingNodes = [];

    /// <summary>
    /// 局部空间中的边界框
    /// </summary>
    private BoundingBox? localBoundingBox;

    public BoundingBox? LocalBoundingBox
    {
        get
        {
            if (localBoundingBox == null)
                initLocalBoundingBox();
            return localBoundingBox;
        }
    }

    public event Action<IOctreeObject>? OnBoudingBoxChanged = delegate { };

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
        
        if (IsSkinnedMesh == false || EnableSkeletonBoudingBox == false)
        {
            calcStaticMeshBoudingBox();
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

    public Skeleton? Skeleton => Model?.Skeleton;

    public IAnimationSampler? AnimationSampler => Model?.AnimationSampler;

    private Dictionary<int, BoundingBox> SkeletalMeshBoudingBox = new ();

    private List<BoundingBox> skeletalMeshBoudingBox2 = new ();

    protected override void OnWorldTransformChanged()
    {
        base.OnWorldTransformChanged();
        UpdateWorldBoundingBox();
        OnBoudingBoxChanged?.Invoke(this);
    }

    private void calcStaticMeshBoudingBox()
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

        // 骨骼网格体 加大BoxBoudingBox以减少误差
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

        SkeletalMeshBoudingBox.Clear();

        Dictionary<int, List<Vector3>> JointPoints = new Dictionary<int, List<Vector3>>();

        var mesh = this;

        if (mesh.Geometry == null)
            return;

        var positions = mesh.Geometry.GetAttributeData(BuildInVertexAttribute.Position);

        var joints = mesh.Geometry.GetAttributeData(BuildInVertexAttribute.Jonits_0);

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
            SkeletalMeshBoudingBox.Add(index, boundingBox);

            localBoundingBox = BoundingBox.CreateMerged(SkeletalMeshBoudingBox.Values);
        }
    }

    public void CalcSkeletalMeshBoundingBoxInPlayAnimation()
    {
        if (IsSkinnedMesh == false)
            return;
        if (AnimationSampler == null)
            return;

        skeletalMeshBoudingBox2.Clear();

        foreach (var (index, boundingBox) in SkeletalMeshBoudingBox)
        {
            if (index < AnimationSampler.BonesTransform.Count)
            {
                skeletalMeshBoudingBox2.Add(boundingBox.Transform(Skeleton.Bones[index].InverseWorldMatrix * AnimationSampler.BonesTransform[index]));
            }
        }

        localBoundingBox = BoundingBox.CreateMerged(skeletalMeshBoudingBox2);

        boundingBox = null;
    }
}
