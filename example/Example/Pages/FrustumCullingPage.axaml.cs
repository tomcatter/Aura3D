using Aura3D.Avalonia;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class FrustumCullingPage : UserControl
{
    bool _isPressed = false;

    Avalonia.Point point = new(-1, -1);

    public FrustumCullingPage()
    {
        InitializeComponent();
        aura3Dview.Focusable = true;
        checkbox.IsChecked = true;
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
            {
                return;
            }
            if (e.Key == Avalonia.Input.Key.W)
            {
                aura3Dview.MainCamera!.Position += aura3Dview.MainCamera.Forward * (float)deltaTime;
            }
            else if (e.Key == Avalonia.Input.Key.S)
            {
                aura3Dview.MainCamera!.Position -= aura3Dview.MainCamera.Forward * (float)deltaTime;
            }
            else if (e.Key == Avalonia.Input.Key.A)
            {
                aura3Dview.MainCamera!.Position -= aura3Dview.MainCamera.Right * (float)deltaTime;
            }
            else if (e.Key == Avalonia.Input.Key.D)
            {
                aura3Dview.MainCamera!.Position += aura3Dview.MainCamera.Right * (float)deltaTime;
            }
        };

    }

    List<Mesh> meshes = [];
    BoxGeometry? box = null;
    Material? material = null;

    private void Build(Aura3DView view, int num)
    {
        foreach(var mesh in meshes)
        {
            view.Remove(mesh);
        }

        meshes.Clear();
        if (box == null)
        {
             box = new BoxGeometry();
        }
        if (material == null)
        {
             material = new Material
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
        }

        int gridSize = (int)Math.Ceiling(Math.Pow(num, 1.0 / 3.0));
        float spacing = 2f;
        float offset = (gridSize - 1) * spacing / 2f;

        int currentIndex = 0;
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    if (currentIndex >= num) break;

                    float xPos = x * spacing - offset;
                    float yPos = y * spacing - offset;
                    float zPos = z * spacing - offset;

                    var mesh = new Mesh
                    {
                        Geometry = box,
                        Material = material,
                        Position = new Vector3(xPos, yPos, zPos)
                    };

                    view.AddNode(mesh);
                    meshes.Add(mesh);
                    currentIndex++;
                }
            }
        }

        // 计算整体范围
        float minX = -offset;
        float maxX = offset;
        float minZ = -offset;
        float maxZ = offset;
        float minY = -1f;   // 你固定的 Y
        float maxY = 1f;    // 可以稍微给点高度余量

        var rand = new Random();

        // 在范围内随机生成摄像机位置
        float camX = (float)(rand.NextDouble() * (maxX - minX) + minX);
        float camY = (float)(rand.NextDouble() * (maxY - minY) + minY);
        float camZ = (float)(rand.NextDouble() * (maxZ - minZ) + minZ);

        // 设置摄像机
        view.MainCamera.Position = new Vector3(camX, camY, camZ);

    }
    private void Aura3DView_SceneInitialized(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var view = sender as Aura3DView;

        if (view == null)
        {
            return;
        }

        Build(aura3Dview, 1000);

        var dl = new DirectionalLight();

        dl.RotationDegrees = new Vector3(-30, -15, 0);

        dl.LightColor = Color.Red;

        view.AddNode(dl);

    }

    double deltaTime = 0;

    private List<double> deltaTimes = [];
    private void aura3Dview_SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        deltaTime = args.DeltaTime;

        if (deltaTimes.Count >= 10)
        {
            deltaTimes.RemoveAt(0);
        }

        deltaTimes.Add(deltaTime);

        var dt = deltaTimes.Average();

        frameText.Text = "帧率:" + (int)(1 / dt);

    }

    private void CheckBox_IsCheckedChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (aura3Dview.Scene == null)
            return;
        if (checkbox.IsChecked == null || checkbox.IsChecked == false)
        {

            aura3Dview.Scene.RenderPipeline.EnableFrustumCulling = false;
        }
        else
        {
            aura3Dview.Scene.RenderPipeline.EnableFrustumCulling = true;
        }
    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (int.TryParse(numTextBox.Text.Trim(), out var num))
        {
            Build(aura3Dview, num);
        }


    }
}