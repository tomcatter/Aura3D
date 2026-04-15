using Silk.NET.Core.Native;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Aura3D.Core.Math;

/// <summary>
/// 三维八叉树空间索引，用于高效的空间查询和物体管理（单线程版本）
/// </summary>
public class Octree<T> where T : IOctreeObject
{
    /// <summary>
    /// 八叉树最大深度
    /// </summary>
    public int MaxDepth => _maxDepth;
    private readonly int _maxDepth;

    /// <summary>
    /// 八叉树根节点尺寸
    /// </summary>
    public Vector3 Size => _size;

    private Vector3 _size;

    /// <summary>
    /// 八叉树根节点
    /// </summary>
    private OctreeNode<T> _rootNode;

    /// <summary>
    /// 所有加入八叉树的物体
    /// </summary>
    private readonly HashSet<T> _allObjects = new HashSet<T>();

    /// <summary>
    /// 初始化八叉树
    /// </summary>
    /// <param name="size">根节点尺寸（沿X/Y/Z轴的长度）</param>
    /// <param name="maxDepth">八叉树最大深度（≥0）</param>
    /// <exception cref="ArgumentOutOfRangeException">maxDepth 为负数时抛出</exception>
    /// <exception cref="ArgumentException">size 无效（零/负数/NaN/Infinity）时抛出</exception>
    public Octree(Vector3 size, int maxDepth)
    {
        // 参数校验
        if (maxDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "最大深度不能为负数");

        if (BoundingBox.IsInvalidVector(size) || size.X <= 0 || size.Y <= 0 || size.Z <= 0)
            throw new ArgumentException("尺寸必须为正数且非 NaN/Infinity", nameof(size));

        _size = size;
        _maxDepth = maxDepth;
        _rootNode = CreateRootNode();
    }

    /// <summary>
    /// 创建八叉树根节点
    /// </summary>
    /// <returns>根节点</returns>
    private OctreeNode<T> CreateRootNode()
    {
        return CreateOctreeNode(Vector3.Zero, _size, 0);
    }

    /// <summary>
    /// 创建八叉树节点
    /// </summary>
    /// <param name="center">节点中心点</param>
    /// <param name="size">节点尺寸</param>
    /// <param name="depth">节点深度</param>
    /// <returns>新的八叉树节点</returns>
    internal OctreeNode<T> CreateOctreeNode(Vector3 center, Vector3 size, int depth)
    {
        return new OctreeNode<T>(this, center, size, depth);
    }

    /// <summary>
    /// 添加物体到八叉树
    /// </summary>
    /// <param name="obj">待添加的物体</param>
    /// <returns>添加成功返回 true，已存在返回 false</returns>
    /// <exception cref="ArgumentNullException">obj 为 null 时抛出</exception>
    /// <exception cref="ArgumentException">obj 的包围盒无效时抛出</exception>
    public bool Add(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        // 校验包围盒有效性
        if (BoundingBox.IsInvalidVector(obj.BoundingBox.Min) ||
            BoundingBox.IsInvalidVector(obj.BoundingBox.Max))
            throw new ArgumentException("物体的包围盒包含无效值", nameof(obj));

        if (_allObjects.Contains(obj))
            return false;

        if (_rootNode.BoundingBox.Contains(obj.BoundingBox) == false)
        {
            foreach (var obj2 in _allObjects)
            {
                foreach (var objNode in obj2.BelongingNodes)
                {
                    if (objNode is OctreeNode<T> node)
                    {
                        node.Remove(obj2);
                    }
                }
                obj2.BelongingNodes.Clear();
            }

            var newBoundingBox = _rootNode.BoundingBox;

            var newSize = Size;

            while (newBoundingBox.Contains(obj.BoundingBox) == false)
            {

                if (obj.BoundingBox.Max.X > newSize.X / 2 || obj.BoundingBox.Min.X < newSize.X / -2)
                {
                    newSize.X = newSize.X * 2;
                }

                if (obj.BoundingBox.Max.Y > newSize.Y / 2 || obj.BoundingBox.Min.Y < newSize.Y / -2)
                {
                    newSize.Y = newSize.Y * 2;
                }

                if (obj.BoundingBox.Max.Z > newSize.Z / 2 || obj.BoundingBox.Min.Z < newSize.Z / -2)
                {
                    newSize.Z = newSize.Z * 2;
                }

                newBoundingBox = new BoundingBox(new Vector3(newSize.X / -2, newSize.Y / -2, newSize.Z / -2), new Vector3(newSize.X / 2, newSize.Y / 2, newSize.Z / 2));
            }

            Rebuild(newSize);
        }

        _rootNode.Add(obj);
        _allObjects.Add(obj);

        
        return true;
    }

