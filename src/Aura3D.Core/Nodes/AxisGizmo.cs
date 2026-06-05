using Aura3D.Core.Resources;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 方向轴可视化节点，在场景中显示 RGB 三色坐标轴（X=红, Y=绿, Z=蓝），
/// 带有箭头尖端作为方向指示器。
/// 调试绘制数据由 DebugDrawPass 直接渲染，不经过 Mesh 管线。
/// </summary>
public class AxisGizmo : Node
{
    /// <summary>
    /// 轴线的总长度。
    /// </summary>
    public float AxisLength { get; set; } = 1.0f;

    /// <summary>
    /// 箭头尖端的大小。
    /// </summary>
    public float ArrowheadSize { get; set; } = 0.15f;

    /// <summary>
    /// X 轴（红色）的调试绘制数据。
    /// </summary>
    public DebugDrawData XAxisData { get; private set; }

    /// <summary>
    /// Y 轴（绿色）的调试绘制数据。
    /// </summary>
    public DebugDrawData YAxisData { get; private set; }

    /// <summary>
    /// Z 轴（蓝色）的调试绘制数据。
    /// </summary>
    public DebugDrawData ZAxisData { get; private set; }

    private readonly List<DebugDrawData> _debugDrawDataList;

    /// <summary>
    /// 初始化 <see cref="AxisGizmo"/> 类的新实例。
    /// </summary>
    /// <param name="axisLength">轴线的长度，默认为 1.0。</param>
    public AxisGizmo(float axisLength = 1.0f)
    {
        Name = "AxisGizmo";
        AxisLength = axisLength;

        XAxisData = CreateAxisData(
            direction: new Vector3(1, 0, 0),
            color: Color.Red);

        YAxisData = CreateAxisData(
            direction: new Vector3(0, 1, 0),
            color: Color.Green);

        ZAxisData = CreateAxisData(
            direction: new Vector3(0, 0, 1),
            color: Color.Blue);

        _debugDrawDataList = [XAxisData, YAxisData, ZAxisData];
    }

    private DebugDrawData CreateAxisData(Vector3 direction, Color color)
    {
        var positions = new List<float>();
        float len = AxisLength;
        float arrow = ArrowheadSize;

        // 确定垂直于轴的两个方向，用于绘制箭头尖端
        Vector3 perp1, perp2;
        if (MathF.Abs(direction.X) > 0.5f)
        {
            perp1 = new Vector3(0, 1, 0);
            perp2 = new Vector3(0, 0, 1);
        }
        else if (MathF.Abs(direction.Y) > 0.5f)
        {
            perp1 = new Vector3(1, 0, 0);
            perp2 = new Vector3(0, 0, 1);
        }
        else
        {
            perp1 = new Vector3(1, 0, 0);
            perp2 = new Vector3(0, 1, 0);
        }

        Vector3 tip = direction * len;
        Vector3 basePt = tip - direction * arrow;
        float arrowWidth = arrow * 0.35f;

        // 主线：原点 → 尖端
        AddLineVertex(positions, Vector3.Zero);
        AddLineVertex(positions, tip);

        // 箭头：尖端 → 四个方向的短线段
        AddLineVertex(positions, tip);
        AddLineVertex(positions, basePt + perp1 * arrowWidth);

        AddLineVertex(positions, tip);
        AddLineVertex(positions, basePt - perp1 * arrowWidth);

        AddLineVertex(positions, tip);
        AddLineVertex(positions, basePt + perp2 * arrowWidth);

        AddLineVertex(positions, tip);
        AddLineVertex(positions, basePt - perp2 * arrowWidth);

        return new DebugDrawData(this, positions.ToArray(), color, noDepthTest: true);
    }

    /// <inheritdoc />
    public override List<IGpuResource> GetGpuResources()
    {
        return [XAxisData, YAxisData, ZAxisData];
    }

    /// <inheritdoc />
    public override IEnumerable<DebugDrawData> GetDebugDrawData()
    {
        return _debugDrawDataList;
    }

    private static void AddLineVertex(List<float> positions, Vector3 point)
    {
        positions.Add(point.X);
        positions.Add(point.Y);
        positions.Add(point.Z);
    }
}
