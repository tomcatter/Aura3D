using Aura3D.Avalonia;
using Aura3D.Core;
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

    List<Mesh> pointCloudMeshes = new();
    List<double> deltaTimes = new();

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
        foreach (var mesh in pointCloudMeshes)
            view.Remove(mesh);
        pointCloudMeshes.Clear();

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
        var cellSize = halfSize * 2 / gridDivisions;

        var rand = new Random(42);

        var material = new Material
        {
            BlendMode = BlendMode.Opaque,
        };
        material.SetParameterValue("uPointSize", pointSize);

        var cells = new Dictionary<(int, int, int), (List<float> Positions, List<float> Colors)>();

        for (int i = 0; i < pointCount; i++)
        {
            var position = GeneratePoint(rand, shape, sizeX, sizeY, sizeZ);
            var color = ComputeColor(position, shape, sizeX, sizeY, sizeZ);

            int ix = Math.Clamp((int)MathF.Floor((position.X + halfSize) / cellSize), 0, gridDivisions - 1);
            int iy = Math.Clamp((int)MathF.Floor((position.Y + halfSize) / cellSize), 0, gridDivisions - 1);
            int iz = Math.Clamp((int)MathF.Floor((position.Z + halfSize) / cellSize), 0, gridDivisions - 1);
            var key = (ix, iy, iz);

            if (!cells.TryGetValue(key, out var cell))
            {
                cell = (new List<float>(), new List<float>());
                cells[key] = cell;
            }

            cell.Positions.Add(position.X);
            cell.Positions.Add(position.Y);
            cell.Positions.Add(position.Z);
            cell.Colors.Add(color.X);
            cell.Colors.Add(color.Y);
            cell.Colors.Add(color.Z);
            cell.Colors.Add(color.W);
        }

        foreach (var (_, (positions, colors)) in cells)
        {
            var geometry = new Geometry();
            geometry.PrimitiveType = PrimitiveType.Points;
            geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, positions);
            geometry.SetVertexAttribute(BuildInVertexAttribute.Color_0, 4, colors);

            var mesh = new Mesh
            {
                Geometry = geometry,
                Material = material
            };

            pointCloudMeshes.Add(mesh);
            view.AddNode(mesh);
        }

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

    private static Vector3 GenerateBoxPoint(Random rand, float sx, float sy, float sz)
    {
        return new Vector3(
            (float)(rand.NextDouble() - 0.5) * sx,
            (float)(rand.NextDouble() - 0.5) * sy,
            (float)(rand.NextDouble() - 0.5) * sz);
    }

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
