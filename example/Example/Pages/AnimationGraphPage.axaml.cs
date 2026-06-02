using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Example.ViewModels;
using System;
using System.Drawing;
using System.Numerics;

namespace Example.Pages;

public partial class AnimationGraphPage : UserControl
{
    public AnimationGraphPage()
    {
        InitializeComponent();
    }

    private void aura3Dview_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        var vm = DataContext as AnimationGraphViewModel;
        if (vm == null)
            return;

        var dl = new DirectionalLight();

        dl.RotationDegrees = new Vector3(-30, 0, 0);

        dl.LightColor = Color.White;

        aura3Dview.AddNode(dl);

        using var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/Soldier.glb"));
        
        var (model, animations) =  ModelLoader.LoadGlbModelAndAnimations(stream);

        var idleNode = new AnimationGraphNode(new AnimationSampler(animations[0]));

        idleNode.BlendTime = 1;

        var walkNode = new AnimationGraphNode(new AnimationSampler(animations[3]));

        walkNode.BlendTime = 1;

        var runNode = new AnimationGraphNode(new AnimationSampler(animations[1]));

        runNode.BlendTime = 1;

        idleNode.AddNextNode((sampler, deltaTime) => vm.Speed > 0.001, walkNode);

        walkNode.AddNextNode((sampler, deltaTime) => vm.Speed > 300, runNode);

        walkNode.AddNextNode((sampler, deltaTime) => vm.Speed < 0.001, idleNode);

        runNode.AddNextNode((sampler, deltaTime) => vm.Speed < 300, walkNode);

        var animationGraph = new AnimationGraph(model.Skeleton, idleNode);

        model.AnimationSampler = animationGraph;

        model.RotationDegrees = new Vector3(0, -180, 0);

        aura3Dview.AddNode(model);

        aura3Dview.MainCamera.FitToBoundingBox(model.BoundingBox);
        
    }
}