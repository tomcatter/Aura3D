using System.Drawing;
using System.Numerics;
using Aura3D.Core.Renderers;
using Aura3D.Core.Math;
using Aura3D.Core.Resources;

namespace Aura3D.Core.Nodes;

public class Camera : Node
{
    public static ControlRenderTarget? ControlRenderTarget;

    public Camera()
    {
        if (ControlRenderTarget == null)
            throw new Exception("ControlRenderTarget is null, please set Camera.ControlRenderTarget before create Camera instance.");
        RenderTarget = ControlRenderTarget;
    }

    public float NearPlane { get; set; } = 1f; // 近裁剪面

    public float FarPlane { get; set; } = 100f; // 远裁剪面

    public float FieldOfView { get; set; } = 75f; // 视野角度（度数）

    public float OrthographicSize { get; set; } = 5f; // 正交投影时的大小

    public Matrix4x4 View
    {
        get
        {
            var worldTransform = WorldTransform;

            return Matrix4x4.CreateLookAt(worldTransform.Translation, worldTransform.Translation + worldTransform.ForwardVector(), worldTransform.UpVector());

        }
    }

    public Matrix4x4 Projection
    {
        get
        {
            if (ProjectionType == ProjectionType.Perspective)
            {
                var fovRadians = FieldOfView.DegreeToRadians();

                var aspectRatio = RenderTarget.Width / (float)RenderTarget.Height;

                var projection =  Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, NearPlane, FarPlane);

                return projection;
            }
            else // Orthographic
            {
                float aspectRatio = RenderTarget.Width / (float)RenderTarget.Height;
                return Matrix4x4.CreateOrthographic(
                    OrthographicSize * aspectRatio, // 宽度
                    OrthographicSize, // 高度
                    NearPlane,
                    FarPlane);
            }
        }
    }

    public Matrix4x4 ViewProjection => View * Projection;

    public ProjectionType ProjectionType { get; set; } = ProjectionType.Perspective; // 投影类型

    public IRenderTarget RenderTarget { get; set; } = new ControlRenderTarget();

    public bool IsRenderBackground = true;

    public override List<IGpuResource> GetGpuResources()
    {
        var list = new List<IGpuResource>();

        list.Add(RenderTarget);

        return list;
    }

    public void LookAt(Vector3 target)
    {
        var camera = this;

        Vector3 cameraPos = camera.Position;

        Vector3 forward = Vector3.Normalize(target - cameraPos);

        Vector3 up = Vector3.UnitY; // 假设世界上方向为Y轴

        // 计算右向量
        Vector3 right = Vector3.Cross(forward, up);
        // 重新计算正交上向量
        up = Vector3.Cross(right, forward);

        // 构建旋转矩阵
        Matrix4x4 rotation = Matrix4x4.Identity;
        rotation.M11 = right.X;
        rotation.M21 = right.Y;
        rotation.M31 = right.Z;
        rotation.M12 = up.X;
        rotation.M22 = up.Y;
        rotation.M32 = up.Z;
        rotation.M13 = -forward.X;
        rotation.M23 = -forward.Y;
        rotation.M33 = -forward.Z;

        // 从旋转矩阵提取欧拉角（弧度）
        float pitch = MathF.Asin(-rotation.M23);
        float yaw = MathF.Atan2(rotation.M13, rotation.M33);
        float roll = MathF.Atan2(rotation.M21, rotation.M22);

        // 转换为角度并设置
        camera.RotationDegrees = new Vector3(
            pitch.RadiansToDegree(),
            yaw.RadiansToDegree(),
            roll.RadiansToDegree()
        );
    }

    public void FitToBoundingBox(BoundingBox aabb, float padding = 0.1f)
    {
        var camera = this;
        if (camera == null) throw new ArgumentNullException(nameof(camera));
        if (aabb == null) throw new ArgumentNullException(nameof(aabb));
        if (padding < 0 || padding > 1) throw new ArgumentOutOfRangeException(nameof(padding));

        Vector3 boxCenter = aabb.Center;
        Vector3 boxSize = aabb.Size;

        float fovRadians = camera.FieldOfView.DegreeToRadians();
        float aspectRatio = camera.RenderTarget.Width / (float)camera.RenderTarget.Height;

        float maxExtent = MathF.Max(boxSize.X, MathF.Max(boxSize.Y, boxSize.Z)) / 2f;

        float distance = maxExtent / MathF.Sin(fovRadians / 2f) * (1 + padding);
        distance = MathF.Max(distance, maxExtent / (MathF.Sin(fovRadians / 2f) * aspectRatio) * (1 + padding));
       

        Vector3 cameraDirection = camera.Forward;
        camera.Position = boxCenter - cameraDirection * distance;

        float boxDiagonal = boxSize.Length();

        camera.NearPlane = distance - boxDiagonal * 0.6f;
        camera.FarPlane = distance + boxDiagonal * 1.2f;

        if (camera.NearPlane < 0)
        {
            camera.NearPlane = -camera.NearPlane;

            camera.FarPlane = camera.FarPlane + 2 * camera.NearPlane;
        }

        camera.LookAt(boxCenter);
    }
}

public enum ProjectionType
{
    Perspective, // 透视投影
    Orthographic // 正交投影
}

public enum ClearType
{
    OnlyDepth, // 仅清除颜色缓冲区
    Color,
    Skybox,
    Texture
}