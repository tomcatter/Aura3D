using System.Numerics;

namespace Aura3D.Core.Math;

/// <summary>
/// 表示 3D 空间中的一条射线，由原点和单位方向向量定义。
/// </summary>
public struct Ray
{
    /// <summary>
    /// 射线的起点。
    /// </summary>
    public Vector3 Origin;

    /// <summary>
    /// 射线的单位方向向量。
    /// </summary>
    public Vector3 Direction;

    /// <summary>
    /// 初始化 <see cref="Ray"/> 结构的新实例，自动归一化方向向量。
    /// </summary>
    /// <param name="origin">射线起点。</param>
    /// <param name="direction">射线方向（会被归一化）。</param>
    public Ray(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = direction == Vector3.Zero ? direction : Vector3.Normalize(direction);
    }

    /// <summary>
    /// 获取射线上沿方向距离为 t 的点。
    /// </summary>
    /// <param name="t">沿射线方向的距离。</param>
    /// <returns>世界空间中的点。</returns>
    public Vector3 GetPoint(float t)
    {
        return Origin + Direction * t;
    }

    /// <summary>
    /// 检测射线与 <see cref="BoundingBox"/> 的相交，返回射线原点到交点的参数 t。
    /// 使用平板法（slab method）进行高效的 AABB 相交检测。
    /// </summary>
    /// <param name="box">要检测的轴对齐包围盒。</param>
    /// <returns>入口点的 t 值（沿射线方向的距离），不相交则返回 null。</returns>
    public float? Intersects(BoundingBox box)
    {
        // 计算每个轴分量的逆方向，用于平板相交测试
        float tMin = float.MinValue;
        float tMax = float.MaxValue;

        // X 轴
        if (MathF.Abs(Direction.X) < BoundingBox.DefaultEpsilon)
        {
            if (Origin.X < box.Min.X || Origin.X > box.Max.X)
                return null;
        }
        else
        {
            float invDir = 1.0f / Direction.X;
            float t1 = (box.Min.X - Origin.X) * invDir;
            float t2 = (box.Max.X - Origin.X) * invDir;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax) return null;
        }

        // Y 轴
        if (MathF.Abs(Direction.Y) < BoundingBox.DefaultEpsilon)
        {
            if (Origin.Y < box.Min.Y || Origin.Y > box.Max.Y)
                return null;
        }
        else
        {
            float invDir = 1.0f / Direction.Y;
            float t1 = (box.Min.Y - Origin.Y) * invDir;
            float t2 = (box.Max.Y - Origin.Y) * invDir;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax) return null;
        }

        // Z 轴
        if (MathF.Abs(Direction.Z) < BoundingBox.DefaultEpsilon)
        {
            if (Origin.Z < box.Min.Z || Origin.Z > box.Max.Z)
                return null;
        }
        else
        {
            float invDir = 1.0f / Direction.Z;
            float t1 = (box.Min.Z - Origin.Z) * invDir;
            float t2 = (box.Max.Z - Origin.Z) * invDir;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax) return null;
        }

        // 只返回正方向的交点（射线前方的交点）
        if (tMax < 0)
            return null;

        return tMin >= 0 ? tMin : tMax;
    }

    /// <summary>
    /// 使用 Möller–Trumbore 算法检测射线与三角形的相交。
    /// </summary>
    /// <param name="v0">三角形顶点 0。</param>
    /// <param name="v1">三角形顶点 1。</param>
    /// <param name="v2">三角形顶点 2。</param>
    /// <returns>命中点的 t 值（沿射线方向的距离），不相交则返回 null。</returns>
    public float? IntersectsTriangle(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        const float epsilon = 1e-6f;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 h = Vector3.Cross(Direction, edge2);
        float a = Vector3.Dot(edge1, h);

        // 射线与三角形平面平行
        if (MathF.Abs(a) < epsilon)
            return null;

        float f = 1.0f / a;
        Vector3 s = Origin - v0;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0f || u > 1.0f)
            return null;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(Direction, q);

        if (v < 0.0f || u + v > 1.0f)
            return null;

        float t = f * Vector3.Dot(edge2, q);

        if (t > epsilon)
            return t;

        return null;
    }
}
