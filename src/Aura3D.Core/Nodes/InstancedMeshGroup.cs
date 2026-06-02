using Aura3D.Core.Math;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 类似 UE Hierarchical Instanced Static Mesh (HISM) 的层次化实例网格组。
/// 将大量实例按空间位置通过八叉树分组，每组生成一个 <see cref="InstancedMesh"/>，
/// 借助 <see cref="InstancedMesh"/> 自带的视锥体剔除实现高效的按组裁剪。
///
/// 使用方式：
/// <code>
/// var group = new InstancedMeshGroup(sourceMesh);
/// group.SetInstances(transforms);
/// group.Build();
/// scene.AddNode(group);
/// </code>
/// </summary>
public class InstancedMeshGroup : Node
{
    /// <summary>
    /// 初始化实例网格组。
    /// </summary>
    /// <param name="sourceMesh">源网格，所有实例将共享此网格的几何体与材质。</param>
    /// <exception cref="ArgumentNullException">sourceMesh 为 null 时抛出。</exception>
    public InstancedMeshGroup(Mesh sourceMesh)
    {
        SourceMesh = sourceMesh ?? throw new ArgumentNullException(nameof(sourceMesh));
        Name = "InstancedMeshGroup";
    }

    /// <summary>
    /// 获取源网格。
    /// </summary>
    public Mesh SourceMesh { get; }

    /// <summary>
    /// 每个 InstancedMesh 分组最多容纳的实例数。默认 64。
    /// 数值越小分组越细，剔除精度越高但 DrawCall 也越多。
    /// </summary>
    public int MaxInstancesPerGroup { get; set; } = 64;

    /// <summary>
    /// 八叉树最大深度。默认 6。
    /// 防止实例过于密集时无限递归。
    /// </summary>
    public int MaxDepth { get; set; } = 6;

    /// <summary>
    /// 获取当前已创建的分组列表（调用 <see cref="Build"/> 后可用）。
    /// </summary>
    public IReadOnlyList<InstancedMesh> Groups => _groups;

    private readonly List<InstancedMesh> _groups = new();
    private readonly List<Matrix4x4> _transforms = new();
    private readonly List<int> _instanceToGroupMap = new();
    private bool _needsBuild;

    /// <summary>
    /// 获取实例总数。
    /// </summary>
    public int InstanceCount => _transforms.Count;

    /// <summary>
    /// 获取分组数量。
    /// </summary>
    public int GroupCount => _groups.Count;

    /// <summary>
    /// 一次性设置所有实例变换并标记需要重建。
    /// </summary>
    /// <param name="transforms">实例的世界变换矩阵列表。</param>
    public void SetInstances(IReadOnlyList<Matrix4x4> transforms)
    {
        _transforms.Clear();
        _transforms.AddRange(transforms);
        _needsBuild = true;
    }

    /// <summary>
    /// 追加一个实例并标记需要重建。
    /// </summary>
    /// <param name="transform">实例的世界变换矩阵。</param>
    /// <returns>新实例的索引。</returns>
    public int AddInstance(Matrix4x4 transform)
    {
        _transforms.Add(transform);
        _needsBuild = true;
        return _transforms.Count - 1;
    }

    /// <summary>
    /// 批量追加实例并标记需要重建。
    /// </summary>
    /// <param name="transforms">要追加的变换矩阵集合。</param>
    public void AddInstances(IEnumerable<Matrix4x4> transforms)
    {
        _transforms.AddRange(transforms);
        _needsBuild = true;
    }

