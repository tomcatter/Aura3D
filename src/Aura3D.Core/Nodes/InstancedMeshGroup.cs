using Aura3D.Core.Math;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 类似 UE Hierarchical Instanced Static Mesh (HISM) 的层次化实例网格组。
/// 将大量实例按空间位置通过八叉树分组，每组生成一个 <see cref="InstancedMesh"/>，
/// 借助 <see cref="InstancedMesh"/> 自带的视锥体剔除实现高效的按组裁剪。
///
/// <para>增量更新：当 <see cref="UpdateInstance"/> 发现实例仍在原空间分组内时，
/// 原地修改 InstancedMesh 的 VBO 数据（标记 NeedsUpload），不触发重建；
/// 仅在实例跨分组移动、增删实例时才回退到全量重建。</para>
///
/// 使用方式：
/// <code>
/// var group = new InstancedMeshGroup(sourceMesh);
/// group.SetInstances(transforms);
/// group.Build();
/// scene.AddNode(group);
///
/// // 每帧更新
/// group.UpdateInstance(5, newTransform);  // 同区域 → O(1) 原地更新
/// group.BuildIfNeeded();                  // 跨区域 / 增删 → 全量重建
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

    /// <summary>
    /// 获取实例总数。
    /// </summary>
    public int InstanceCount => _transforms.Count;

    /// <summary>
    /// 获取分组数量。
    /// </summary>
    public int GroupCount => _groups.Count;

    /// <summary>
    /// 获取自上次 <see cref="Build"/> 以来执行过的原地更新次数。
    /// </summary>
    public int InPlaceUpdateCount { get; private set; }

    /// <summary>
    /// 获取自上次 <see cref="Build"/> 以来触发过的全量重建次数。
    /// </summary>
    public int RebuildCount { get; private set; }

    private readonly List<InstancedMesh> _groups = new();
    private readonly List<Matrix4x4> _transforms = new();
    private readonly List<int> _instanceGroupIndex = new();    // 每个实例属于哪个 group
    private readonly List<int> _instanceIndexInGroup = new();  // 每个实例在其 group 中的 AddInstance 序号
    private InstanceOctreeNode? _rootNode;
    private bool _needsBuild;
    private bool _built;

    // ========================================================================
    // Public API
    // ========================================================================

    /// <summary>
    /// 一次性设置所有实例变换并标记需要重建。
    /// </summary>
    /// <param name="transforms">实例的世界变换矩阵列表。</param>
    public void SetInstances(IReadOnlyList<Matrix4x4> transforms)
    {
        _transforms.Clear();
        _transforms.AddRange(transforms);
        Invalidate();
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
    /// 更新指定索引的实例变换。
    /// 如果实例仍在原空间分组内，仅原地修改 VBO 数据（标记 NeedsUpload），
    /// 不触发全量重建；如果跨分组移动，则回退到全量重建。
    /// </summary>
    /// <param name="index">实例索引。</param>
    /// <param name="transform">新的世界变换矩阵。</param>
    public void UpdateInstance(int index, Matrix4x4 transform)
    {
        if (index < 0 || index >= _transforms.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _transforms[index] = transform;

        // 尝试增量更新
        if (TryIncrementalUpdate(index, transform))
            return;

        // 跨分组或其他情况 → 回退到全量重建
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
        Invalidate();
    }

    /// <summary>
    /// 构建空间八叉树并为每个叶子节点创建 <see cref="InstancedMesh"/> 分组。
    /// 会先清除所有旧分组。
    /// </summary>
    public void Build()
    {
        // 清除旧分组
        DestroyGroups();
        _groups.Clear();
        _instanceGroupIndex.Clear();
        _instanceIndexInGroup.Clear();
        _rootNode = null;
        InPlaceUpdateCount = 0;
        RebuildCount++;

        if (_transforms.Count == 0)
        {
            _needsBuild = false;
            _built = true;
            return;
        }

        // 计算整体包围盒
        var overallBB = ComputeOverallBoundingBox();
        if (overallBB == null)
        {
            _needsBuild = false;
            _built = true;
            return;
        }

        // 构建八叉树
        _rootNode = new InstanceOctreeNode(overallBB, -1);
        for (int i = 0; i < _transforms.Count; i++)
        {
            _rootNode.Insert(i, _transforms[i].Translation);
        }
        _rootNode.Subdivide(_transforms, MaxInstancesPerGroup, MaxDepth);

        // 收集叶子节点并创建 InstancedMesh
        _instanceGroupIndex.AddRange(new int[_transforms.Count]);
        _instanceIndexInGroup.AddRange(new int[_transforms.Count]);
        var leafNodes = new List<InstanceOctreeNode>();
        _rootNode.CollectLeaves(leafNodes);

        foreach (var leaf in leafNodes)
        {
            if (leaf.InstanceIndices.Count == 0)
                continue;

            var groupIdx = _groups.Count;
            leaf.GroupIndex = groupIdx; // 将叶子节点与 group 关联

            var im = InstancedMesh.FromMesh(SourceMesh);
            im.Name = $"{Name}_Group{groupIdx}";

            for (int j = 0; j < leaf.InstanceIndices.Count; j++)
            {
                var instanceIdx = leaf.InstanceIndices[j];
                im.AddInstance(_transforms[instanceIdx]);
                _instanceGroupIndex[instanceIdx] = groupIdx;
                _instanceIndexInGroup[instanceIdx] = j;
            }

            _groups.Add(im);
            AddChild(im, AttachToParentRule.KeepWorld);
        }

        _needsBuild = false;
        _built = true;
    }

    /// <summary>
    /// 如果实例发生变化则重建。通常由 <see cref="Update"/> 自动调用。
    /// </summary>
    public void BuildIfNeeded()
    {
        if (_needsBuild)
            Build();
    }

    /// <summary>
    /// 每帧更新：自动调用 <see cref="BuildIfNeeded"/>。
    /// </summary>
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

    // ========================================================================
    // Incremental update
    // ========================================================================

    /// <summary>
    /// 尝试增量更新：若实例的新位置仍在同一空间分组内，仅原地修改 InstancedMesh 数据。
    /// </summary>
    /// <returns>成功增量更新返回 true；需要全量重建返回 false。</returns>
    private bool TryIncrementalUpdate(int index, Matrix4x4 transform)
    {
        if (!_built || _rootNode == null)
            return false;
        if (index >= _instanceGroupIndex.Count || index >= _instanceIndexInGroup.Count)
            return false;

        var newPos = transform.Translation;
        var targetLeaf = _rootNode.FindLeafForPosition(newPos);
        if (targetLeaf == null || targetLeaf.GroupIndex < 0)
            return false;

        var oldGroupIdx = _instanceGroupIndex[index];
        if (targetLeaf.GroupIndex != oldGroupIdx)
            return false; // 跨分组 → 需要重建

        // 同一分组 → 原地更新
        var idxInGroup = _instanceIndexInGroup[index];
        _groups[oldGroupIdx].UpdateInstance(idxInGroup, transform);
        InPlaceUpdateCount++;
        return true;
    }

    // ========================================================================
    // Internal helpers
    // ========================================================================

    private void Invalidate()
    {
        _needsBuild = true;
        _built = false;
        _rootNode = null;
    }

    private void DestroyGroups()
    {
        foreach (var group in _groups)
        {
            if (_children.Contains(group))
                RemoveChild(group, AttachToParentRule.KeepWorld);
        }
    }

    private BoundingBox? ComputeOverallBoundingBox()
    {
        var localBB = SourceMesh.LocalBoundingBox;
        if (localBB != null && _transforms.Count > 0)
        {
            var boxes = new List<BoundingBox>(System.Math.Min(_transforms.Count, 1024));
            foreach (var t in _transforms)
            {
                boxes.Add(localBB.Transform(t));
            }
            return BoundingBox.CreateMerged(boxes);
        }

        return ComputeBoundingBoxFromPositions();
    }

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

        // 加一点 padding 避免边界实例丢失
        var padding = new Vector3(0.1f);
        return new BoundingBox(min - padding, max + padding);
    }
}

