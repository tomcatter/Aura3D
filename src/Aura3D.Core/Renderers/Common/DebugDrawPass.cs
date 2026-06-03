using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 调试绘制渲染通道，用于渲染方向轴、网格等引擎内置调试可视化元素。
/// 该通道在每帧最后执行，禁用深度测试以确保调试元素始终可见。
/// </summary>
public class DebugDrawPass : RenderPass
{
    /// <summary>
    /// 初始化 <see cref="DebugDrawPass"/> 类的新实例。
    /// </summary>
    /// <param name="renderPipeline">所属的渲染管线。</param>
    public DebugDrawPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        this.VertexShader = ShaderResource.DebugVert;
        this.FragmentShader = ShaderResource.DebugFrag;
        ShaderName = nameof(DebugDrawPass);
    }

    /// <summary>
    /// 在渲染指定相机之前设置渲染状态。
    /// 默认启用深度测试；方向轴等需要始终可见的元素通过材质参数
    /// "noDepthTest" 单独禁用深度测试。
    /// </summary>
    /// <param name="camera">当前要渲染的相机。</param>
    public override void BeforeRender(Camera camera)
    {
        BindOutPutRenderTarget(camera);

        // 默认启用深度测试，使网格等参考面能被场景物体遮挡
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);

        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
    }

    /// <summary>
    /// 执行指定相机的调试绘制渲染。
    /// </summary>
    /// <param name="camera">当前要渲染的相机。</param>
    public override void Render(Camera camera)
    {
        UseShader();
        RenderMeshes(
            mesh => mesh.Material != null && mesh.Material.HasShader && IsDebugMesh(mesh),
            camera.View,
            camera.Projection);
    }

    /// <summary>
    /// 在渲染指定相机之后恢复渲染状态。
    /// </summary>
    /// <param name="camera">当前已渲染完成的相机。</param>
    public override void AfterRender(Camera camera)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.Disable(EnableCap.Blend);
    }

    /// <summary>
    /// 渲染单个调试网格。若材质设置了 "noDepthTest" 参数为 true，则禁用深度测试
    /// 使该元素始终可见（如方向轴）。
    /// </summary>
    /// <param name="mesh">要渲染的网格。</param>
    /// <param name="view">视图矩阵。</param>
    /// <param name="projection">投影矩阵。</param>
    public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
    {
        ClearTextureUnit();

        UniformMatrix4("modelMatrix", mesh.WorldTransform);
        UniformMatrix4("viewMatrix", view);
        UniformMatrix4("projectionMatrix", projection);

        // 默认颜色为白色
        UniformVector3("uColor", new Vector3(1, 1, 1));

        gl.BindVertexArray(mesh.Geometry!.Vao);

        // 检查是否需要禁用深度测试（方向轴始终可见）
        bool noDepth = mesh.Material != null
            && mesh.Material.TryGetParameterValue<bool>("noDepthTest", out var nd)
            && nd;

        if (noDepth)
        {
            gl.Disable(EnableCap.DepthTest);
            gl.DepthMask(false);
        }

        // 执行材质中注册的回调，用于设置 uColor 等自定义参数
        if (mesh.Material != null && mesh.Material.HasShader)
        {
            var callback = mesh.Material.GetShaderPassParametersCallback(ShaderName);
            callback?.Invoke(this);
        }

        var primitive = PrimitiveTypeToGLEnum(mesh.Geometry.PrimitiveType);
        if (mesh.Geometry.IndicesCount > 0)
            unsafe { gl.DrawElements(primitive, (uint)mesh.Geometry.IndicesCount, GLEnum.UnsignedInt, (void*)0); }
        else
            gl.DrawArrays(primitive, 0, (uint)mesh.Geometry.VertexCount);

        // 恢复深度测试状态
        if (noDepth)
        {
            gl.Enable(EnableCap.DepthTest);
            gl.DepthMask(true);
        }
    }

    /// <summary>
    /// 判断网格是否为调试网格（使用调试着色器的网格）。
    /// </summary>
    /// <param name="mesh">要检查的网格。</param>
    /// <returns>如果是调试网格则返回 true。</returns>
    private static bool IsDebugMesh(Mesh mesh)
    {
        if (mesh.Material == null)
            return false;

        var (vertexShader, _) = mesh.Material.GetShaderSource("DebugDrawPass");
        return vertexShader != null;
    }

    /// <summary>
    /// 将图元类型枚举转换为 OpenGL 图元类型。
    /// </summary>
    private static GLEnum PrimitiveTypeToGLEnum(Aura3D.Core.Resources.PrimitiveType type) => type switch
    {
        Aura3D.Core.Resources.PrimitiveType.Points => GLEnum.Points,
        Aura3D.Core.Resources.PrimitiveType.Lines => GLEnum.Lines,
        Aura3D.Core.Resources.PrimitiveType.LineStrip => GLEnum.LineStrip,
        Aura3D.Core.Resources.PrimitiveType.LineLoop => GLEnum.LineLoop,
        Aura3D.Core.Resources.PrimitiveType.TriangleStrip => GLEnum.TriangleStrip,
        Aura3D.Core.Resources.PrimitiveType.TriangleFan => GLEnum.TriangleFan,
        _ => GLEnum.Triangles,
    };
}