    private void Rebuild(Vector3 newSize)
    {
        _size = newSize;
        _rootNode = CreateRootNode();

        foreach(var obj in _allObjects)
        {
            _rootNode.Add(obj);
        }

    }

    /// <summary>
    /// 从八叉树移除物体
    /// </summary>
    /// <param name="obj">待移除的物体</param>
    /// <returns>移除成功返回 true，不存在返回 false</returns>
    /// <exception cref="ArgumentNullException">obj 为 null 时抛出</exception>
    public bool Remove(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (!_allObjects.Contains(obj))
            return false;

        // 从所有所属节点移除
        foreach (var objNode in obj.BelongingNodes.ToArray())
        {
            if (objNode is OctreeNode<T> node)
                node.Remove(obj);
        }
        obj.BelongingNodes.Clear();

        _allObjects.Remove(obj);
        return true;
    }

    /// <summary>
    /// 更新物体在八叉树中的位置（移除后重新添加）
    /// </summary>
    /// <param name="obj">待更新的物体</param>
    /// <exception cref="ArgumentNullException">obj 为 null 时抛出</exception>
    /// <exception cref="KeyNotFoundException">obj 未加入八叉树时抛出</exception>
    public void Update(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (!_allObjects.Contains(obj))
            throw new KeyNotFoundException("物体未加入八叉树，无法更新");

        // 从所有所属节点移除
        foreach (var objNode in obj.BelongingNodes.ToArray())
        {
            if (objNode is OctreeNode<T> node)
                node.Remove(obj);
        }
        obj.BelongingNodes.Clear();

        // 重新添加
        _rootNode.Add(obj);
    }

    /// <summary>
    /// 空间查询：获取指定包围盒内的所有物体
    /// </summary>
    /// <param name="queryBox">查询包围盒</param>
    /// <returns>符合条件的物体集合</returns>
    /// <exception cref="ArgumentNullException">queryBox 为 null 时抛出</exception>



    /// <summary>
    /// 空间查询：获取指定包围盒内的所有物体
    /// </summary>
    /// <param name="queryBox">查询包围盒</param>
    /// <returns>符合条件的物体集合</returns>
    /// <exception cref="ArgumentNullException">queryBox 为 null 时抛出</exception>
    public void Query(BoundingBox queryBox, List<T> result)
    {
        ArgumentNullException.ThrowIfNull(queryBox);

        _rootNode.Query(queryBox, result);
    }

    /// <summary>
    /// 空间查询：获取指定包围盒内的所有物体
    /// </summary>
    /// <param name="filter">判断函数</param>
    /// <returns>符合条件的物体集合</returns>
    /// <exception cref="ArgumentNullException">queryBox 为 null 时抛出</exception>
    public void Query(Func<BoundingBox, bool> filter, List<T> result)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _rootNode.Query(filter, result);
    }

    /// <summary>
    /// 清空八叉树所有物体
    /// </summary>
    public void Clear()
    {
        foreach (var obj in _allObjects)
        {
            obj.BelongingNodes.Clear();
        }
        _rootNode.Clear();
        _allObjects.Clear();
    }
}

/// <summary>
/// 八叉树节点（内部实现）
/// </summary>
public class OctreeNode<T> where T : IOctreeObject
{
    /// <summary>
    /// 所属八叉树
    /// </summary>
    private readonly Octree<T> _octree;

    /// <summary>
    /// 节点深度（根节点为 0）
    /// </summary>
    private readonly int _depth;

    /// <summary>
    /// 节点包围盒
    /// </summary>
    public BoundingBox BoundingBox { get; }

    /// <summary>
    /// 子节点（8个，初始化时为 null，按需创建）
    /// </summary>
    private List<OctreeNode<T>>? _children;

    /// <summary>
    /// 当前节点存储的物体
    /// </summary>
    private readonly HashSet<T> _objects = new HashSet<T>();

    /// <summary>
    /// 初始化八叉树节点
    /// </summary>
    /// <param name="octree">所属八叉树</param>
    /// <param name="center">节点中心点</param>
    /// <param name="size">节点尺寸</param>
    /// <param name="depth">节点深度</param>
    public OctreeNode(Octree<T> octree, Vector3 center, Vector3 size, int depth)
    {
        _octree = octree;
        _depth = depth;
        BoundingBox = new BoundingBox(center - size / 2, center + size / 2);
    }

