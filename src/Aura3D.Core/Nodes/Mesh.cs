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
    /// 世界空间中的边界框。
    /// 静态网格：Geometry 顶点 AABB 经 WorldTransform 变换。
    /// 骨骼网格：每骨骼 AABB 经蒙皮矩阵合并后，再经 WorldTransform 变换。
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

            // 几何体变更时，清除骨骼 AABB 缓存（下一帧重新计算）
            _boneBoundsReady = false;
            _boneLocalBounds = null;

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

    // ====================================================
    // 骨骼网格专用：每骨骼局部 AABB
    // ====================================================

    /// <summary>
    /// 每骨骼在模型局部空间中的顶点 AABB（按骨骼索引）。
    /// 索引对应 Skeleton.Bones 的索引，null 表示该骨骼不影响此 Mesh。
    /// 每个 Mesh 独立持有，不与其它共享同一 Skeleton 的 Mesh 冲突。
    /// </summary>
    private BoundingBox?[]? _boneLocalBounds;

    /// <summary>
    /// 骨骼局部 AABB 是否已从几何体计算完毕。
    /// </summary>
    private bool _boneBoundsReady;

    /// <summary>
    /// 从几何体顶点数据计算每骨骼的局部 AABB。
    /// 模型加载后调用一次（可以是静态或骨骼网格，只在 IsSkinnedMesh 时生效）。
    /// </summary>
    public void ComputeBoneLocalBounds()
    {
        if (!IsSkinnedMesh) return;

        var skeleton = Model!.Skeleton!;
        if (skeleton.Bones.Count == 0) return;

        var posData     = Geometry!.GetAttributeData(BuildInVertexAttribute.Position);
        var jointsData  = Geometry!.GetAttributeData(BuildInVertexAttribute.Joints_0);
        var weightsData = Geometry!.GetAttributeData(BuildInVertexAttribute.Weights_0);

        if (posData == null || jointsData == null || weightsData == null) return;

        int boneCount = skeleton.Bones.Count;
        var boneMin = new Vector3?[boneCount];
        var boneMax = new Vector3?[boneCount];

        int vertexCount = posData.Count / 3;
        for (int v = 0; v < vertexCount; v++)
        {
            var pos = new Vector3(posData[v * 3], posData[v * 3 + 1], posData[v * 3 + 2]);

            for (int j = 0; j < 4; j++)
            {
                int wIdx = v * 4 + j;
                if (wIdx >= weightsData.Count) break;

                float weight = weightsData[wIdx];
                if (weight <= 0) continue;

                int jIdx = v * 4 + j;
                if (jIdx >= jointsData.Count) break;

                int boneIdx = (int)jointsData[jIdx];
                if (boneIdx < 0 || boneIdx >= boneCount) continue;

                if (boneMin[boneIdx] == null)
                {
                    boneMin[boneIdx] = pos;
                    boneMax[boneIdx] = pos;
                }
                else
                {
                    boneMin[boneIdx] = Vector3.Min(boneMin[boneIdx]!.Value, pos);
                    boneMax[boneIdx] = Vector3.Max(boneMax[boneIdx]!.Value, pos);
                }
            }
        }

        _boneLocalBounds = new BoundingBox?[boneCount];
        for (int i = 0; i < boneCount; i++)
        {
            if (boneMin[i].HasValue)
                _boneLocalBounds[i] = new BoundingBox(boneMin[i]!.Value, boneMax[i]!.Value);
        }

        _boneBoundsReady = true;
    }

    /// <summary>
    /// 计算局部空间中的 AABB——静态和骨骼网格的统一分发点。
    /// 静态：返回 Geometry.BoundingBox。
    /// 骨骼：合并每骨骼 AABB 经蒙皮矩阵变换后的结果。
    /// 若骨骼数据就绪但 AnimationSampler 尚未设置（如 Clone 过程中），
    /// 回退到 Geometry.BoundingBox。
    /// </summary>
    private BoundingBox? ComputeLocalBoundingBox()
    {
        if (IsSkinnedMesh && _boneBoundsReady && _boneLocalBounds != null
            && Model!.AnimationSampler != null)
        {
            return ComputeAnimatedLocalBoundingBox();
        }
        return Geometry?.BoundingBox;
    }

    /// <summary>
    /// 骨骼路径：对每个骨骼的局部 AABB 应用蒙皮矩阵变换，合并得到动画后的局部 AABB。
    /// 蒙皮矩阵与 GPU 顶点着色器中的 BoneMatrices 一致：
    /// <c>skinMatrix = Bone.InverseWorldMatrix * BonesTransform[i]</c>
    /// </summary>
    private BoundingBox? ComputeAnimatedLocalBoundingBox()
    {
        var skeleton = Model!.Skeleton!;
        var bonesTransform = Model!.AnimationSampler!.BonesTransform;

        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        bool valid = false;

        for (int i = 0; i < _boneLocalBounds!.Length; i++)
        {
            if (_boneLocalBounds[i] == null) continue;

            var skinMat = skeleton.Bones[i].InverseWorldMatrix * bonesTransform[i];
            var boneBB = _boneLocalBounds[i]!.Transform(skinMat);

            if (!valid)
            {
                min = boneBB.Min;
                max = boneBB.Max;
                valid = true;
            }
            else
            {
                min = Vector3.Min(min, boneBB.Min);
                max = Vector3.Max(max, boneBB.Max);
            }
        }

        return valid ? new BoundingBox(min, max) : null;
    }

    // ====================================================

    /// <summary>
    /// 更新世界空间中的边界框。
    /// 静态网格和骨骼网格统一入口：先获取局部 AABB，再经 WorldTransform 变换。
    /// </summary>
    public virtual void UpdateWorldBoundingBox()
    {
        var localBB = ComputeLocalBoundingBox();

        if (localBB == null)
        {
            boundingBox = null;
            return;
        }
        boundingBox = localBB.Transform(WorldTransform);
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
