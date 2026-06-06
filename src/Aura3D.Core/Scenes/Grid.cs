using System.Drawing;

namespace Aura3D.Core.Scenes;

/// <summary>
/// 网格可视化配置，由 DebugDrawPass 使用即时模式绘制。
/// </summary>
public class Grid
{
    /// <summary>
    /// 是否显示参考网格。
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// 网格的总尺寸（宽度和深度）。
    /// </summary>
    public float Size { get; set; } = 10.0f;

    /// <summary>
    /// 网格的细分数量（每个方向的单元格数量）。
    /// </summary>
    public int Divisions { get; set; } = 10;

    /// <summary>
    /// 网格线的颜色。
    /// </summary>
    public Color LineColor { get; set; } = Color.FromArgb(255, 80, 80, 80);

    /// <summary>
    /// 中心线（X=0 和 Z=0 轴线）的颜色。
    /// </summary>
    public Color CenterLineColor { get; set; } = Color.FromArgb(255, 60, 60, 60);
}
