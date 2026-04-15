using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Math;


/// <summary>
/// 可加入八叉树的物体接口
/// </summary>
public interface IOctreeObject
{
    /// <summary>
    /// 物体的包围盒（不能为空）
    /// </summary>
    BoundingBox? BoundingBox { get; }

    /// <summary>
    /// 物体所属的八叉树节点（用于快速移除/更新）
    /// </summary>
    List<object> BelongingNodes { get; }

    event Action<IOctreeObject>? OnBoundingBoxChanged;
}