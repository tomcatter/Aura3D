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
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class AnimationFeaturesPage : UserControl
{
    public AnimationFeaturesPage()
    {
        InitializeComponent();

        // Wire up loop mode radio buttons
        loopRadio.IsChecked = true;
        loopRadio.IsCheckedChanged += (s, e) =>
        {
            if (s is RadioButton rb && rb.IsChecked == true)
            {
                if (DataContext is AnimationFeaturesViewModel v) v.LoopModeIndex = 0;
                ApplyLoopMode(LoopMode.Loop);
            }
        };
        onceRadio.IsCheckedChanged += (s, e) =>
        {
            if (s is RadioButton rb && rb.IsChecked == true)
            {
                if (DataContext is AnimationFeaturesViewModel v) v.LoopModeIndex = 1;
                ApplyLoopMode(LoopMode.Once);
            }
        };
        pingPongRadio.IsCheckedChanged += (s, e) =>
        {
            if (s is RadioButton rb && rb.IsChecked == true)
            {
                if (DataContext is AnimationFeaturesViewModel v) v.LoopModeIndex = 2;
                ApplyLoopMode(LoopMode.PingPong);
            }
        };

        // Wire up Reset button
        resetButton.Click += (s, e) =>
        {
            animationSampler?.Reset();
            UpdatePlaybackStatus();
        };

        // Wire up External Update checkbox
        externalUpdateCheck.IsCheckedChanged += (s, e) => OnExternalUpdateChanged();

        // Wire up speed slider
        speedSlider.ValueChanged += (s, e) =>
        {
            if (animationSampler != null)
                animationSampler.TimeScale = (float)((Slider)s!).Value;
        };

        // Wire up animation combo
        animationCombo.SelectionChanged += (s, e) =>
        {
            if (model == null || animations.Count == 0)
                return;
            if (DataContext is AnimationFeaturesViewModel v2 && !string.IsNullOrEmpty(v2.SelectedAnimation))
            {
                SwitchToAnimation(v2.SelectedAnimation);
            }
        };

        // Demo mode switching
        demoModeCombo.SelectionChanged += (s, e) =>
        {
            if (model == null)
                return;
            if (DataContext is AnimationFeaturesViewModel v3)
            {
                SwitchDemoMode(v3.DemoModeIndex);
            }
        };

        // Graph speed slider
        graphSpeedSlider.ValueChanged += (s, e) =>
        {
            if (DataContext is AnimationFeaturesViewModel v4)
                v4.GraphSpeed = (float)((Slider)s!).Value;
        };

        // Blend space sliders
        blendXSlider.ValueChanged += (s, e) =>
        {
            if (DataContext is AnimationFeaturesViewModel v5)
                v5.BlendX = ((Slider)s!).Value;
        };
        blendYSlider.ValueChanged += (s, e) =>
        {
            if (DataContext is AnimationFeaturesViewModel v6)
                v6.BlendY = ((Slider)s!).Value;
        };
    }

    // ─── Fields ────────────────────────────────────────────────

    List<Animation> animations = [];
    Model? model;
    AnimationSampler? animationSampler;
    AnimationGraph? animationGraph;
    AnimationBlendSpace? animationBlendSpace;

    // ─── Scene Initialization ─────────────────────────────────

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        if (sender is not Aura3DView view)
            return;

        // === Load SK_Mannequin model via Assimp (no animations embedded) ===
        using (var stream = AssetLoader.Open(new Uri("avares://Example/Assets/Models/SK_Mannequin.FBX")))
        {
            model = AssimpLoader.Load(stream, "fbx");
        }

        // === Load 7 external animation files ===
        string[] animFiles =
        [
            "Idle_Rifle_Hip.FBX",          // [0] Idle
            "Jog_Fwd_Rifle.FBX",           // [1] Jog Forward
            "Jog_Bwd_Rifle.FBX",           // [2] Jog Backward
            "Jog_Lt_Rifle.FBX",            // [3] Jog Left
            "Jog_Rt_Rifle.FBX",            // [4] Jog Right
            "AS_Rifle_WalkBwdLeft_Aim.FBX",// [5] Walk Back-Left
            "AS_Rifle_WalkBwdRight_Aim.FBX"// [6] Walk Back-Right
        ];

        foreach (var file in animFiles)
        {
            using var stream = AssetLoader.Open(
                new Uri($"avares://Example/Assets/Models/{file}"));
            animations.AddRange(
                AssimpLoader.LoadAnimations(stream, model.Skeleton, "fbx"));
        }

        if (DataContext is not AnimationFeaturesViewModel vm)
            return;

        // Populate animation name list
        vm.Animations.Clear();
        string[] displayNames =
        [
            "Idle (Rifle Hip)",
            "Jog Forward",
            "Jog Backward",
            "Jog Left",
            "Jog Right",
            "Walk Back-Left",
            "Walk Back-Right"
        ];
        for (int i = 0; i < animations.Count; i++)
        {
            vm.Animations.Add(displayNames[i]);
        }
        vm.SelectedAnimation = displayNames[0];

        // Position the model
        model.RotationDegrees = new Vector3(0, 180, 0);

        // === Create basic animation sampler (default mode) ===
        animationSampler = new AnimationSampler(animations[0])
        {
            TimeScale = 1.0f,
            LoopMode = LoopMode.Loop
        };
        model.AnimationSampler = animationSampler;

        // === Pre-build Animation Graph: Idle ↔ Jog Forward ===
        var graphVm = vm;
        var idleNode = new AnimationGraphNode(new AnimationSampler(animations[0]))
        {
            BlendTime = 0.5f
        };
        var jogNode = new AnimationGraphNode(new AnimationSampler(animations[1]))
        {
            BlendTime = 0.3f
        };

        idleNode.AddNextNode((sampler, dt) => graphVm.GraphSpeed > 150f, jogNode);
        jogNode.AddNextNode((sampler, dt) => graphVm.GraphSpeed < 150f, idleNode);

        animationGraph = new AnimationGraph(model.Skeleton, idleNode);

        // === Pre-build Blend Space with all 7 animations ===
        animationBlendSpace = new AnimationBlendSpace(model.Skeleton)
        {
            IdwPower = 2f  // Inverse Distance Weighting exponent
        };

        animationBlendSpace.AddAnimationSampler(new Vector2(0, 0),      // Idle at center
            new AnimationSampler(animations[0]));
        animationBlendSpace.AddAnimationSampler(new Vector2(0, 1),      // Forward
            new AnimationSampler(animations[1]));
        animationBlendSpace.AddAnimationSampler(new Vector2(0, -1),     // Backward
            new AnimationSampler(animations[2]));
        animationBlendSpace.AddAnimationSampler(new Vector2(-1, 0),     // Left
            new AnimationSampler(animations[3]));
        animationBlendSpace.AddAnimationSampler(new Vector2(1, 0),      // Right
            new AnimationSampler(animations[4]));
        animationBlendSpace.AddAnimationSampler(new Vector2(-1, -1),    // Diagonal: Back-Left
            new AnimationSampler(animations[5]));
        animationBlendSpace.AddAnimationSampler(new Vector2(1, -1),     // Diagonal: Back-Right
            new AnimationSampler(animations[6]));

        // Compute initial blend pose to avoid T-pose on first frame
        animationBlendSpace.InitializePose();

        // === Lighting ===
        var dl = new DirectionalLight
        {
            RotationDegrees = new Vector3(-30, -60, 0),
            LightColor = Color.White,
            CastShadow = true
        };
        dl.ShadowConfig.FarPlane = 1000;
        dl.ShadowConfig.NearPlane = 10;
        dl.ShadowConfig.Width = 500;
        dl.ShadowConfig.Height = 500;
        view.AddNode(dl);

        // === Ground plane ===
        var mesh = new Mesh
        {
            Geometry = new Aura3D.Core.Geometries.PlaneGeometry(400, 400),
            Material = new Material
            {
                Channels = [
                    new Channel()
                    {
                        Name = "BaseColor",
                        Texture = Texture.CreateFromColor(Color.Gray)
                    }
                ]
            }
        };
        view.AddNode(mesh);

        // === Add model to scene ===
        view.AddNode(model);

        view.MainCamera.FitToBoundingBox(model.BoundingBox, 0.5f);
        view.MainCamera.Position += view.MainCamera.Up * 100;
        view.MainCamera.RotationDegrees = new Vector3(-30, 0, 0);

        UpdatePlaybackStatus();
    }

    // ─── Per-Frame Update ────────────────────────────────────

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        if (model == null || DataContext is not AnimationFeaturesViewModel vm)
            return;

        // Handle ExternalUpdate for basic mode
        if (vm.IsBasicMode && vm.IsExternalUpdate && animationSampler != null)
        {
            animationSampler.Update(e.DeltaTime);
        }

        // Handle ExternalUpdate for Graph mode
        if (vm.IsGraphMode && vm.IsExternalUpdate && animationGraph != null)
        {
            animationGraph.Update(e.DeltaTime);
        }

        // Handle ExternalUpdate for BlendSpace mode
        if (vm.IsBlendSpaceMode && vm.IsExternalUpdate && animationBlendSpace != null)
        {
            animationBlendSpace.Update(e.DeltaTime);
        }

        // Update blend space axis each frame
        if (vm.IsBlendSpaceMode && animationBlendSpace != null)
        {
            animationBlendSpace.SetAxis((float)vm.BlendX, (float)vm.BlendY);
        }
    }

    // ─── Core Operations ─────────────────────────────────────

    private void SwitchToAnimation(string animName)
    {
        if (model == null || DataContext is not AnimationFeaturesViewModel vm)
            return;

        var target = animations.FirstOrDefault(a =>
        {
            // Match by display name → index
            var idx = vm.Animations.IndexOf(animName);
            return idx >= 0 && idx < animations.Count &&
                   animations[idx] == a;
        });

        // Fall back to index-based lookup
        var displayIdx = vm.Animations.IndexOf(animName);
        if (displayIdx < 0 || displayIdx >= animations.Count)
            return;
        target = animations[displayIdx];

        var currentSpeed = animationSampler?.TimeScale ?? 1.0f;
        var currentLoop = animationSampler?.LoopMode ?? LoopMode.Loop;

        animationSampler = new AnimationSampler(target)
        {
            TimeScale = currentSpeed,
            LoopMode = currentLoop,
            ExternalUpdate = vm.IsExternalUpdate
        };

        if (vm.IsBasicMode)
        {
            model.AnimationSampler = animationSampler;
        }

        UpdatePlaybackStatus();
    }

    private void ApplyLoopMode(LoopMode mode)
    {
        if (animationSampler != null)
            animationSampler.LoopMode = mode;
        UpdatePlaybackStatus();
    }

    private void SwitchDemoMode(int modeIndex)
    {
        if (model == null || DataContext is not AnimationFeaturesViewModel vm)
            return;

        switch (modeIndex)
        {
            case 0: // Basic
                if (animationSampler == null)
                {
                    var currentAnim = animations[0];
                    animationSampler = new AnimationSampler(currentAnim)
                    {
                        TimeScale = (float)vm.Speed,
                        LoopMode = (LoopMode)vm.LoopModeIndex,
                        ExternalUpdate = vm.IsExternalUpdate
                    };
                }
                model.AnimationSampler = animationSampler;
                break;

            case 1: // Graph
                animationGraph?.Reset();
                model.AnimationSampler = animationGraph!;
                break;

            case 2: // BlendSpace
                animationBlendSpace?.Reset();
                model.AnimationSampler = animationBlendSpace!;
                break;
        }
    }

    private void OnExternalUpdateChanged()
    {
        if (DataContext is not AnimationFeaturesViewModel vm)
            return;

        var external = vm.IsExternalUpdate;

        if (animationSampler != null)
            animationSampler.ExternalUpdate = external;
        if (animationGraph != null)
            animationGraph.ExternalUpdate = external;
        if (animationBlendSpace != null)
            animationBlendSpace.ExternalUpdate = external;

        UpdatePlaybackStatus();
    }

    private void UpdatePlaybackStatus()
    {
        if (DataContext is not AnimationFeaturesViewModel vm)
            return;

        var loopStr = (animationSampler?.LoopMode ?? LoopMode.Loop) switch
        {
            LoopMode.Loop => "Loop",
            LoopMode.Once => "Once",
            LoopMode.PingPong => "PingPong",
            _ => "Unknown"
        };

        var speed = animationSampler?.TimeScale ?? 1.0f;
        var external = vm.IsExternalUpdate ? "Manual Update" : "Auto (System Clock)";

        vm.PlaybackStatus = $"Mode: {loopStr} | Speed: {speed:F1}× | {external}";
    }
}
