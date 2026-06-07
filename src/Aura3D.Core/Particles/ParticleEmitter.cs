using Aura3D.Core.Resources;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Particles;

public class ParticleEmitter
{
    public float EmissionRate { get; set; } = 100f;
    public EmissionShape Shape { get; set; } = EmissionShape.Point;
    public Vector3 ShapeSize { get; set; } = Vector3.One;
    public float ConeAngle { get; set; } = 30f;
    public bool Looping { get; set; } = true;
    public float Duration { get; set; } = 0f;

    public RangeFloat Lifetime { get; set; } = new(1f, 3f);
    public RangeVector3 Velocity { get; set; } = new(new(0, 5, 0), new(0, 10, 0));
    public RangeFloat StartSize { get; set; } = new(0.1f, 0.3f);
    public RangeFloat EndSize { get; set; } = new(0.01f, 0.05f);
    public Color StartColor { get; set; } = Color.White;
    public Color EndColor { get; set; } = Color.Transparent;
    public RangeFloat Rotation { get; set; } = new(0f, MathF.PI * 2);
    public RangeFloat AngularVelocity { get; set; } = new(-1f, 1f);

    public Vector3 Gravity { get; set; } = new(0f, -9.8f, 0f);
    public float Damping { get; set; } = 0f;

    /// <summary>
    /// Scale multiplier for mesh-based particles. Applied on top of the per-particle size.
    /// Ignored for billboard particles.
    /// </summary>
    public float MeshScale { get; set; } = 1f;

    public ITexture? Texture { get; set; }

    public float ElapsedTime { get; internal set; }
    public float EmissionAccumulator { get; internal set; }
    public bool IsFinished => !Looping && Duration > 0 && ElapsedTime >= Duration;

    public Vector4 GetStartColorVector() =>
        new(StartColor.R / 255f, StartColor.G / 255f, StartColor.B / 255f, StartColor.A / 255f);

    public Vector4 GetEndColorVector() =>
        new(EndColor.R / 255f, EndColor.G / 255f, EndColor.B / 255f, EndColor.A / 255f);
}
