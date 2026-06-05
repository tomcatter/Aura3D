using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class PointCloudPage : UserControl
{
    private CameraController _cameraController;

    PointCloudMeshGroup? pointCloudGroup;
    int currentPointCount = 0;

    List<double> deltaTimes = new();

    // 逐点顶点着色器：逐顶点位置 + 颜色 + gl_PointSize
    private const string PointCloudVertexShader = """
        #version 300 es
        precision mediump float;

        //{{defines}}

        layout(location = 0) in vec3 position;
        layout(location = 2) in vec4 color;

        uniform mat4 modelMatrix;
        uniform mat4 viewMatrix;
        uniform mat4 projectionMatrix;
        uniform float uPointSize;

        out vec4 vColor;

        void main()
        {
            vec4 worldPosition = modelMatrix * vec4(position, 1.0);
            gl_Position = projectionMatrix * viewMatrix * worldPosition;
            gl_PointSize = uPointSize;
            vColor = color;
        }
        """;

    // 逐点片段着色器：圆形裁剪 + 逐顶点颜色
    private const string PointCloudFragmentShader = """
        #version 300 es
        precision mediump float;
        out vec4 outColor;

        in vec4 vColor;

        void main()
        {
            float dist = length(gl_PointCoord - 0.5);
            if (dist > 0.5) discard;

            outColor = vColor;
        }
        """;

    public PointCloudPage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3Dview);
    }

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        var view = sender as Aura3DView;
        if (view == null)
            return;

        view.AutoRequestNextFrameRendering = false;

        view.MainCamera.Position = new Vector3(0, 0, 30);
        view.MainCamera.RotationDegrees = new Vector3(0, 0, 0);

        var dl = new DirectionalLight();
        dl.RotationDegrees = new Vector3(-30, -15, 0);
        dl.LightColor = Color.White;
        view.AddNode(dl);

        BuildPointCloud(view);

        view.RequestNextFrameRendering();
    }

    private bool TryParseFloat(TextBox textBox, float defaultValue, out float value)
    {
        if (float.TryParse(textBox.Text?.Trim(), out value))
            return true;
        value = defaultValue;
        return false;
    }

    private bool TryParseInt(TextBox textBox, int defaultValue, out int value)
    {
        if (int.TryParse(textBox.Text?.Trim(), out value))
            return true;
        value = defaultValue;
        return false;
    }

    private void BuildPointCloud(Aura3DView view)
    {
        pointCloudGroup?.RemoveFrom(view);
        pointCloudGroup = null;

        // 解析参数
        if (!TryParseInt(pointCountTextBox, 20000, out int pointCount))
            pointCount = 20000;

        TryParseFloat(sizeXTextBox, 20f, out float sizeX);
        TryParseFloat(sizeYTextBox, 20f, out float sizeY);
        TryParseFloat(sizeZTextBox, 20f, out float sizeZ);
        TryParseFloat(pointSizeTextBox, 5f, out float pointSize);
        TryParseInt(gridTextBox, 4, out int gridDivisions);

        var shape = (shapeComboBox.SelectedIndex) switch
        {
            1 => PointCloudShape.Sphere,
            2 => PointCloudShape.Disk,
            _ => PointCloudShape.Box,
        };

        var halfSize = Math.Max(Math.Max(sizeX, sizeY), sizeZ) / 2f;

        var rand = new Random(42);

        // 构建材质（所有 mesh 共享同一材质实例）
        var material = new Material
        {
            BlendMode = BlendMode.Opaque,
        };
        material.SetShaderSource("LightPass", ShaderType.Vertex, PointCloudVertexShader);
        material.SetShaderSource("LightPass", ShaderType.Fragment, PointCloudFragmentShader);
        material.SetParameterValue("uPointSize", pointSize);
        material.SetShaderPassParametersCallback("LightPass", pass =>
        {
            if (material.TryGetParameterValue<float>("uPointSize", out var ps))
            {
                pass.UniformFloat("uPointSize", ps);
            }
        });

        pointCloudGroup = new PointCloudMeshGroup(gridDivisions, halfSize);

        for (int i = 0; i < pointCount; i++)
        {
            var position = GeneratePoint(rand, shape, sizeX, sizeY, sizeZ);
            var color = ComputeColor(position, shape, sizeX, sizeY, sizeZ);
            pointCloudGroup.AddPoint(position, color);
        }

        pointCloudGroup.BuildAndAddTo(view, material);
        currentPointCount = pointCount;

        var shapeName = shape switch
        {
            PointCloudShape.Sphere => "Sphere",
            PointCloudShape.Disk => "Disk",
            _ => "Box"
        };
        infoText.Text = $"Points: {pointCount} | Shape: {shapeName} | Grid: {gridDivisions}³ | FPS: --";
    }

    private static Vector3 GeneratePoint(Random rand, PointCloudShape shape, float sx, float sy, float sz)
    {
        return shape switch
        {
            PointCloudShape.Sphere => GenerateSpherePoint(rand, sx / 2f),
            PointCloudShape.Disk => GenerateDiskPoint(rand, sx / 2f, sy / 2f),
            _ => GenerateBoxPoint(rand, sx, sy, sz),
        };
    }

    /// <summary>
    /// Box: 在 [-sx/2, sx/2] × [-sy/2, sy/2] × [-sz/2, sz/2] 范围内均匀分布
    /// </summary>
    private static Vector3 GenerateBoxPoint(Random rand, float sx, float sy, float sz)
    {
        return new Vector3(
            (float)(rand.NextDouble() - 0.5) * sx,
            (float)(rand.NextDouble() - 0.5) * sy,
            (float)(rand.NextDouble() - 0.5) * sz);
    }

    /// <summary>
    /// Sphere: 在半径为 radius 的球体内均匀分布（体积均匀）
    /// </summary>
    private static Vector3 GenerateSpherePoint(Random rand, float radius)
    {
        float theta = (float)(rand.NextDouble() * Math.PI * 2);
        float phi = (float)(Math.Acos(2 * rand.NextDouble() - 1));
        float r = (float)(radius * Math.Cbrt(rand.NextDouble()));

        return new Vector3(
            r * (float)(Math.Sin(phi) * Math.Cos(theta)),
            r * (float)(Math.Sin(phi) * Math.Sin(theta)),
            r * (float)(Math.Cos(phi)));
    }

    /// <summary>
    /// Disk: 在 XZ 平面圆盘上分布，Y 轴方向有厚度
    /// </summary>
    private static Vector3 GenerateDiskPoint(Random rand, float radius, float thickness)
    {
        float angle = (float)(rand.NextDouble() * Math.PI * 2);
        float r = radius * (float)Math.Sqrt(rand.NextDouble());

        return new Vector3(
            r * (float)Math.Cos(angle),
            (float)(rand.NextDouble() - 0.5f) * thickness * 2f,
            r * (float)Math.Sin(angle));
    }

    private static Vector4 ComputeColor(Vector3 position, PointCloudShape shape, float sx, float sy, float sz)
    {
        return shape switch
        {
            PointCloudShape.Sphere => ComputeSphereColor(position, sx / 2f),
            PointCloudShape.Disk => ComputeDiskColor(position, sx / 2f, sy / 2f),
            _ => ComputeBoxColor(position, sx, sy, sz),
        };
    }

    private static Vector4 ComputeBoxColor(Vector3 p, float sx, float sy, float sz)
    {
        float hx = sx / 2f;
        float hy = sy / 2f;
        float hz = sz / 2f;
        return new Vector4(
            (p.X / hx + 1f) / 2f,
            (p.Y / hy + 1f) / 2f,
            (p.Z / hz + 1f) / 2f,
            1.0f);
    }

    private static Vector4 ComputeSphereColor(Vector3 p, float radius)
    {
        return new Vector4(
            (p.X / radius + 1f) / 2f,
            (p.Y / radius + 1f) / 2f,
            (p.Z / radius + 1f) / 2f,
            1.0f);
    }

    private static Vector4 ComputeDiskColor(Vector3 p, float radius, float thickness)
    {
        float t = thickness > 0.001f ? thickness : 0.001f;
        return new Vector4(
            (p.X / radius + 1f) / 2f,
            (p.Y / t + 1f) / 2f,
            (p.Z / radius + 1f) / 2f,
            1.0f);
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        if (deltaTimes.Count >= 10)
            deltaTimes.RemoveAt(0);
        deltaTimes.Add(args.DeltaTime);
        var avgDt = deltaTimes.Average();
        infoText.Text = infoText.Text?.Split(" | FPS:")[0] + $" | FPS: {(int)(1 / avgDt)}";

        (sender as Aura3DView)?.RequestNextFrameRendering();
    }

    private void RegenerateButton_Click(object? sender, RoutedEventArgs e)
    {
        BuildPointCloud(aura3Dview);
    }
}