    /// <summary>
    /// 添加物体到节点（递归）
    /// </summary>
    /// <param name="obj">待添加的物体</param>
    public void Add(T obj)
    {
        // 1. 达到最大深度，直接添加到当前节点
        if (_depth >= _octree.MaxDepth)
        {
            _objects.Add(obj);
            obj.BelongingNodes.Add(this);
            return;
        }

        // 2. 物体尺寸超过子节点尺寸，直接添加到当前节点
        var childSize = BoundingBox.Size / 2;
        if (obj.BoundingBox.Size.X > childSize.X + BoundingBox.DefaultEpsilon ||
            obj.BoundingBox.Size.Y > childSize.Y + BoundingBox.DefaultEpsilon ||
            obj.BoundingBox.Size.Z > childSize.Z + BoundingBox.DefaultEpsilon)
        {
            _objects.Add(obj);
            obj.BelongingNodes.Add(this);
            return;
        }

        // 3. 按需创建子节点
        EnsureChildrenCreated();

        // 4. 将物体添加到所有相交的子节点
        bool addedToChild = false;
        foreach (var child in _children!)
        {
            if (child.BoundingBox.Intersects(obj.BoundingBox))
            {
                child.Add(obj);
                addedToChild = true;
            }
        }

        // 5. 无相交子节点时，添加到当前节点
        if (!addedToChild)
        {
            _objects.Add(obj);
            obj.BelongingNodes.Add(this);
        }
    }

    /// <summary>
    /// 从节点移除物体（递归）
    /// </summary>
    /// <param name="obj">待移除的物体</param>
    public void Remove(T obj)
    {
        // 从当前节点移除
        _objects.Remove(obj);

        // 递归移除子节点中的物体
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.Remove(obj);
            }
        }
    }

    /// <summary>
    /// 空间查询（递归）
    /// </summary>
    /// <param name="queryBox">查询包围盒</param>
    /// <param name="result">查询结果（输出参数）</param>
    public void Query(BoundingBox queryBox, List<T> result)
    {
        // 当前节点与查询盒无交集，直接返回
        if (!BoundingBox.Intersects(queryBox))
            return;

        // 添加当前节点中符合条件的物体
        foreach (var obj in _objects)
        {
            if (obj.BoundingBox.Intersects(queryBox) && !result.Contains(obj))
                result.Add(obj);
        }

        // 递归查询子节点
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.Query(queryBox, result);
            }
        }
    }


    /// <summary>
    /// 空间查询（递归）
    /// </summary>
    /// <param name="filter">判断函数</param>
    /// <param name="result">查询结果（输出参数）</param>
    public void Query(Func<BoundingBox, bool> filter, List<T> result)
    {
        if (filter.Invoke(this.BoundingBox) == false) 
            return;

        // 添加当前节点中符合条件的物体
        foreach (var obj in _objects)
        {
            if (filter.Invoke(obj.BoundingBox) && !result.Contains(obj))
            {
                result.Add(obj);
            }
        }

        // 递归查询子节点
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.Query(filter, result);
            }
        }
    }

    /// <summary>
    /// 清空节点所有物体（递归）
    /// </summary>
    public void Clear()
    {
        _objects.Clear();

        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.Clear();
            }
        }
    }

    /// <summary>
    /// 确保子节点已创建（按需初始化）
    /// </summary>
    private void EnsureChildrenCreated()
    {
        if (_children != null)
            return;

        _children = new List<OctreeNode<T>>(8);
        var center = BoundingBox.Center;
        var childSize = BoundingBox.Size / 2;
        var quarterSize = BoundingBox.Size / 4;

        // 8个子节点的偏移量（±X, ±Y, ±Z）
        var offsets = new[]
        {
            new Vector3(-1, -1, -1), new Vector3(1, -1, -1),
            new Vector3(-1, 1, -1),  new Vector3(-1, -1, 1),
            new Vector3(1, 1, -1),   new Vector3(1, -1, 1),
            new Vector3(-1, 1, 1),   new Vector3(1, 1, 1)
        };

        // 创建8个子节点
        foreach (var offset in offsets)
        {
            var childCenter = center + offset * quarterSize;
            _children.Add(_octree.CreateOctreeNode(childCenter, childSize, _depth + 1));
        }
    }
}
