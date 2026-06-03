using Aura3D.Core.Nodes;
using System.Numerics;

namespace Aura3D.Core.Scenes;

/// <summary>
/// 射线拾取的结果，包含被命中的节点、命中距离和世界空间坐标。
/// </summary>
public class PickResult
{
    /// <summary>
    /// 被拾取到的节点（Mesh 或 Model）。
    /// </summary>
    public required Node Node { get; init; }

    /// <summary>
    /// 如果是 <see cref="InstancedMesh"/> 实例被命中，则为实例索引；否则为 null。
    /// </summary>
    public int? InstanceIndex { get; init; }

    /// <summary>
    /// 从射线原点到命中点的距离。
    /// </summary>
    public float Distance { get; init; }

    /// <summary>
    /// 世界空间中的命中点坐标。
    /// </summary>
    public Vector3 WorldPosition { get; init; }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (InstanceIndex.HasValue)
            return $"PickResult: {Node.Name}[{InstanceIndex.Value}] at {WorldPosition} (dist={Distance:F3})";
        return $"PickResult: {Node.Name} at {WorldPosition} (dist={Distance:F3})";
    }
}
