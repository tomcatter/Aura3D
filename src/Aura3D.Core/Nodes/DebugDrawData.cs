using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 调试绘制数据，持有线段顶点数据和渲染状态信息。
/// 自管理 VAO/VBO，不依赖 Mesh/Material 管线。
/// DebugDrawPass 直接收集并渲染这些数据，无需任何类型过滤。
/// </summary>
public class DebugDrawData : IGpuResource
{
    /// <summary>
    /// 所属节点，用于获取世界变换矩阵。
    /// </summary>
    public Node Owner { get; }

    /// <summary>
    /// 绘制颜色。
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// 图元类型（Lines、Triangles 等）。
    /// </summary>
    public Resources.PrimitiveType PrimitiveType { get; set; }

    /// <summary>
    /// 是否禁用深度测试（始终渲染在最上层）。
    /// </summary>
    public bool NoDepthTest { get; set; }

    /// <summary>
    /// 本地空间的顶点位置数组（每 3 个 float 为一个 Vector3）。
    /// </summary>
    public float[] Positions { get; }

    /// <summary>
    /// 顶点数量。
    /// </summary>
    public int VertexCount => Positions.Length / 3;

    /// <summary>
    /// OpenGL VAO 句柄。
    /// </summary>
    public uint Vao { get; private set; }

    private uint _vbo;

    /// <summary>
    /// 获取或设置是否需要上传到 GPU。
    /// </summary>
    public bool NeedsUpload { get; set; } = true;

    /// <summary>
    /// 初始化 <see cref="DebugDrawData"/> 类的新实例。
    /// </summary>
    /// <param name="owner">所属节点，用于获取 WorldTransform。</param>
    /// <param name="positions">本地空间顶点数据。</param>
    /// <param name="color">绘制颜色。</param>
    /// <param name="primitiveType">图元类型，默认为 Lines。</param>
    /// <param name="noDepthTest">是否禁用深度测试（始终渲染在最上层）。</param>
    public DebugDrawData(Node owner, float[] positions, Color color,
        Resources.PrimitiveType primitiveType = Resources.PrimitiveType.Lines, bool noDepthTest = false)
    {
        Owner = owner;
        Positions = positions;
        Color = color;
        PrimitiveType = primitiveType;
        NoDepthTest = noDepthTest;
    }

    /// <summary>
    /// 获取当前世界变换矩阵。
    /// </summary>
    public Matrix4x4 WorldTransform => Owner.WorldTransform;

    /// <summary>
    /// 将顶点数据上传到 GPU。
    /// </summary>
    /// <param name="gl">OpenGL 上下文。</param>
    public unsafe void Upload(GL gl)
    {
        if (Vao == 0)
            Vao = gl.GenVertexArray();
        if (_vbo == 0)
            _vbo = gl.GenBuffer();

        gl.BindVertexArray(Vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, _vbo);

        fixed (float* p = Positions)
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(Positions.Length * sizeof(float)), p, GLEnum.StaticDraw);
        }

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, 3 * sizeof(float), (void*)0);

        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindVertexArray(0);

        NeedsUpload = false;
    }

    /// <summary>
    /// 销毁 GPU 资源。
    /// </summary>
    /// <param name="gl">OpenGL 上下文。</param>
    public void Destroy(GL gl)
    {
        if (_vbo != 0)
        {
            gl.DeleteBuffer(_vbo);
            _vbo = 0;
        }
        if (Vao != 0)
        {
            gl.DeleteVertexArray(Vao);
            Vao = 0;
        }
        NeedsUpload = true;
    }
}
