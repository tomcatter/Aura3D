using Aura3D.Avalonia;
using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Particles;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class ParticleEditorPage : UserControl
{
    private CameraController _cameraController = null!;
    private ParticleEditorViewModel? _vm;
    private ParticleSystem? _ps;
    private readonly List<double> _deltaTimes = [];
    private int _fpsMin = int.MaxValue, _fpsMax, _fpsFrameCount;
    private long _fpsSum;
    private bool _suppressRebuild;

    public ParticleEditorPage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3DView) { MoveSpeed = 10f };
    }

    private void SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        var view = aura3DView;
        view.AutoRequestNextFrameRendering = false;

        _vm = DataContext as ParticleEditorViewModel;
        if (_vm == null) return;

        _vm.PropertyChanged += OnSystemPropertyChanged;

        foreach (var evm in _vm.Emitters)
            evm.PropertyChanged += OnEmitterPropertyChanged;

        _vm.Emitters.CollectionChanged += OnEmittersCollectionChanged;

        view.MainCamera.Position = new Vector3(0, 5, 15);
        view.MainCamera.LookAt(new Vector3(0, 2, 0));

        BuildParticleSystem();
        view.RequestNextFrameRendering();
    }

    private void OnSystemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suppressRebuild || _vm == null || _ps == null) return;

        switch (e.PropertyName)
        {
            case nameof(ParticleEditorViewModel.PosX):
            case nameof(ParticleEditorViewModel.PosY):
            case nameof(ParticleEditorViewModel.PosZ):
                _ps.Position = new Vector3(_vm.PosX, _vm.PosY, _vm.PosZ);
                break;
        }
    }

    private void OnEmitterPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suppressRebuild) return;
        RebuildParticleSystem();
    }

    private void OnEmittersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (EmitterViewModel evm in e.NewItems)
                evm.PropertyChanged += OnEmitterPropertyChanged;

        if (e.OldItems != null)
            foreach (EmitterViewModel evm in e.OldItems)
                evm.PropertyChanged -= OnEmitterPropertyChanged;

        BuildParticleSystem();
    }

    private void BuildParticleSystem()
    {
        if (_vm == null) return;
        var view = aura3DView;

        _ps?.Stop();
        if (_ps != null) view.Remove(_ps);

        _ps = new ParticleSystem
        {
            Name = "ParticleEditor",
            MaxParticles = _vm.Emitters.Count > 0 ? _vm.Emitters.Sum(e => e.MaxParticles) : 10000,
            Position = new Vector3(_vm.PosX, _vm.PosY, _vm.PosZ),
        };

        ApplyAllEmitters(_ps.Emitters);
        view.AddNode(_ps);
        _ps.Play();
    }

    private void RebuildParticleSystem()
    {
        if (_vm == null || _ps == null) return;

        // Stop first so old InstancedMesh children are properly removed
        _ps.Stop();

        _ps.Emitters.Clear();
        ApplyAllEmitters(_ps.Emitters);

        _ps.MaxParticles = _vm.Emitters.Count > 0 ? _vm.Emitters.Sum(e => e.MaxParticles) : 10000;
        _ps.Position = new Vector3(_vm.PosX, _vm.PosY, _vm.PosZ);
        _ps.Play();
    }

    private void ApplyAllEmitters(List<ParticleEmitter> emitters)
    {
        if (_vm == null) return;
        emitters.Clear();
        foreach (var evm in _vm.Emitters)
            emitters.Add(CreateEmitterFromViewModel(evm));
    }

    private ParticleEmitter CreateEmitterFromViewModel(EmitterViewModel evm)
    {
        var em = new ParticleEmitter
        {
            BlendMode = BlendMode.Translucent,
            EmissionRate = evm.EmissionRate,
            Shape = (EmissionShape)evm.ShapeIndex,
            ShapeSize = new Vector3(evm.ShapeSizeX, evm.ShapeSizeY, evm.ShapeSizeZ),
            ConeAngle = evm.ConeAngle,
            MaxParticles = evm.MaxParticles,
            Lifetime = new RangeFloat(evm.LifetimeMin, evm.LifetimeMax),
            Velocity = new RangeVector3(
                new(evm.VelocityXMin, evm.VelocityYMin, evm.VelocityZMin),
                new(evm.VelocityXMax, evm.VelocityYMax, evm.VelocityZMax)),
            StartSize = new RangeFloat(evm.StartSizeMin, evm.StartSizeMax),
            EndSize = new RangeFloat(evm.EndSizeMin, evm.EndSizeMax),
            StartColor = Color.FromArgb(evm.StartColorA, evm.StartColorR, evm.StartColorG, evm.StartColorB),
            EndColor = Color.FromArgb(evm.EndColorA, evm.EndColorR, evm.EndColorG, evm.EndColorB),
            Rotation = new RangeFloat(evm.RotationMin, evm.RotationMax),
            AngularVelocity = new RangeFloat(evm.AngularVelocityMin, evm.AngularVelocityMax),
            Gravity = new Vector3(0, evm.GravityY, 0),
            Damping = evm.Damping,
        };

        // Billboard mode
        if (evm.IsBillboard)
        {
            em.Texture = evm.LoadedTexture;
            em.FlipbookTiles = new Vector2(evm.FlipbookTilesX, evm.FlipbookTilesY);
        }
        // Mesh mode
        else if (evm.IsMesh)
        {
            em.Mesh = evm.LoadedMesh;
            em.MeshScale = evm.MeshScale;
        }

        return em;
    }

    private void EmitterBar_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is EmitterViewModel evm && _vm != null)
            _vm.SelectedEmitter = evm;
    }

    private async void PickTexture_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not EmitterViewModel evm) return;

        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel == null) return;

        var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Texture",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image files") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga"] },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ]
        });

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (path == null) return;

        try
        {
            using var stream = File.OpenRead(path);
            var texture = TextureLoader.LoadTexture(stream);
            evm.LoadedTexture = texture;
            evm.TexturePath = Path.GetFileName(path);
            RebuildParticleSystem();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load texture: {ex.Message}");
        }
    }

    private async void PickMesh_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not EmitterViewModel evm) return;

        var toplevel = TopLevel.GetTopLevel(this);
        if (toplevel == null) return;

        var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Mesh",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("GLTF/GLB") { Patterns = ["*.glb", "*.gltf"] },
                new FilePickerFileType("FBX") { Patterns = ["*.fbx"] },
                new FilePickerFileType("OBJ") { Patterns = ["*.obj"] },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ]
        });

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (path == null) return;

        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            Model? model;
            if (ext == ".glb" || ext == ".gltf")
                model = await System.Threading.Tasks.Task.Run(() => ModelLoader.LoadGlbModel(path));
            else
                model = await System.Threading.Tasks.Task.Run(() => AssimpLoader.Load(path));

            // Use the first mesh from the model (recursive search)
            var mesh = model.Meshes.FirstOrDefault();
            if (mesh != null)
            {
                evm.LoadedMesh = mesh;
                evm.MeshPath = Path.GetFileName(path);
                RebuildParticleSystem();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load mesh: {ex.Message}");
        }
    }

    private void SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        if (_vm == null) return;

        if (_deltaTimes.Count >= 30) _deltaTimes.RemoveAt(0);
        _deltaTimes.Add(args.DeltaTime);
        var fps = (int)(1 / _deltaTimes.Average());

        if (_fpsFrameCount == 0) { _fpsMin = fps; _fpsMax = fps; _fpsSum = 0; }
        if (fps < _fpsMin) _fpsMin = fps;
        if (fps > _fpsMax) _fpsMax = fps;
        _fpsSum += fps; _fpsFrameCount++;

        _vm.CurrentFps = fps;
        _vm.MinFps = _fpsMin;
        _vm.MaxFps = _fpsMax;
        _vm.AvgFps = (int)(_fpsSum / _fpsFrameCount);
        _vm.FpsBarWidth = Math.Clamp(fps / 200.0, 0, 1) * 200;
        _vm.FpsBarColor = fps switch { < 30 => "#FF6B6B", < 60 => "#FFD93D", _ => "#6BCB77" };

        var pc = _ps?.ActiveCount ?? 0;
        _vm.DetailText = $"Particles: {pc}  |  Emitters: {_vm.Emitters.Count}  |  Pos: ({_vm.PosX:F1}, {_vm.PosY:F1}, {_vm.PosZ:F1})";

        aura3DView.RequestNextFrameRendering();
    }
}
