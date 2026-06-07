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

    public BlendMode BlendMode { get; set; } = BlendMode.Translucent;
    public ITexture? ParticleTexture { get; set; }
    public Vector2 FlipbookTiles { get; set; } = Vector2.One;
    public bool IsPlaying => _isPlaying;
    public int ActiveCount => _activeCount;
    public List<ParticleEmitter> Emitters { get; } = new();

    /// <summary>Internal: CPU-side particle data array, accessed by ParticlePass for rendering.</summary>
    internal ParticleData[] Particles => _particles;

    /// <summary>Internal: GPU buffer, accessed by ParticlePass for drawing.</summary>
    internal ParticleGpuBuffer GpuBuffer => _gpuBuffer;

    public void Play()
    {
        if (_isPlaying) return;
        _particles = new ParticleData[_maxParticles];
        _activeCount = 0;
        _isPlaying = true;
        _isPaused = false;
        _rng = new Random();
        foreach (var e in Emitters) { e.ElapsedTime = 0; e.EmissionAccumulator = 0; }
    }

    public void Stop()
    {
        _isPlaying = false; _isPaused = false; _activeCount = 0;
        _particles = Array.Empty<ParticleData>();
    }

    public void Pause() => _isPaused = !_isPaused;

    public override void Update(double delta)
    {
        base.Update(delta);
        if (!_isPlaying || _isPaused) return;
        float dt = (float)delta;

        ParticleSimulation.Update(_particles!, ref _activeCount, _maxParticles, Emitters, dt, _rng, WorldTransform.Translation);

        _gpuBuffer.SetParticleData(_particles, _activeCount);
    }

    /// <summary>
    /// Sort particles by distance to camera (back-to-front) for correct alpha blending.
    /// Called by ParticlePass during rendering.
    /// </summary>
    internal void SortByDistance(Vector3 camPos)
    {
        if (_activeCount <= 1 || BlendMode == BlendMode.Opaque || BlendMode == BlendMode.Masked) return;
        int n = _activeCount;
        var camPosLocal = camPos;
        System.Array.Sort(_particles!, 0, n, Comparer<ParticleData>.Create((a, b) =>
        {
            float da = Vector3.DistanceSquared(a.Position, camPosLocal);
            float db = Vector3.DistanceSquared(b.Position, camPosLocal);
            return db.CompareTo(da);
        }));
    }

    public override List<IGpuResource> GetGpuResources()
    {
        var list = new List<IGpuResource> { _gpuBuffer };
        if (ParticleTexture is IGpuResource gr) list.Add(gr);
        return list;
    }

    private int _maxParticles = 10000;
    private ParticleData[] _particles = Array.Empty<ParticleData>();
    private int _activeCount;
    private bool _isPlaying, _isPaused;
    private Random _rng = new();
    private readonly ParticleGpuBuffer _gpuBuffer = new();
}
