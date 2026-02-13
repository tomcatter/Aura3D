using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class BlendSpacePage : UserControl
{
    AnimationBlendSpace? animationBlendSpace = null;
    public BlendSpacePage()
    {
        InitializeComponent();
    }

    List<Animation> animations = [];

    Model? model = null;

    private void aura3Dview_SceneInitialized(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var aura3DView = sender as Aura3DView;
        if (aura3DView == null)
            return;
        var dl = new DirectionalLight();

        dl.RotationDegrees = new Vector3(-30, -60, 0);

        dl.LightColor = Color.White;

        dl.CastShadow = true;

        dl.ShadowConfig.FarPlane = 1000;

        dl.ShadowConfig.NearPlane = 10;

        dl.ShadowConfig.Width = 500;

        dl.ShadowConfig.Height = 500;

        


        aura3DView.AddNode(dl);

        if (model == null)
        {
            using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/SK_Mannequin.FBX")))
            {
                model = AssimpLoader.Load(stream, "fbx");
            }
        }

        if (animations.Count == 0)
        {
            using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/Idle_Rifle_Hip.FBX")))
            {
                animations.AddRange(AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx"));
            }
            using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/Jog_Fwd_Rifle.FBX")))
            {
                animations.AddRange(AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx"));
            }


            using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/Jog_Bwd_Rifle.FBX")))
            {
                animations.AddRange(AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx"));
            }

            using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/Jog_Lt_Rifle.FBX")))
            {
                animations.AddRange(AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx"));
            }

            using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/Jog_Rt_Rifle.FBX")))
            {
                animations.AddRange(AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx"));
            }
            using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/AS_Rifle_WalkBwdLeft_Aim.FBX")))
            {
                animations.AddRange(AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx"));
            }
            using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/AS_Rifle_WalkBwdRight_Aim.FBX")))
            {
                animations.AddRange(AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx"));
            }
        }


        animationBlendSpace = new AnimationBlendSpace(model.Skeleton);

        animationBlendSpace.AddAnimationSampler(new(0, 0), new AnimationSampler(animations.First()));

        animationBlendSpace.AddAnimationSampler(new(0, 1), new AnimationSampler(animations.Skip(1).First()));

        animationBlendSpace.AddAnimationSampler(new(0, -1), new AnimationSampler(animations.Skip(2).First()));

        animationBlendSpace.AddAnimationSampler(new(-1, 0), new AnimationSampler(animations.Skip(3).First()));

        animationBlendSpace.AddAnimationSampler(new(1, 0), new AnimationSampler(animations.Skip(4).First()));

        animationBlendSpace.AddAnimationSampler(new(-1, -1), new AnimationSampler(animations.Skip(5).First()));

        animationBlendSpace.AddAnimationSampler(new(1, -1), new AnimationSampler(animations.Skip(6).First()));

        model.AnimationSampler = animationBlendSpace;
        
        aura3Dview.AddNode(model);

        aura3Dview.MainCamera.FitToBoundingBox(model.BoundingBox, 0.5f);

        aura3Dview.MainCamera.ClearColor = Color.FromArgb(255, 100, 100, 100);

        aura3Dview.MainCamera.Position += aura3Dview.MainCamera.Up * 100 ;

        aura3DView.MainCamera.RotationDegrees = new Vector3(-30, 0, 0);

        var mesh = new Mesh();

        mesh.Geometry = new PlaneGeometry(400, 400);

        mesh.Material = new Material
        {
            Channels = [
            new Channel(){
                Name = "BaseColor",
                Texture = Texture.CreateFromColor(Color.Blue),
            }
             ]
        };

        aura3DView.AddNode(mesh);

        dl.Position = aura3Dview.MainCamera.Position;

    }

    private void aura3Dview_SceneUpdated(object? sender, Aura3D.Avalonia.UpdateRoutedEventArgs e)
    {
        var vm = DataContext as BlendSpaceViewModel;
        if (vm == null)
            return;
        if (animationBlendSpace == null)
            return;
        animationBlendSpace.SetAxis((float)vm.X, (float)vm.Y);
    }
}