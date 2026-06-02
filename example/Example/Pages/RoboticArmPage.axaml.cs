using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Example.Pages;

public partial class RoboticArmPage : UserControl
{

    private CameraController _cameraController;

    public RoboticArmPage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3Dview);
    }

    Node item1 = new Node();
    Node item2 = new Node();
    Node item3 = new Node();
    Node item4 = new Node();
    Node item5 = new Node();
    Node item6 = new Node();
    Node item7 = new Node();

    Model? model = null;
    private async void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        while (App.model == null)
        {
            await Task.Delay(10);
        }
        var scene = e.Scene;

        var model = App.model.Clone(CopyType.SharedResourceData);


        item1 = model.Meshes.First(mesh => mesh.Name == "item1");
        item2 = model.Meshes.First(mesh => mesh.Name == "item2");
        item3 = model.Meshes.First(mesh => mesh.Name == "item3");
        item4 = model.Meshes.First(mesh => mesh.Name == "item4");
        item5 = model.Meshes.First(mesh => mesh.Name == "item5");
        item6 = model.Meshes.First(mesh => mesh.Name == "item6");
        item7 = model.Meshes.First(mesh => mesh.Name == "item7");

        model.Position = scene.MainCamera.Position + scene.MainCamera.Forward * 2f - scene.MainCamera.Up;

        scene.AddNode(model);

        var dl = new DirectionalLight();

        dl.RotationDegrees = new Vector3(-30, 0, 0);

        dl.LightColor = Color.White;

        scene.AddNode(dl);

        slider1.Value = CalcDegree(item2.RotationDegrees.Y);
        slider2.Value = CalcDegree(item3.RotationDegrees.X);
        slider3.Value = CalcDegree(item4.RotationDegrees.Y);
        slider4.Value = CalcDegree(item5.RotationDegrees.Y);
        slider5.Value = CalcDegree(item6.RotationDegrees.X);
        slider6.Value = CalcDegree(item7.RotationDegrees.X);

        this.model = model;
    }

    public static float CalcDegree(float degree)
    {
        if (degree < 0)
            degree += 360 * 10;
        degree %= 360;
        return degree;
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
    }


    private void slider1_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (model == null)
            return;
        item2.RotationDegrees = item2.RotationDegrees with { Y = (float)slider1.Value };
    }

    private void slider2_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (model == null)
            return;
        item3.RotationDegrees = item3.RotationDegrees with { X = (float)slider2.Value };
    }

    private void slider3_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (model == null)
            return;
        item4.RotationDegrees = item4.RotationDegrees with { Y = (float)slider3.Value };
    }

    private void slider4_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (model == null)
            return;
        item5.RotationDegrees = item5.RotationDegrees with { Y = (float)slider4.Value };
    }

    private void slider5_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (model == null)
            return;
        item6.RotationDegrees = item6.RotationDegrees with { X = (float)slider5.Value };
    }

    private void slider6_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (model == null)
            return;
        item7.RotationDegrees = item7.RotationDegrees with { X = (float)slider6.Value };
    }
}