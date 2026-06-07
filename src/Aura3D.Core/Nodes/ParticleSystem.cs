using Aura3D.Core.Math;
using Aura3D.Core.Particles;
using Aura3D.Core.Resources;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// High-performance particle system node using CPU simulation.
/// Supports two render modes:
/// - Billboard (default): GPU instanced billboard quads via ParticlePass.
/// - Mesh: when ParticleMesh is set, particles render as 3D mesh instances through the normal
///   mesh rendering pipeline (InstancedMesh → NoLightPass/LightPass/TranslucentPass).
/// The CPU simulation is identical in both modes.
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

    // ---- Mesh mode ----

    /// <summary>
    /// The mesh to use for each particle. When set, particles render as 3D mesh instances
    /// through the normal mesh pipeline. When null (default), particles use the billboard quad path.
    /// </summary>
    public Mesh? ParticleMesh { get; set; }

    /// <summary>
    /// Optional material override for mesh-mode particles.
    /// When null, the material from ParticleMesh is used.
    /// </summary>
    public Material? ParticleMaterial { get; set; }

    private BoundingBox? _customBoundingBox;

    /// <summary>
    /// Optional custom world-space bounding box for mesh-mode particles.
    /// When set, this overrides the automatic estimate and syncs to the InstancedMesh.
    /// Set this to optimize culling if you know your particle spread precisely.
    /// </summary>
    public BoundingBox? CustomBoundingBox
    {
        get => _customBoundingBox;
        set
        {
            _customBoundingBox = value;
            _estimatedBbox = value ?? EstimateWorldBoundingBox();
            if (_estimatedBbox != null)
                _instancedMesh?.SetStaticWorldBoundingBox(_estimatedBbox);
        }
    }

    /// <summary>
    /// When enabled, the particle simulation is skipped if the system's bounding box
    /// is outside the main camera frustum. Requires mesh mode.
    /// </summary>
    public bool EnableVisibilityCulling { get; set; } = false;

    /// <summary>
    /// World-space bounding box for debug display and visibility culling.
    /// </summary>
    public BoundingBox? WorldBoundingBox => _estimatedBbox;

    /// <summary>
    /// Whether this system renders through the mesh pipeline (true) or billboard pipeline (false).
    /// </summary>
    public bool UseMeshRenderer => ParticleMesh != null;

    // ---- Internal: billboard mode ----

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
        _accumulatedSkippedTime = 0f;
        foreach (var e in Emitters) { e.ElapsedTime = 0; e.EmissionAccumulator = 0; }

        // Use custom bounding box if set, otherwise auto-estimate
        _estimatedBbox = _customBoundingBox ?? EstimateWorldBoundingBox();

        // Mesh mode: create InstancedMesh from the source mesh
        if (UseMeshRenderer)
        {
            _instancedMesh = InstancedMesh.FromMesh(ParticleMesh!);
            if (ParticleMaterial != null)
                _instancedMesh.Material = ParticleMaterial;
            _instancedMesh.Name = $"{Name}_ParticleInstances";
            _instancedMesh.EnableFrustumCulling = false;

            if (_estimatedBbox != null)
                _instancedMesh.SetStaticWorldBoundingBox(_estimatedBbox);
            // Safe to parent: the INSTANCED_MESH shader uses per-instance matrices directly,
            // ignoring the node's WorldTransform. AddChild ensures proper lifecycle cleanup.
            AddChild(_instancedMesh, AttachToParentRule.KeepWorld);
        }
    }

    public void Stop()
    {
        _isPlaying = false;
        _isPaused = false;
        _activeCount = 0;
        _particles = Array.Empty<ParticleData>();

        // Clean up mesh mode
        if (_instancedMesh != null)
        {
            if (_children.Contains(_instancedMesh))
                RemoveChild(_instancedMesh, AttachToParentRule.KeepWorld);
            _instancedMesh = null;
        }
    }

    public void Pause() => _isPaused = !_isPaused;

    public override void Update(double delta)
    {
        base.Update(delta);
        if (!_isPlaying || _isPaused) return;
        float dt = (float)delta;

        // Update frustum planes (always, so they're fresh)
        UpdateCachedFrustumPlanes();

        // Visibility check
        bool isVisible = !EnableVisibilityCulling
            || _estimatedBbox == null
            || _cachedFrustumPlanes == null
            || _estimatedBbox.IsBoxInsideFrustum(_cachedFrustumPlanes);

        if (!isVisible)
        {
            // Accumulate skipped time so we can catch up later
            _accumulatedSkippedTime += dt;
            return;
        }

        // Apply accumulated time from when we were invisible (fast-forward catch-up)
        if (_accumulatedSkippedTime > 0f)
        {
            float catchUp = MathF.Min(_accumulatedSkippedTime, 2f);
            _accumulatedSkippedTime = 0f;
            Simulate(catchUp);
        }

        Simulate(dt);
    }

    private void Simulate(float dt)
    {
        ParticleSimulation.Update(_particles!, ref _activeCount, _maxParticles, Emitters, dt, _rng,
            WorldTransform.Translation, WorldTransform.Rotation());

        if (UseMeshRenderer)
        {
            UpdateMeshInstances();
        }
        else
        {
            _gpuBuffer.SetParticleData(_particles, _activeCount);
        }
    }

    private void UpdateCachedFrustumPlanes()
    {
        if (!EnableVisibilityCulling) return;
        var cameras = CurrentScene?.RenderPipeline?.Cameras;
        if (cameras == null || cameras.Count == 0) return;
        var cam = cameras[0];
        if (!cam.Enable) return;

        var vp = cam.View * cam.Projection;
        _cachedFrustumPlanes ??= new Plane[6];
        MatrixHelper.ExtractPlanes(vp, _cachedFrustumPlanes);
    }

    /// <summary>
    /// Convert alive particles to world-space transforms and update the InstancedMesh.
    /// Emission rotation is handled by ParticleSimulation; here we only compose
    /// the system rotation with the particle's own spin for correct mesh orientation.
    /// </summary>
    private void UpdateMeshInstances()
    {
        if (_instancedMesh == null) return;

        int n = _activeCount;
        if (_instanceTransforms == null || _instanceTransforms.Length < n)
            _instanceTransforms = new Matrix4x4[n];

        var systemRot = WorldTransform.Rotation();

        for (int i = 0; i < n; i++)
        {
            ref var p = ref _particles![i];
            var em = p.EmitterIndex < Emitters.Count ? Emitters[p.EmitterIndex] : null;
            float meshScale = em?.MeshScale ?? 1f;

            // Particle's own spin, composed with system rotation
            var spinRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, p.Rotation);
            var finalRot = Quaternion.Concatenate(systemRot, spinRot);

            _instanceTransforms[i] =
                Matrix4x4.CreateScale(p.CurrentSize * meshScale)
                * Matrix4x4.CreateFromQuaternion(finalRot)
                * Matrix4x4.CreateTranslation(p.Position);
        }

        _instancedMesh.SetInstances(
            new ArraySegment<Matrix4x4>(_instanceTransforms, 0, n));
    }

    /// <summary>
    /// Sort particles by distance to camera (back-to-front) for correct alpha blending.
    /// Called by ParticlePass during rendering (billboard mode only).
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
        var list = new List<IGpuResource>();

        if (!UseMeshRenderer)
        {
            // Billboard mode: ParticleSystem owns GpuBuffer and texture
            list.Add(_gpuBuffer);
            if (ParticleTexture is IGpuResource gr)
                list.Add(gr);
        }
        // Mesh mode: InstancedMesh is a child node — it manages its own GPU resources
        // via the scene graph. Duplicating here would cause double ref-counting.

        return list;
    }

    private int _maxParticles = 10000;
    private ParticleData[] _particles = Array.Empty<ParticleData>();
    private int _activeCount;
    private bool _isPlaying, _isPaused;
    private Random _rng = new();
    private readonly ParticleGpuBuffer _gpuBuffer = new();

    // Mesh mode
    /// <summary>
    /// Estimate a static world bounding box from emitter settings.
    /// Uses max velocity × max lifetime to determine spread radius.
    /// </summary>
    private BoundingBox? EstimateWorldBoundingBox()
    {
        if (Emitters.Count == 0) return null;

        float maxVx = 0f, maxVy = 0f, maxVz = 0f;
        float maxLifetime = 0f;
        float maxSize = 0f;
        float gravityY = 0f;

        foreach (var em in Emitters)
        {
            maxVx = MathF.Max(maxVx, MathF.Max(MathF.Abs(em.Velocity.Min.X), MathF.Abs(em.Velocity.Max.X)));
            maxVy = MathF.Max(maxVy, MathF.Max(MathF.Abs(em.Velocity.Min.Y), MathF.Abs(em.Velocity.Max.Y)));
            maxVz = MathF.Max(maxVz, MathF.Max(MathF.Abs(em.Velocity.Min.Z), MathF.Abs(em.Velocity.Max.Z)));

            maxLifetime = MathF.Max(maxLifetime, em.Lifetime.Max);
            maxSize = MathF.Max(maxSize, em.StartSize.Max);
            gravityY = MathF.Min(gravityY, em.Gravity.Y); // gravity is negative
        }

        // Horizontal: tight estimate
        float rx = maxVx * maxLifetime * 0.2f + maxSize;
        float rz = maxVz * maxLifetime * 0.2f + maxSize;

        float upY, downY;
        if (gravityY < 0)
        {
            // Gravity-affected: asymmetric Y — tight upward (v²/2g), wider downward
            float g = MathF.Abs(gravityY);
            upY = (maxVy * maxVy) / (2f * g) + maxSize;
            float velDown = maxVy * maxLifetime * 0.2f;
            float gravDrop = 0.15f * g * maxLifetime * maxLifetime;
            downY = velDown + gravDrop + maxSize;
        }
        else
        {
            // No gravity: symmetric spread
            upY = maxVy * maxLifetime * 0.2f + maxSize;
            downY = upY;
        }

        var center = WorldTransform.Translation;
        return new BoundingBox(
            new Vector3(center.X - rx, center.Y - downY, center.Z - rz),
            new Vector3(center.X + rx, center.Y + upY,  center.Z + rz));
    }

    private InstancedMesh? _instancedMesh;
    private Matrix4x4[]? _instanceTransforms;
    private Plane[]? _cachedFrustumPlanes;
    private BoundingBox? _estimatedBbox;
    private float _accumulatedSkippedTime;
}
