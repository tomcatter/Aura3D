using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 网格可视化节点，在 XZ 平面上显示参考网格线。
/// Y 轴为上方（引擎使用 Y-up 坐标系）。
/// </summary>
public class Grid : Node
{
    /// <summary>
    /// 获取或设置网格的总尺寸（宽度和深度）。
    /// </summary>
    public float Size { get; set; }

    /// <summary>
    /// 获取或设置网格的细分数量（每个方向的单元格数量）。
    /// </summary>
    public int Divisions { get; set; }

    /// <summary>
    /// 获取或设置网格线的颜色。
    /// </summary>
    public Color LineColor { get; set; } = Color.FromArgb(255, 80, 80, 80);

    /// <summary>
    /// 获取或设置中心线（X=0 和 Z=0 轴线）的颜色。
    /// </summary>
    public Color CenterLineColor { get; set; } = Color.FromArgb(255, 60, 60, 60);

    /// <summary>
    /// 包含网格线的网格节点。
    /// </summary>
    public Mesh GridMesh { get; private set; }

    /// <summary>
    /// 包含中心轴线的网格节点。
    /// </summary>
    public Mesh CenterLineMesh { get; private set; }

    /// <summary>
    /// 初始化 <see cref="Grid"/> 类的新实例。
    /// </summary>
    /// <param name="size">网格的总尺寸，默认为 10.0。</param>
    /// <param name="divisions">细分数量，默认为 10。</param>
    public Grid(float size = 10.0f, int divisions = 10)
    {
        Name = "Grid";
        Size = size;
        Divisions = System.Math.Max(1, divisions);

        GridMesh = CreateGridLinesMesh("GridLines", LineColor);
        CenterLineMesh = CreateCenterLinesMesh("GridCenterLines", CenterLineColor);

        AddChild(GridMesh, AttachToParentRule.KeepLocal);
        AddChild(CenterLineMesh, AttachToParentRule.KeepLocal);
    }

    private Mesh CreateGridLinesMesh(string name, Color color)
    {
        var positions = new List<float>();
        float halfSize = Size * 0.5f;
        float step = Size / Divisions;

        // X 方向的线（沿 Z 轴分布）
        for (int i = 0; i <= Divisions; i++)
        {
            float z = -halfSize + i * step;

            // 跳过中心线（由 CenterLineMesh 处理）
            if (MathF.Abs(z) < step * 0.01f)
                continue;

            AddLineVertex(positions, new Vector3(-halfSize, 0, z));
            AddLineVertex(positions, new Vector3(halfSize, 0, z));
        }

        // Z 方向的线（沿 X 轴分布）
        for (int i = 0; i <= Divisions; i++)
        {
            float x = -halfSize + i * step;

            // 跳过中心线（由 CenterLineMesh 处理）
            if (MathF.Abs(x) < step * 0.01f)
                continue;

            AddLineVertex(positions, new Vector3(x, 0, -halfSize));
            AddLineVertex(positions, new Vector3(x, 0, halfSize));
        }

        return CreateLineMesh(name, positions, color);
    }

    private Mesh CreateCenterLinesMesh(string name, Color color)
    {
        var positions = new List<float>();
        float halfSize = Size * 0.5f;

        // X 轴中心线
        AddLineVertex(positions, new Vector3(-halfSize, 0, 0));
        AddLineVertex(positions, new Vector3(halfSize, 0, 0));

        // Z 轴中心线
        AddLineVertex(positions, new Vector3(0, 0, -halfSize));
        AddLineVertex(positions, new Vector3(0, 0, halfSize));

        return CreateLineMesh(name, positions, color);
    }

    private static Mesh CreateLineMesh(string name, List<float> positions, Color color)
    {
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
        material.SetShaderPassParametersCallback("DebugDrawPass", pass =>
        {
            if (material.TryGetParameterValue<Vector4>("uDebugColor", out var c))
            {
                pass.UniformVector3("uColor", new Vector3(c.X, c.Y, c.Z));
            }
        });

        return new Mesh
        {
            Name = name,
            Geometry = geometry,
            Material = material
        };
    }

    private static void AddLineVertex(List<float> positions, Vector3 point)
    {
        positions.Add(point.X);
        positions.Add(point.Y);
        positions.Add(point.Z);
    }
}
