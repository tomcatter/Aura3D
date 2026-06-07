using System.Numerics;

namespace Aura3D.Core.Particles;

public static class ParticleSimulation
{
    public static void Update(
        ParticleData[] particles, ref int activeCount, int maxParticles,
        IReadOnlyList<ParticleEmitter> emitters, float deltaTime, Random rng,
        Vector3 worldOffset, Quaternion worldRotation = default)
    {
        if (worldRotation == default) worldRotation = Quaternion.Identity;
        RemoveDead(particles, ref activeCount);
        Emit(particles, ref activeCount, maxParticles, emitters, deltaTime, rng, worldOffset, worldRotation);
        UpdateAlive(particles, activeCount, emitters, deltaTime);
    }

    private static void RemoveDead(ParticleData[] p, ref int n)
    {
        int i = 0;
        while (i < n)
        {
            if (p[i].IsDead) { n--; if (i < n) p[i] = p[n]; }
            else i++;
        }
    }

    private static void Emit(
        ParticleData[] p, ref int n, int max, IReadOnlyList<ParticleEmitter> emitters,
        float dt, Random rng, Vector3 offset, Quaternion worldRotation)
    {
        if (n >= max) return;
        for (int e = 0; e < emitters.Count; e++)
        {
            var em = emitters[e];
            if (em.IsFinished) continue;
            em.ElapsedTime += dt;
            float toEmit = em.EmissionRate * dt + em.EmissionAccumulator;
            int cnt = (int)toEmit;
            em.EmissionAccumulator = toEmit - cnt;
            int rem = max - n;
            if (cnt > rem) cnt = rem;
            for (int j = 0; j < cnt; j++) { p[n] = NewParticle(em, e, rng, offset, worldRotation); n++; }
        }
    }

    private static ParticleData NewParticle(ParticleEmitter em, int idx, Random rng,
        Vector3 offset, Quaternion worldRotation)
    {
        var localPos = SamplePosition(em, rng);
        var localVel = em.Velocity.Random(rng);
        return new ParticleData
        {
            Position = offset + Vector3.Transform(localPos, worldRotation),
            Velocity = Vector3.Transform(localVel, worldRotation),
            Lifetime = em.Lifetime.Random(rng),
            StartSize = em.StartSize.Random(rng),
            EndSize = em.EndSize.Random(rng),
            StartColor = em.GetStartColorVector(),
            EndColor = em.GetEndColorVector(),
            Rotation = em.Rotation.Random(rng),
            AngularVelocity = em.AngularVelocity.Random(rng),
            EmitterIndex = idx,
        };
    }

    private static void UpdateAlive(ParticleData[] p, int n, IReadOnlyList<ParticleEmitter> emitters, float dt)
    {
        for (int i = 0; i < n; i++)
        {
            ref var r = ref p[i];
            var em = r.EmitterIndex < emitters.Count ? emitters[r.EmitterIndex] : null;
            var g = em?.Gravity ?? Vector3.Zero;
            var d = em?.Damping ?? 0f;
            r.Velocity += g * dt;
            r.Velocity *= MathF.Max(0f, 1f - d * dt);
            r.Position += r.Velocity * dt;
            r.Rotation += r.AngularVelocity * dt;
            r.Age += dt;
        }
    }

    // ===== Shape sampling =====

    public static Vector3 SamplePosition(ParticleEmitter em, Random rng) => em.Shape switch
    {
        EmissionShape.Point => Vector3.Zero,
        EmissionShape.Sphere => SampleSphere(em.ShapeSize, rng, false),
        EmissionShape.SphereSurface => SampleSphere(em.ShapeSize, rng, true),
        EmissionShape.Box => SampleBox(em.ShapeSize, rng),
        EmissionShape.Cone => SampleCone(em.ShapeSize, rng, em.ConeAngle),
        EmissionShape.Circle => SampleCircle(em.ShapeSize, rng),
        EmissionShape.Hemisphere => SampleHemi(em.ShapeSize, rng),
        _ => Vector3.Zero,
    };

    private static Vector3 SampleSphere(Vector3 s, Random r, bool surf)
    {
        float th = (float)(r.NextDouble() * MathF.PI * 2);
        float ph = MathF.Acos((float)(2 * r.NextDouble() - 1));
        float rad = surf ? 1f : MathF.Cbrt((float)r.NextDouble());
        float rx = s.X * .5f, ry = s.Y * .5f, rz = s.Z * .5f;
        return new(rad * rx * MathF.Sin(ph) * MathF.Cos(th), rad * ry * MathF.Cos(ph), rad * rz * MathF.Sin(ph) * MathF.Sin(th));
    }

    private static Vector3 SampleBox(Vector3 s, Random r) =>
        new((float)(r.NextDouble() - .5) * s.X, (float)(r.NextDouble() - .5) * s.Y, (float)(r.NextDouble() - .5) * s.Z);

    private static Vector3 SampleCone(Vector3 s, Random r, float angDeg)
    {
        float rad = s.X * .5f, h = s.Y, a = angDeg * MathF.PI / 180f;
        float t = (float)r.NextDouble(), rr = t * rad, y = t * h;
        float th = (float)(r.NextDouble() * MathF.PI * 2);
        float mx = y * MathF.Tan(a);
        if (rr > mx && mx > 0) rr = (float)r.NextDouble() * mx;
        return new(rr * MathF.Cos(th), y, rr * MathF.Sin(th));
    }

    private static Vector3 SampleCircle(Vector3 s, Random r)
    {
        float rx = s.X * .5f, rz = s.Z * .5f;
        float a = (float)(r.NextDouble() * MathF.PI * 2);
        float rad = MathF.Sqrt((float)r.NextDouble());
        return new(rad * rx * MathF.Cos(a), 0, rad * rz * MathF.Sin(a));
    }

    private static Vector3 SampleHemi(Vector3 s, Random r)
    {
        float th = (float)(r.NextDouble() * MathF.PI * 2);
        float ph = MathF.Acos((float)r.NextDouble());
        float rad = MathF.Cbrt((float)r.NextDouble());
        float rx = s.X * .5f, ry = s.Y * .5f, rz = s.Z * .5f;
        return new(rad * rx * MathF.Sin(ph) * MathF.Cos(th), MathF.Abs(rad * ry * MathF.Cos(ph)), rad * rz * MathF.Sin(ph) * MathF.Sin(th));
    }
}
