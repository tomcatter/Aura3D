using Aura3D.Avalonia;
using Aura3D.Core.Geometries;
using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia.Controls;
using Avalonia.Platform;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class SkinnedMeshCullingPage : UserControl
{
    private CameraController _cameraController = null!;
    private List<Model> _soldiers = [];
    private List<double> _deltaTimes = [];
    private Animation? _soldierAnimation;
    private Model? _sourceModel;
    private BoundingBox? _soldierBounds;

    // FPS tracking
    private int _fpsMin = int.MaxValue;
    private int _fpsMax;
    private long _fpsSum;
    private int _fpsFrameCount;

    public SkinnedMeshCullingPage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3DView) { MoveSpeed = 10f };

        buildButton.Click += (s, e) => BuildSoldiers();
    }

    // ─── Scene Init ──────────────────────────────────────────

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        aura3DView.AutoRequestNextFrameRendering = false;

        if (DataContext is SkinnedMeshCullingViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(SkinnedMeshCullingViewModel.EnableFrustumCulling):
                        ApplyCulling();
                        break;
                    case nameof(SkinnedMeshCullingViewModel.ShowBoundingBox):
                        ApplyBoundingBox();
                        break;
                    case nameof(SkinnedMeshCullingViewModel.BoundingBoxPadding):
                        ApplyBoundingBoxPadding();
                        break;
                }
            };
        }

        // ── Lighting ──
        var dl = new DirectionalLight
        {
            RotationDegrees = new Vector3(-40, -30, 0),
            LightColor = Color.White,
            CastShadow = true
        };
        dl.ShadowConfig.FarPlane = 200;
        dl.ShadowConfig.NearPlane = 1;
        dl.ShadowConfig.Width = 200;
        dl.ShadowConfig.Height = 200;
        aura3DView.AddNode(dl);

        // ── Load Soldier.glb (with embedded animations if any) ──
        using (var stream = AssetLoader.Open(
            new Uri("avares://Example/Assets/Models/Soldier.glb")))
        {
            (_sourceModel, var animations) = ModelLoader.LoadGlbModelAndAnimations(stream);
            if (animations.Count > 0)
            {
                _soldierAnimation = animations[0];
            }
        }

        // ── Ground plane (sized relative to soldier) ──
        _soldierBounds = _sourceModel.BoundingBox;
        float groundSize = _soldierBounds != null
            ? MathF.Max(_soldierBounds.Size.X, _soldierBounds.Size.Z) * 10f
            : 100f;

        var ground = new Mesh
        {
            Name = "Ground",
            Geometry = new PlaneGeometry(groundSize, groundSize),
            Material = new Material
            {
                Channels = [
                    new Channel
                    {
                        Name = "BaseColor",
                        Texture = Texture.CreateFromColor(Color.DarkGray)
                    }
                ]
            }
        };
        aura3DView.AddNode(ground);

        // ── Camera ──
        if (_soldierBounds != null)
        {
            aura3DView.MainCamera.FitToBoundingBox(_soldierBounds, padding: 0.3f);
            
            aura3DView.MainCamera.FarPlane = 500;
        }
        else
        {
            aura3DView.MainCamera.Position = new Vector3(0, 5, 15);
            aura3DView.MainCamera.RotationDegrees = new Vector3(-15, 0, 0);
        }

        // ── Initial build ──
        BuildSoldiers();

        aura3DView.RequestNextFrameRendering();
    }

    // ─── Per-Frame Update ────────────────────────────────────

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        // FPS
        if (_deltaTimes.Count >= 30) _deltaTimes.RemoveAt(0);
        _deltaTimes.Add(args.DeltaTime);
        var fps = (int)(1 / _deltaTimes.Average());

        if (_fpsFrameCount == 0)
        {
            _fpsMin = fps;
            _fpsMax = fps;
            _fpsSum = 0;
        }
        if (fps < _fpsMin) _fpsMin = fps;
        if (fps > _fpsMax) _fpsMax = fps;
        _fpsSum += fps;
        _fpsFrameCount++;

        if (DataContext is SkinnedMeshCullingViewModel vm)
        {
            vm.CurrentFps = fps;
            vm.MinFps = _fpsMin;
            vm.MaxFps = _fpsMax;
            vm.AvgFps = (int)(_fpsSum / _fpsFrameCount);

            double barRatio = Math.Clamp(fps / 200.0, 0.0, 1.0);
            vm.FpsBarWidth = barRatio * 200;
            vm.FpsBarColor = fps switch
            {
                < 30 => "#FF6B6B",
                < 60 => "#FFD93D",
                _ => "#6BCB77"
            };

            vm.TotalSoldiers = _soldiers.Count;

            vm.DetailText = _soldierAnimation != null
                ? $"{_soldiers.Count} animated soldiers | Culling: {(vm.EnableFrustumCulling ? "ON" : "OFF")}"
                : $"{_soldiers.Count} soldiers (no animation) | Culling: {(vm.EnableFrustumCulling ? "ON" : "OFF")}";
        }

        // Count visible skinned models (not individual meshes)
        var visible = aura3DView.Scene?.RenderPipeline.VisibleMeshesInCamera;
        if (visible != null && DataContext is SkinnedMeshCullingViewModel vm2)
        {
            var visibleModels = new HashSet<Model>();
            foreach (var m in visible)
            {
                if (m.IsSkinnedMesh && m.Model != null)
                    visibleModels.Add(m.Model);
            }
            vm2.VisibleSoldiers = visibleModels.Count;
        }

        aura3DView.RequestNextFrameRendering();
    }

    // ─── Build Soldiers ──────────────────────────────────────

    private void BuildSoldiers()
    {
        if (_sourceModel == null) return;
        if (DataContext is not SkinnedMeshCullingViewModel vm) return;

        ClearSoldiers();

        // Reset FPS tracking
        _deltaTimes.Clear();
        _fpsFrameCount = 0;
        _fpsMin = int.MaxValue;
        _fpsMax = 0;

        int count = vm.SoldierCount;
        if (count <= 0) count = 25;

        // Spacing based on soldier bounding box size
        var bb = _soldierBounds;
        float spacing = bb != null
            ? MathF.Max(bb.Size.X, bb.Size.Z) * 1.5f
            : 3f;

        // 2D grid on XZ plane
        int gridSize = (int)Math.Ceiling(Math.Sqrt(count));
        float half = (gridSize - 1) * spacing / 2f;

        for (int i = 0; i < count; i++)
        {
            int row = i / gridSize;
            int col = i % gridSize;

            float x = col * spacing - half;
            float z = row * spacing - half;

            var clone = _sourceModel.Clone();

            if (_soldierAnimation != null)
            {
                var sampler = new AnimationSampler(_soldierAnimation)
                {
                    TimeScale = 1.0f,
                    LoopMode = LoopMode.Loop
                };
                clone.AnimationSampler = sampler;
            }

            clone.Position = new Vector3(x, 0, z);
            clone.RotationDegrees = new Vector3(0, 180 + (i % 4) * 90, 0);

            // Apply bounding box padding
            clone.BoundingBoxPadding = vm.BoundingBoxPadding;

            aura3DView.AddNode(clone);
            _soldiers.Add(clone);
        }

        ApplyCulling();
        aura3DView.RequestNextFrameRendering();
    }

    private void ClearSoldiers()
    {
        foreach (var soldier in _soldiers)
        {
            aura3DView.Remove(soldier);
        }
        _soldiers.Clear();
    }

    private void ApplyCulling()
    {
        if (aura3DView.Scene?.RenderPipeline == null) return;
        if (DataContext is SkinnedMeshCullingViewModel vm)
        {
            aura3DView.Scene.RenderPipeline.EnableFrustumCulling = vm.EnableFrustumCulling;
        }
    }

    private void ApplyBoundingBox()
    {
        if (aura3DView.Scene?.RenderPipeline == null) return;
        if (DataContext is SkinnedMeshCullingViewModel vm)
        {
            aura3DView.Scene.RenderPipeline.Settings.ShowBoundingBox = vm.ShowBoundingBox;
        }
    }

    private void ApplyBoundingBoxPadding()
    {
        if (DataContext is not SkinnedMeshCullingViewModel vm) return;
        foreach (var soldier in _soldiers)
        {
            soldier.BoundingBoxPadding = vm.BoundingBoxPadding;
            // 强制刷新包围盒以应用新 padding
            foreach (var mesh in soldier.Meshes)
                mesh.UpdateWorldBoundingBox();
        }
    }
}
