using System.Drawing;

namespace Aura3D.Core.Scenes;

/// <summary>
/// 方向轴可视化配置，由 DebugDrawPass 使用即时模式绘制。
/// </summary>
public class AxisGizmo
{
    /// <summary>
    /// 是否显示方向轴。
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// 轴线的总长度。
    /// </summary>
    public float AxisLength { get; set; } = 1.0f;

    /// <summary>
    /// 箭头尖端的大小。
    /// </summary>
    public float ArrowheadSize { get; set; } = 0.15f;
}
