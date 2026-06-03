using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 方向轴可视化节点，在场景中显示 RGB 三色坐标轴（X=红, Y=绿, Z=蓝），
/// 带有箭头尖端作为方向指示器。
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
    /// X 轴（红色）的网格节点。
    /// </summary>
    public Mesh XAxisMesh { get; private set; }

    /// <summary>
    /// Y 轴（绿色）的网格节点。
    /// </summary>
    public Mesh YAxisMesh { get; private set; }

    /// <summary>
    /// Z 轴（蓝色）的网格节点。
    /// </summary>
    public Mesh ZAxisMesh { get; private set; }

    /// <summary>
    /// 初始化 <see cref="AxisGizmo"/> 类的新实例。
    /// </summary>
    /// <param name="axisLength">轴线的长度，默认为 1.0。</param>
    public AxisGizmo(float axisLength = 1.0f)
    {
        Name = "AxisGizmo";
        AxisLength = axisLength;

        XAxisMesh = CreateAxisMesh(
            name: "AxisX",
            direction: new Vector3(1, 0, 0),
            color: Color.Red);

        YAxisMesh = CreateAxisMesh(
            name: "AxisY",
            direction: new Vector3(0, 1, 0),
            color: Color.Green);

        ZAxisMesh = CreateAxisMesh(
            name: "AxisZ",
            direction: new Vector3(0, 0, 1),
            color: Color.Blue);

        AddChild(XAxisMesh, AttachToParentRule.KeepLocal);
        AddChild(YAxisMesh, AttachToParentRule.KeepLocal);
        AddChild(ZAxisMesh, AttachToParentRule.KeepLocal);
    }

    private Mesh CreateAxisMesh(string name, Vector3 direction, Color color)
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

        var geometry = new Geometry
        {
            PrimitiveType = PrimitiveType.Lines
        };
        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);

        var material = new Material
        {
            BlendMode = BlendMode.Opaque,
            DoubleSided = true
        };
        material.SetShaderSource("DebugDrawPass", ShaderType.Vertex, ShaderResource.DebugVert);
        material.SetShaderSource("DebugDrawPass", ShaderType.Fragment, ShaderResource.DebugFrag);
        material.SetParameterValue("uDebugColor", color.ToVector4());
        // 方向轴始终渲染在最上层，不被场景物体遮挡
        material.SetParameterValue("noDepthTest", true);

        material.SetShaderPassParametersCallback("DebugDrawPass", pass =>
        {
            if (material.TryGetParameterValue<Vector4>("uDebugColor", out var c))
            {
                pass.UniformVector3("uColor", new Vector3(c.X, c.Y, c.Z));
            }
        });

        var mesh = new Mesh
        {
            Name = name,
            Geometry = geometry,
            Material = material
        };

        return mesh;
    }

    private static void AddLineVertex(List<float> positions, Vector3 point)
    {
        positions.Add(point.X);
        positions.Add(point.Y);
        positions.Add(point.Z);
    }
}