/// <summary>
/// 内部八叉树节点，用于按空间位置对实例进行分组。
/// Build 后作为叶子节点与 InstancedMesh 一一对应；
/// Build 后保留树结构供增量更新时按位置查找所属分组。
/// </summary>
internal class InstanceOctreeNode
{
    /// <summary>
    /// 节点的世界空间包围盒。
    /// </summary>
    public BoundingBox Bounds { get; }

    /// <summary>
    /// Build 后，叶子节点对应的 InstancedMesh 在 _groups 列表中的索引。
    /// 非叶子节点或未关联时为 -1。
    /// </summary>
    public int GroupIndex { get; set; } = -1;

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

    public InstanceOctreeNode(BoundingBox bounds, int groupIndex)
    {
        Bounds = bounds;
        GroupIndex = groupIndex;
    }

    /// <summary>
    /// 将实例索引加入当前节点。
    /// </summary>
    public void Insert(int instanceIndex, Vector3 position)
    {
        InstanceIndices.Add(instanceIndex);
    }

    /// <summary>
    /// 递归细分节点，直到每个叶子节点的实例数不超过 maxPerNode 或达到最大深度。
    /// </summary>
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
            Children[i] = new InstanceOctreeNode(
                new BoundingBox(childCenter - childSize / 2, childCenter + childSize / 2),
                -1);
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

        // 清空当前节点并保留边界实例
        InstanceIndices.Clear();
        InstanceIndices.AddRange(remaining);

        // 递归细分各子节点
        bool allChildrenEmpty = true;
        for (int i = 0; i < 8; i++)
        {
            Children[i].Subdivide(transforms, maxPerNode, maxDepth, currentDepth + 1);
            if (Children[i].InstanceIndices.Count > 0 || !Children[i].IsLeaf)
                allChildrenEmpty = false;
        }

        // 全部为空则回退为叶子
        if (allChildrenEmpty && InstanceIndices.Count == 0)
        {
            Children = null;
        }
    }

    /// <summary>
    /// 收集此子树中的所有叶子节点。
    /// </summary>
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

    /// <summary>
    /// 根据世界坐标查找包含该位置的叶子节点。
    /// 用于增量更新时判断实例应属于哪个分组。
    /// </summary>
    /// <param name="position">世界空间位置。</param>
    /// <returns>包含该位置的叶子节点；如果位置不在此子树内则返回 null。</returns>
    public InstanceOctreeNode? FindLeafForPosition(Vector3 position)
    {
        if (!Bounds.Contains(position))
            return null;

        if (IsLeaf)
            return InstanceIndices.Count > 0 ? this : null;

        // 非叶子 → 递归查找子节点
        if (Children != null)
        {
            foreach (var child in Children)
            {
                var result = child.FindLeafForPosition(position);
                if (result != null)
                    return result;
            }
        }

        // 位置在节点内但不在任何子节点中（边界情况），返回当前节点
        return InstanceIndices.Count > 0 ? this : null;
    }
}
