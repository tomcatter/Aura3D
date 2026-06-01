using Aura3D.Avalonia;
using Aura3D.Core.Geometries;
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

public partial class InstancedRenderingPage : UserControl
{
    bool _isPressed = false;
    Avalonia.Point point = new(-1, -1);
    double deltaTime = 0;

    InstancedMesh? instancedMesh;
    List<float> instanceRotationAngles = new();
    List<float> instanceRotationSpeeds = new();
    List<Vector3> instancePositions = new();
    int currentGridSize = 0;

    List<double> deltaTimes = new();

    public InstancedRenderingPage()
    {
        InitializeComponent();
        aura3Dview.Focusable = true;

        this.aura3Dview.PointerPressed += (s, e) =>
        {
            _isPressed = true;
            point = new(-1, -1);
        };

        this.aura3Dview.PointerReleased += (s, e) =>
        {
            _isPressed = false;
            point = new(-1, -1);
        };

        this.aura3Dview.PointerMoved += (s, e) =>
        {
            if (_isPressed == false)
                return;
            if (e.Pointer.IsPrimary == false)
                return;

            var newPosition = e.GetCurrentPoint(this).Position;
            if (point.X != -1 && point.Y != -1)
            {
                var delta = newPosition - point;

                if (aura3Dview.MainCamera != null)
                {
                    aura3Dview.MainCamera!.RotationDegrees = new Vector3(
                        (float)(aura3Dview.MainCamera.RotationDegrees.X + (float)delta.Y * (float)deltaTime * 20),
                        (float)(aura3Dview.MainCamera.RotationDegrees.Y + (float)delta.X * (float)deltaTime * 20f), 0);
                }
            }
            point = newPosition;
        };

        this.aura3Dview.KeyDown += (s, e) =>
        {
            if (aura3Dview.MainCamera == null)
                return;

            if (e.Key == Avalonia.Input.Key.W)
                aura3Dview.MainCamera!.Position += aura3Dview.MainCamera.Forward * (float)deltaTime;
            else if (e.Key == Avalonia.Input.Key.S)
                aura3Dview.MainCamera!.Position -= aura3Dview.MainCamera.Forward * (float)deltaTime;
            else if (e.Key == Avalonia.Input.Key.A)
                aura3Dview.MainCamera!.Position -= aura3Dview.MainCamera.Right * (float)deltaTime;
            else if (e.Key == Avalonia.Input.Key.D)
                aura3Dview.MainCamera!.Position += aura3Dview.MainCamera.Right * (float)deltaTime;
        };
    }

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        var view = sender as Aura3DView;
        if (view == null)
            return;

        view.AutoRequestNextFrameRendering = false;

        view.MainCamera.Position = new Vector3(0, 15, 30);
        view.MainCamera.RotationDegrees = new Vector3(-20, 0, 0);

        var dl = new DirectionalLight();
        dl.RotationDegrees = new Vector3(-30, -15, 0);
        dl.LightColor = Color.White;
        view.AddNode(dl);

        BuildInstancedGrid(view, 10);

        view.RequestNextFrameRendering();
    }

    private void BuildInstancedGrid(Aura3DView view, int gridSize)
    {
        if (instancedMesh != null)
        {
            view.Remove(instancedMesh);
            instancedMesh = null;
        }

        var sourceMesh = new Mesh();
        sourceMesh.Geometry = new BoxGeometry();
        sourceMesh.Material = new Material
        {
            BlendMode = BlendMode.Opaque,
            Channels = new List<Channel>
            {
                new Channel()
                {
                    Name = "BaseColor",
                    Texture = Texture.CreateFromColor(Color.White),
                }
            }
        };

        instancedMesh = InstancedMesh.FromMesh(sourceMesh);

        instanceRotationAngles.Clear();
        instanceRotationSpeeds.Clear();
        instancePositions.Clear();

        var rand = new Random(42);
        float spacing = 2.5f;
        float offset = (gridSize - 1) * spacing / 2f;

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = new Vector3(
                        x * spacing - offset,
                        y * spacing - offset,
                        z * spacing - offset);

                    Matrix4x4 transform = Matrix4x4.CreateTranslation(pos);
                    instancedMesh.AddInstance(transform);

                    instancePositions.Add(pos);
                    instanceRotationAngles.Add((float)(rand.NextDouble() * Math.PI * 2));
                    instanceRotationSpeeds.Add((float)(rand.NextDouble() * 2 - 1));
                }
            }
        }

        view.AddNode(instancedMesh);
        currentGridSize = gridSize;

        fpsText.Text = $"Instances: {instancedMesh.InstanceCount} | FPS: --";
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        deltaTime = args.DeltaTime;

        if (deltaTimes.Count >= 10)
            deltaTimes.RemoveAt(0);
        deltaTimes.Add(deltaTime);
        var avgDt = deltaTimes.Average();
        fpsText.Text = $"Instances: {currentGridSize * currentGridSize * currentGridSize} | FPS: {(int)(1 / avgDt)}";

        if (instancedMesh != null)
        {
            for (int i = 0; i < instancedMesh.InstanceCount; i++)
            {
                instanceRotationAngles[i] += instanceRotationSpeeds[i] * (float)deltaTime;

                Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(
                    instanceRotationAngles[i],
                    instanceRotationAngles[i] * 0.7f,
                    instanceRotationAngles[i] * 0.3f);

                Matrix4x4 transform = rotation * Matrix4x4.CreateTranslation(instancePositions[i]);
                instancedMesh.UpdateInstance(i, transform);
            }
        }

        (sender as Aura3DView)?.RequestNextFrameRendering();
    }

    private void BuildButton_Click(object? sender, RoutedEventArgs e)
    {
        if (int.TryParse(gridSizeTextBox.Text?.Trim(), out int size) && size > 0)
        {
            size = Math.Min(size, 30);
            BuildInstancedGrid(aura3Dview, size);
        }
    }
}
