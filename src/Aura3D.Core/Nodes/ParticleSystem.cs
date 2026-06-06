using Aura3D.Core.Particles;
using Aura3D.Core.Resources;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// High-performance particle system node using CPU simulation + GPU instanced billboard quads.
/// </summary>
public class ParticleSystem : Node
{
    public int MaxParticles
    {
        get => _maxParticles;
        set { if (!_isPlaying) _maxParticles = value; }
    }

    public int MaxInstancesPerGroup { get; set; } = 2048;
    public List<ParticleEmitter> Emitters { get; } = new();
    public BlendMode BlendMode { get; set; } = BlendMode.Translucent;
    public ITexture? ParticleTexture { get; set; }
    public Vector2 FlipbookTiles { get; set; } = Vector2.One;
    public bool IsPlaying => _isPlaying;
    public int ActiveCount => _activeCount;
    public int GroupCount => _renderGroups.Count;

    public void Play()
    {
        if (_isPlaying) return;
        _particles = new ParticleData[_maxParticles];
        _activeCount = 0;
        _isPlaying = true;
        _isPaused = false;
        _rng = new Random();
        foreach (var e in Emitters) { e.ElapsedTime = 0; e.EmissionAccumulator = 0; }
        _needsRebuild = true;
    }

    public void Stop()
    {
        _isPlaying = false; _isPaused = false; _activeCount = 0;
        _particles = Array.Empty<ParticleData>();
        DestroyRenderGroups();
    }

    public void Pause() => _isPaused = !_isPaused;

    public override void Update(double delta)
    {
        base.Update(delta);
        if (!_isPlaying || _isPaused) return;
        float dt = (float)delta;

        ParticleSimulation.Update(_particles!, ref _activeCount, _maxParticles, Emitters, dt, _rng, WorldTransform.Translation);

        if (_needsRebuild) { RebuildRenderGroups(); _needsRebuild = false; }
        else TryIncrementalUpdate();
    }

    public override List<IGpuResource> GetGpuResources()
    {
        var list = new List<IGpuResource>();
        foreach (var g in _renderGroups) list.AddRange(g.GetGpuResources());
        if (ParticleTexture is IGpuResource gr) list.Add(gr);
        return list;
    }

    private int _maxParticles = 10000;
    private ParticleData[] _particles = Array.Empty<ParticleData>();
    private int _activeCount;
    private bool _isPlaying, _isPaused, _needsRebuild;
    private Random _rng = new();
    private readonly List<InstancedMesh> _renderGroups = new();
    private readonly List<Matrix4x4> _transformsBuf = new();
    private readonly List<Vector4> _colorsBuf = new();
    private readonly List<float> _sizesBuf = new();
    private readonly List<float> _ageRatiosBuf = new();
    private int _lastRebuildActiveCount;

    private void RebuildRenderGroups()
    {
        DestroyRenderGroups();
        if (_activeCount == 0) return;

        var sharedGeo = ParticleRenderData.GetSharedBillboardGeometry();
        var material = CreateMaterial();
        int groupCount = (_activeCount + MaxInstancesPerGroup - 1) / MaxInstancesPerGroup;

        for (int g = 0; g < groupCount; g++)
        {
            int start = g * MaxInstancesPerGroup;
            int count = System.Math.Min(MaxInstancesPerGroup, _activeCount - start);

            _transformsBuf.Clear(); _colorsBuf.Clear(); _sizesBuf.Clear(); _ageRatiosBuf.Clear();
            for (int i = 0; i < count; i++)
            {
                ref var p = ref _particles[start + i];
                _transformsBuf.Add(Matrix4x4.CreateFromYawPitchRoll(0, 0, p.Rotation)
                                  * Matrix4x4.CreateTranslation(p.Position));
                _colorsBuf.Add(p.CurrentColor);
                _sizesBuf.Add(p.CurrentSize);
                _ageRatiosBuf.Add(p.AgeRatio);
            }

            var im = ParticleRenderData.CreateBillboardInstancedMesh(
                sharedGeo.DeepClone(), material?.DeepClone(), $"{Name}_G{g}");

            for (int i = 0; i < count; i++) im.AddInstance(_transformsBuf[i]);
            ParticleRenderData.SetParticleInstanceAttributes(im, _colorsBuf, _sizesBuf, _ageRatiosBuf);

            AddChild(im, AttachToParentRule.KeepWorld);
            _renderGroups.Add(im);
        }
        _lastRebuildActiveCount = _activeCount;
    }

    private void TryIncrementalUpdate()
    {
        if (_activeCount != _lastRebuildActiveCount) { _needsRebuild = true; return; }

        for (int g = 0; g < _renderGroups.Count; g++)
        {
            int start = g * MaxInstancesPerGroup;
            int count = System.Math.Min(MaxInstancesPerGroup, _activeCount - start);
            var group = _renderGroups[g];

            for (int i = 0; i < count; i++)
            {
                ref var p = ref _particles[start + i];
                group.UpdateInstance(i, Matrix4x4.CreateFromYawPitchRoll(0, 0, p.Rotation)
                                        * Matrix4x4.CreateTranslation(p.Position));
            }

            _colorsBuf.Clear(); _sizesBuf.Clear(); _ageRatiosBuf.Clear();
            for (int i = 0; i < count; i++)
            {
                ref var p = ref _particles[start + i];
                _colorsBuf.Add(p.CurrentColor);
                _sizesBuf.Add(p.CurrentSize);
                _ageRatiosBuf.Add(p.AgeRatio);
            }
            ParticleRenderData.SetParticleInstanceAttributes(group, _colorsBuf, _sizesBuf, _ageRatiosBuf);
        }
    }

    private void DestroyRenderGroups()
    {
        foreach (var g in _renderGroups)
        { if (_children.Contains(g)) RemoveChild(g, AttachToParentRule.KeepWorld); }
        _renderGroups.Clear();
        _needsRebuild = true;
    }

    private Material CreateMaterial()
    {
        var mat = new Material { BlendMode = BlendMode };
        if (ParticleTexture != null)
        {
            mat.SetParameterValue("uParticleTexture", ParticleTexture);
            mat.SetTexture("uParticleTexture", ParticleTexture);
            if (FlipbookTiles.X > 1f || FlipbookTiles.Y > 1f)
                mat.SetParameterValue("uFlipbookTiles", FlipbookTiles);
        }
        return mat;
    }
}
