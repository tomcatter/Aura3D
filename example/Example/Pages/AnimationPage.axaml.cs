using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;

namespace Example.Pages;

public partial class AnimationPage : UserControl
{
    public AnimationPage()
    {
        InitializeComponent();
    }
    List<Animation> animations = [];
    Model? model;
    AnimationSampler? animationSampler;
    private async void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        if (sender is Aura3DView view == false)
            return;
        (model, animations) = await Task.Run(() =>
       {
           using (var stream = AssetLoader.Open(new Uri($"avares://Example/Assets/Models/Soldier.glb")))
           {
               return ModelLoader.LoadGlbModelAndAnimations(stream);
           }
       });

        if (DataContext is AnimationViewModel vm)
        {
            vm.Animations.Clear();
            foreach (var anim in animations)
            {
                vm.Animations.Add(anim.Name);
            }
            vm.SelectedAnimation = animations[0].Name;


            var modelPosition = view.MainCamera.Position + view.MainCamera.Forward * 2;

            model.Position = modelPosition;

            model.Position = modelPosition - model.Up * 1;

            model.RotationDegrees = new Vector3(0, 180, 0);
            animationSampler = new AnimationSampler(animations[0]);
            animationSampler.TimeScale = (float)vm.Speed;

            model.AnimationSampler = animationSampler;

            view.AddNode(model);


            var dl = new DirectionalLight();

            dl.RotationDegrees = new Vector3(-30, 0, 0);

            dl.LightColor = Color.White;

            view.AddNode(dl);
        }
    }

    private void animationList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (model == null)
            return;
        if (DataContext is AnimationViewModel vm)
        {
            foreach (var animation in animations)
            {
                if (animation.Name == vm.SelectedAnimation)
                {
                    animationSampler = new AnimationSampler(animation);
                    animationSampler.TimeScale = (float)vm.Speed;
                    model.AnimationSampler = animationSampler;
                }
            }
        }
    }

    private void slider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (animationSampler== null)
            return;
        if (DataContext is AnimationViewModel vm)
        {
            animationSampler.TimeScale = (float)vm.Speed;
        }
    }
}