enum PointCloudShape
{
    Box,
    Sphere,
    Disk,
}

/// <summary>
/// 点云空间划分网格，将点分组到多个 Mesh 中以便引擎自动视锥剔除。
/// </summary>
class PointCloudMeshGroup
{
    private readonly int _divisions;
    private readonly float _halfSize;
    private readonly float _cellSize;
    private readonly Dictionary<(int, int, int), CellData> _cells = new();

    private readonly List<Mesh> _meshes = new();

    public PointCloudMeshGroup(int divisions, float halfSize)
    {
        _divisions = divisions;
        _halfSize = halfSize;
        _cellSize = halfSize * 2 / divisions;
    }

    public void AddPoint(Vector3 position, Vector4 color)
    {
        var cell = GetCellIndex(position);
        if (!_cells.TryGetValue(cell, out var cellData))
        {
            cellData = new CellData();
            _cells[cell] = cellData;
        }
        cellData.Positions.Add(position.X);
        cellData.Positions.Add(position.Y);
        cellData.Positions.Add(position.Z);
        cellData.Colors.Add(color.X);
        cellData.Colors.Add(color.Y);
        cellData.Colors.Add(color.Z);
        cellData.Colors.Add(color.W);
    }

    public void BuildAndAddTo(Aura3DView view, Material material)
    {
        foreach (var (_, cellData) in _cells)
        {
            var geometry = new Geometry();
            geometry.PrimitiveType = Aura3D.Core.Resources.PrimitiveType.Points;
            geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, cellData.Positions);
            geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, cellData.Colors);

            var mesh = new Mesh
            {
                Geometry = geometry,
                Material = material
            };

            _meshes.Add(mesh);
            view.AddNode(mesh);
        }
    }

    public void RemoveFrom(Aura3DView view)
    {
        foreach (var mesh in _meshes)
        {
            view.Remove(mesh);
        }
        _meshes.Clear();
        _cells.Clear();
    }

    private (int, int, int) GetCellIndex(Vector3 position)
    {
        int ix = (int)MathF.Floor((position.X + _halfSize) / _cellSize);
        int iy = (int)MathF.Floor((position.Y + _halfSize) / _cellSize);
        int iz = (int)MathF.Floor((position.Z + _halfSize) / _cellSize);

        ix = Math.Clamp(ix, 0, _divisions - 1);
        iy = Math.Clamp(iy, 0, _divisions - 1);
        iz = Math.Clamp(iz, 0, _divisions - 1);

        return (ix, iy, iz);
    }

    class CellData
    {
        public List<float> Positions = new();
        public List<float> Colors = new();
    }
}
