using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System.Numerics;

namespace Aura3D.Core.Particles;

/// <summary>
/// Helper for building particle render data (billboard geometry, instanced mesh creation).
/// </summary>
public static class ParticleRenderData
{
    public const uint InstanceColorLocation = 2;
    public const uint InstanceSizeLocation = 3;

    public static InstancedMesh CreateBillboardInstancedMesh(Geometry billboardGeo, Material? material, string name)
    {
        var mesh = new Mesh { Geometry = billboardGeo, Material = material };
        var im = InstancedMesh.FromMesh(mesh);
        im.Name = name;
        im.EnableFrustumCulling = true;
        im.SetAttributeEnabled("InstanceNormalTransform", false);
        return im;
    }

    public static void SetParticleInstanceAttributes(InstancedMesh im, IReadOnlyList<Vector4> colors, IReadOnlyList<float> sizes)
    {
        im.SetInstanceAttribute((BuildInVertexAttribute)InstanceColorLocation, 4, colors);
        im.SetInstanceAttribute((BuildInVertexAttribute)InstanceSizeLocation, 1, sizes);
    }

    private static Geometry? _sharedGeo;
    private static readonly object _lock = new();

    public static Geometry GetSharedBillboardGeometry()
    {
        if (_sharedGeo != null) return _sharedGeo;
        lock (_lock)
        {
            if (_sharedGeo != null) return _sharedGeo;
            var geo = new Geometry { PrimitiveType = PrimitiveType.Points };
            geo.SetVertexAttribute(BuildInVertexAttribute.Position, 3, new List<float>
                { -0.5f, -0.5f, 0f, 0.5f, -0.5f, 0f, 0.5f, 0.5f, 0f, -0.5f, 0.5f, 0f });
            geo.SetVertexAttribute(BuildInVertexAttribute.TexCoord_0, 2, new List<float>
                { 0f, 1f, 1f, 1f, 1f, 0f, 0f, 0f });
            geo.SetIndices(new List<uint> { 0, 1, 2, 2, 3, 0 });
            _sharedGeo = geo;
            return geo;
        }
    }
}