    /// <summary>
    /// 更新指定索引的实例变换并标记需要重建。
    /// 如果实例移动范围不大，仍在原分组内，可考虑未来做增量更新。
    /// </summary>
    /// <param name="index">实例索引。</param>
    /// <param name="transform">新的世界变换矩阵。</param>
    public void UpdateInstance(int index, Matrix4x4 transform)
    {
        if (index < 0 || index >= _transforms.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _transforms[index] = transform;
        _needsBuild = true;
    }

    /// <summary>
    /// 移除指定索引的实例并标记需要重建。
    /// </summary>
    /// <param name="index">实例索引。</param>
    public void RemoveInstance(int index)
    {
        if (index < 0 || index >= _transforms.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _transforms.RemoveAt(index);
        _needsBuild = true;
    }

    /// <summary>
    /// 清空所有实例。
    /// </summary>
    public void ClearInstances()
    {
        _transforms.Clear();
        _needsBuild = true;
    }

    /// <summary>
    /// 构建空间八叉树并为每个叶子节点创建 <see cref="InstancedMesh"/> 分组。
    /// 会先清除所有旧分组。
    /// </summary>
    public void Build()
    {
        // 清除旧分组
        foreach (var group in _groups)
        {
            if (_children.Contains(group))
                RemoveChild(group, AttachToParentRule.KeepWorld);
        }
        _groups.Clear();
        _instanceToGroupMap.Clear();

        if (_transforms.Count == 0)
        {
            _needsBuild = false;
            return;
        }

        // 计算整体包围盒
        var overallBB = ComputeOverallBoundingBox();
        if (overallBB == null)
        {
            _needsBuild = false;
            return;
        }

        // 构建八叉树根节点
        var rootNode = new InstanceOctreeNode(overallBB);

        // 将所有实例指派到八叉树节点
        for (int i = 0; i < _transforms.Count; i++)
        {
            var position = _transforms[i].Translation;
            rootNode.Insert(i, position, _transforms);
        }

        // 按 MaxInstancesPerGroup / MaxDepth 递归细分
        rootNode.Subdivide(_transforms, MaxInstancesPerGroup, MaxDepth);

        // 收集叶子节点并创建 InstancedMesh
        _instanceToGroupMap.AddRange(new int[_transforms.Count]);
        var leafNodes = new List<InstanceOctreeNode>();
        rootNode.CollectLeaves(leafNodes);

        foreach (var leaf in leafNodes)
        {
            if (leaf.InstanceIndices.Count == 0)
                continue;

            var im = InstancedMesh.FromMesh(SourceMesh);
            im.Name = $"{Name}_Group{_groups.Count}";

            // 按需禁用该分组的视锥体剔除（由 InstancedMeshGroup 统一管理）
            // im.EnableFrustumCulling = true; // 默认已开启

            foreach (var idx in leaf.InstanceIndices)
            {
                im.AddInstance(_transforms[idx]);
                _instanceToGroupMap[idx] = _groups.Count;
            }

            _groups.Add(im);
            AddChild(im, AttachToParentRule.KeepWorld);
        }

        _needsBuild = false;
    }

    /// <summary>
    /// 如果实例发生变化则重建，通常在每帧 Update 中调用。
    /// </summary>
    public void BuildIfNeeded()
    {
        if (_needsBuild)
            Build();
    }

    /// <summary>
    /// 计算所有实例的合并世界包围盒。
    /// </summary>
    private BoundingBox? ComputeOverallBoundingBox()
    {
        var localBB = SourceMesh.LocalBoundingBox;
        if (localBB != null && _transforms.Count > 0)
        {
            // 有局部包围盒时，对每个实例的变换矩阵做 AABB 变换再合并
            var boxes = new List<BoundingBox>(_transforms.Count);
            foreach (var t in _transforms)
            {
                boxes.Add(localBB.Transform(t));
            }
            return BoundingBox.CreateMerged(boxes);
        }

        // 没有局部包围盒时，退化为仅用 transform 位置构建
        return ComputeBoundingBoxFromPositions();
    }

    /// <summary>
    /// 从所有实例位置构建包围盒（无几何体包围盒时的退化方案）。
    /// </summary>
    private BoundingBox? ComputeBoundingBoxFromPositions()
    {
        if (_transforms.Count == 0)
            return null;

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var t in _transforms)
        {
            var pos = t.Translation;
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
        }

        return new BoundingBox(min, max);
    }

    public override void Update(double delta)
    {
        base.Update(delta);
        BuildIfNeeded();
    }

    public override List<Resources.IGpuResource> GetGpuResources()
    {
        var list = new List<Resources.IGpuResource>();
        foreach (var group in _groups)
        {
            list.AddRange(group.GetGpuResources());
        }
        return list;
    }
}

/// <summary>
/// 内部八叉树节点，用于按空间位置对实例进行分组。
/// </summary>
internal class InstanceOctreeNode
{
    /// <summary>
    /// 节点的世界空间包围盒。
    /// </summary>
    public BoundingBox Bounds { get; }

