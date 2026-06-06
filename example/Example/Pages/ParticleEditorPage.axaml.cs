using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Particles;
using Aura3D.Core.Resources;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
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

        _vm.PropertyChanged += OnPropertyChanged;

        view.MainCamera.Position = new Vector3(0, 5, 15);
        view.MainCamera.LookAt(new Vector3(0, 2, 0));

        BuildParticleSystem();
        view.RequestNextFrameRendering();
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suppressRebuild) return;
        if (_vm == null || _ps == null) return;

        // Rebuild on any parameter change
        RebuildParticleSystem();
    }

    private void BuildParticleSystem()
    {
        if (_vm == null) return;
        var view = aura3DView;

        _ps?.Stop();
        if (_ps != null) view.Remove(_ps);

        _ps = new ParticleSystem
        {
            Name = "EditorEmitter",
            MaxParticles = _vm.MaxParticles,
            MaxInstancesPerGroup = 2048,
            BlendMode = BlendMode.Translucent,
            Position = new Vector3(_vm.PosX, _vm.PosY, _vm.PosZ),
        };

        ApplyEmitter(_ps.Emitters);
        view.AddNode(_ps);
        _ps.Play();
    }

    private void RebuildParticleSystem()
    {
        if (_vm == null || _ps == null) return;

        _ps.Emitters.Clear();
        ApplyEmitter(_ps.Emitters);

        if (_ps.ActiveCount == 0)
        {
            // If no particles yet, stop and replay
            _ps.Stop();
            _ps.MaxParticles = _vm.MaxParticles;
            _ps.Position = new Vector3(_vm.PosX, _vm.PosY, _vm.PosZ);
            _ps.Play();
        }
    }

    private void ApplyEmitter(List<ParticleEmitter> emitters)
    {
        if (_vm == null) return;
        var em = new ParticleEmitter
        {
            EmissionRate = _vm.EmissionRate,
            Shape = (EmissionShape)_vm.ShapeIndex,
            ShapeSize = new Vector3(_vm.ShapeSizeX, _vm.ShapeSizeY, _vm.ShapeSizeZ),
            ConeAngle = _vm.ConeAngle,
            Lifetime = new RangeFloat(_vm.LifetimeMin, _vm.LifetimeMax),
            Velocity = new RangeVector3(
                new(_vm.VelocityXMin, _vm.VelocityYMin, _vm.VelocityZMin),
                new(_vm.VelocityXMax, _vm.VelocityYMax, _vm.VelocityZMax)),
            StartSize = new RangeFloat(_vm.StartSizeMin, _vm.StartSizeMax),
            EndSize = new RangeFloat(_vm.EndSizeMin, _vm.EndSizeMax),
            StartColor = Color.FromArgb(_vm.StartColorA, _vm.StartColorR, _vm.StartColorG, _vm.StartColorB),
            EndColor = Color.FromArgb(_vm.EndColorA, _vm.EndColorR, _vm.EndColorG, _vm.EndColorB),
            Rotation = new RangeFloat(_vm.RotationMin, _vm.RotationMax),
            AngularVelocity = new RangeFloat(_vm.AngularVelocityMin, _vm.AngularVelocityMax),
            Gravity = new Vector3(0, _vm.GravityY, 0),
            Damping = _vm.Damping,
            BlendMode = BlendMode.Translucent,
        };
        emitters.Add(em);
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
        var gc = _ps?.GroupCount ?? 0;
        _vm.DetailText = $"Particles: {pc}  |  Groups: {gc}  |  Pos: ({_vm.PosX:F1}, {_vm.PosY:F1}, {_vm.PosZ:F1})";

        aura3DView.RequestNextFrameRendering();
    }
}
