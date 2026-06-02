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

public partial class HISMPage : UserControl
{
    bool _isPressed = false;
    Avalonia.Point point = new(-1, -1);
    double deltaTime = 0;

    InstancedMeshGroup? _group;
    int currentGridSize = 100;
    float currentSpacing = 3f;
    int currentMaxPerGroup = 64;
    int currentMaxDepth = 6;

    List<double> deltaTimes = new();

    public HISMPage()
    {
        InitializeComponent();
        aura3Dview.Focusable = true;

        // 鼠标旋转视角
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
            if (_isPressed == false) return;
            if (e.Pointer.IsPrimary == false) return;

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

        // WASD 飞行
        this.aura3Dview.KeyDown += (s, e) =>
        {
            if (aura3Dview.MainCamera == null) return;

            float speed = 30f * (float)deltaTime;
            if (e.Key == Avalonia.Input.Key.W)
                aura3Dview.MainCamera!.Position += aura3Dview.MainCamera.Forward * speed;
            else if (e.Key == Avalonia.Input.Key.S)
                aura3Dview.MainCamera!.Position -= aura3Dview.MainCamera.Forward * speed;
            else if (e.Key == Avalonia.Input.Key.A)
                aura3Dview.MainCamera!.Position -= aura3Dview.MainCamera.Right * speed;
            else if (e.Key == Avalonia.Input.Key.D)
                aura3Dview.MainCamera!.Position += aura3Dview.MainCamera.Right * speed;
            else if (e.Key == Avalonia.Input.Key.Q)
                aura3Dview.MainCamera!.Position -= Vector3.UnitY * speed;
            else if (e.Key == Avalonia.Input.Key.E)
                aura3Dview.MainCamera!.Position += Vector3.UnitY * speed;
        };

        // 剔除开关
        cullingCheckbox.IsCheckedChanged += (s, e) =>
        {
            if (aura3Dview.Scene?.RenderPipeline != null)
            {
                aura3Dview.Scene.RenderPipeline.EnableFrustumCulling =
                    cullingCheckbox.IsChecked == true;
            }
        };
    }

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        var view = sender as Aura3DView;
        if (view == null) return;

        // 高视角俯瞰全场
        view.MainCamera.Position = new Vector3(0, 80, 0);
        view.MainCamera.RotationDegrees = new Vector3(-60, 0, 0);

        // 方向光
        var dl = new DirectionalLight();
        dl.RotationDegrees = new Vector3(-45, -30, 0);
        dl.LightColor = Color.White;
        view.AddNode(dl);

        // 构建场景
        BuildScene(view);

        view.RequestNextFrameRendering();
    }

    private void BuildScene(Aura3DView view)
    {
        // 清除旧的
        if (_group != null)
        {
            view.Remove(_group);
            _group = null;
        }

        // 源网格
        var sourceMesh = new Mesh();
        sourceMesh.Geometry = new BoxGeometry();
        var material = new Material
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
        sourceMesh.Material = material;

        // 创建 HISM 组
        _group = new InstancedMeshGroup(sourceMesh)
        {
            MaxInstancesPerGroup = currentMaxPerGroup,
            MaxDepth = currentMaxDepth
        };

        // 生成二维网格实例（XZ 平面铺开），随机颜色靠不同 Mat 或接受纯色
        int total = currentGridSize * currentGridSize;
        var transforms = new List<Matrix4x4>(total);
        float offset = (currentGridSize - 1) * currentSpacing / 2f;
        var rand = new Random(42);

        for (int x = 0; x < currentGridSize; x++)
        {
            for (int z = 0; z < currentGridSize; z++)
            {
                float px = x * currentSpacing - offset;
                float pz = z * currentSpacing - offset;
                // 微小的 Y 变化模拟地形
                float py = (float)(Math.Sin(x * 0.3) * Math.Cos(z * 0.3) * 2.0);
                // 随机缩放增加趣味
                float s = 0.5f + (float)rand.NextDouble() * 1.0f;

                var t = Matrix4x4.CreateScale(s)
                    * Matrix4x4.CreateFromYawPitchRoll((float)rand.NextDouble() * 0.5f, 0, 0)
                    * Matrix4x4.CreateTranslation(px, py, pz);

                transforms.Add(t);
            }
        }

        _group.SetInstances(transforms);
        _group.Build();

        view.AddNode(_group);

        statsText.Text = $"Instances: {_group.InstanceCount} | Groups: {_group.GroupCount} | FPS: --";
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        deltaTime = args.DeltaTime;

        // FPS 统计
        if (deltaTimes.Count >= 10) deltaTimes.RemoveAt(0);
        deltaTimes.Add(deltaTime);
        var avgDt = deltaTimes.Average();

        if (_group != null)
        {
            statsText.Text = $"Instances: {_group.InstanceCount} | Groups: {_group.GroupCount} | FPS: {(int)(1 / avgDt)}";
        }

        (sender as Aura3DView)?.RequestNextFrameRendering();
    }

    private void BuildButton_Click(object? sender, RoutedEventArgs e)
    {
        if (int.TryParse(gridSizeTextBox.Text?.Trim(), out int size) && size > 0)
            currentGridSize = size;
        if (float.TryParse(spacingTextBox.Text?.Trim(), out float sp) && sp > 0)
            currentSpacing = sp;
        if (int.TryParse(maxPerGroupTextBox.Text?.Trim(), out int mpg) && mpg > 0)
            currentMaxPerGroup = mpg;
        if (int.TryParse(maxDepthTextBox.Text?.Trim(), out int md) && md > 0)
            currentMaxDepth = md;
        BuildScene(aura3Dview);
    }
}