    /// <summary>
    /// 属于此节点的实例索引集合。
    /// </summary>
    public List<int> InstanceIndices { get; } = new();

    /// <summary>
    /// 子节点（8 个或 null 表示叶子节点）。
    /// </summary>
    public InstanceOctreeNode[]? Children { get; private set; }

    /// <summary>
    /// 是否为叶子节点。
    /// </summary>
    public bool IsLeaf => Children == null;

    public InstanceOctreeNode(BoundingBox bounds)
    {
        Bounds = bounds;
    }

    /// <summary>
    /// 将实例插入此节点（不考虑容量，直接追加到当前节点列表）。
    /// </summary>
    /// <param name="instanceIndex">实例在主列表中的索引。</param>
    /// <param name="position">实例的世界空间位置。</param>
    /// <param name="transforms">全部实例的变换列表（仅用于校验）。</param>
    public void Insert(int instanceIndex, Vector3 position, List<Matrix4x4> transforms)
    {
        InstanceIndices.Add(instanceIndex);
    }

    /// <summary>
    /// 递归细分节点，直到每个叶子节点的实例数不超过 maxPerNode 或达到最大深度。
    /// </summary>
    /// <param name="transforms">全部实例的变换列表。</param>
    /// <param name="maxPerNode">每个叶子节点最大实例数。</param>
    /// <param name="maxDepth">最大细分深度。</param>
    /// <param name="currentDepth">当前深度（内部递归使用）。</param>
    public void Subdivide(List<Matrix4x4> transforms, int maxPerNode, int maxDepth, int currentDepth = 0)
    {
        if (InstanceIndices.Count <= maxPerNode || currentDepth >= maxDepth)
            return;

        // 创建 8 个子节点
        var center = Bounds.Center;
        var childSize = Bounds.Size / 2;
        var quarter = childSize / 2;

        Children = new InstanceOctreeNode[8];
        var offsets = new Vector3[]
        {
            new(-1, -1, -1), new( 1, -1, -1),
            new(-1,  1, -1), new(-1, -1,  1),
            new( 1,  1, -1), new( 1, -1,  1),
            new(-1,  1,  1), new( 1,  1,  1),
        };

        for (int i = 0; i < 8; i++)
        {
            var childCenter = center + offsets[i] * quarter;
            Children[i] = new InstanceOctreeNode(new BoundingBox(childCenter - childSize / 2, childCenter + childSize / 2));
        }

        // 将当前节点的实例分配到子节点
        var remaining = new List<int>();
        foreach (var idx in InstanceIndices)
        {
            var pos = transforms[idx].Translation;
            bool assigned = false;

            for (int i = 0; i < 8; i++)
            {
                if (Children[i].Bounds.Contains(pos))
                {
                    Children[i].InstanceIndices.Add(idx);
                    assigned = true;
                    break;
                }
            }

            if (!assigned)
                remaining.Add(idx);
        }

        // 清空当前节点并保存无法分配的子节点（边界情况）
        InstanceIndices.Clear();
        InstanceIndices.AddRange(remaining);

        // 递归细分各子节点
        for (int i = 0; i < 8; i++)
        {
            Children[i].Subdivide(transforms, maxPerNode, maxDepth, currentDepth + 1);
        }

        // 如果所有子节点都为空且当前节点也无实例，保持为叶子
        bool allChildrenEmpty = true;
        for (int i = 0; i < 8; i++)
        {
            if (Children[i].InstanceIndices.Count > 0 || !Children[i].IsLeaf)
            {
                allChildrenEmpty = false;
                break;
            }
        }

        if (allChildrenEmpty && InstanceIndices.Count == 0)
        {
            Children = null;
        }
    }

    /// <summary>
    /// 收集此子树中的所有叶子节点。
    /// </summary>
    /// <param name="leaves">用于存储叶子节点的列表。</param>
    public void CollectLeaves(List<InstanceOctreeNode> leaves)
    {
        if (IsLeaf)
        {
            if (InstanceIndices.Count > 0)
                leaves.Add(this);
        }
        else
        {
            foreach (var child in Children!)
            {
                child.CollectLeaves(leaves);
            }
        }
    }
}
