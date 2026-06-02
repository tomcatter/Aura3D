using Silk.NET.OpenGLES;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

/// <summary>
/// 几何体类，存储顶点数据和索引数据
/// </summary>
public class Geometry : IGpuResource, IClone<Geometry>
{

    /// <summary>
    /// 是否需要上传到GPU
    /// </summary>
    public bool NeedsUpload { get; set; } = true;

    protected Dictionary<string, VertexAttribute> VertexAttributes = new();

    /// <summary>
    /// 索引列表
    /// </summary>
    public List<uint> Indices { get; protected set; } = [];

    protected HashSet<uint> VertexAttributeLocations = new();

    protected List<uint> VboIds = new();

    /// <summary>
    /// 索引数量
    /// </summary>
    public int IndicesCount => Indices.Count;

    /// <summary>
    /// 顶点数组对象ID
    /// </summary>
    public uint Vao;

    /// <summary>
    /// 元素缓冲对象ID
    /// </summary>
    public uint Ebo;
    public void SetVertexAttribute(string name, uint location, int size, List<float> data)
    {
        if (data.Count % size != 0)
            throw new ArgumentException($"The length of vertex attribute data must be a multiple of its size. Data length: {data.Count}, Size: {size}");

        if (VertexAttributes.TryGetValue(name, out var vertexAttribute))
        {
            VertexAttributes.Remove(name);
            VertexAttributeLocations.Remove(vertexAttribute.Location);
        }

        VertexAttributes.Add(name, new VertexAttribute
        {
            Name = name,
            Location = location,
            Size = size,
            Data = data,
            Enabled = (location <= 6)
        });
        VertexAttributeLocations.Add(location);
    }

    public void SetVertexAttribute(BuildInVertexAttribute attribute, uint size, List<float> data)
    {
        SetVertexAttribute(attribute.ToString(), (uint)attribute, (int)size, data);
    }

    public void SetIndices(List<uint> indices)
    {
        Indices = indices;
    }

    public List<float>? GetAttributeData(string name)
    {
        if (!VertexAttributes.ContainsKey(name))
            return null;
        return VertexAttributes[name].Data;
    }

    public List<float>? GetAttributeData(BuildInVertexAttribute attribute)
    {
        return GetAttributeData(attribute.ToString());
    }

    /// <summary>
    /// 开启或关闭指定顶点属性的上传。
    /// </summary>
    /// <param name="attribute">内置顶点属性枚举。</param>
    /// <param name="enabled">是否启用上传。</param>
    public void SetAttributeEnabled(BuildInVertexAttribute attribute, bool enabled)
    {
        var name = attribute.ToString();
        if (VertexAttributes.TryGetValue(name, out var attr))
        {
            attr.Enabled = enabled;
            VertexAttributes[name] = attr;
            NeedsUpload = true;
        }
    }

    public void Destroy(GL gl)
    {
        foreach (var vbo in VboIds)
        {
            gl.DeleteBuffer(vbo);
        }
        VboIds.Clear();
        if (Ebo != 0)
        {
            gl.DeleteBuffer(Ebo);
            Ebo = 0;
        }
        if (Vao != 0)
        {
            gl.DeleteVertexArray(Vao);
            Vao = 0;
        }
    }

    public unsafe void Upload(GL gl)
    {
        if (Vao == 0)
        {
            Vao = gl.GenVertexArray();
        }
        else
        {
            // 重新上传时清理旧 VBO，避免重复申请导致泄漏
            foreach (var vbo in VboIds)
            {
                gl.DeleteBuffer(vbo);
            }
            VboIds.Clear();
        }

        gl.BindVertexArray(Vao);

        foreach (var(_, attribute) in VertexAttributes)
        {
            if (!attribute.Enabled)
                continue;

            uint vbo = gl.GenBuffer();
            VboIds.Add(vbo);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            unsafe
            {
                fixed (float* dataPtr = CollectionsMarshal.AsSpan(attribute.Data))
                {
                    gl.BufferData(GLEnum.ArrayBuffer, (nuint)(attribute.Data.Count * sizeof(float)), dataPtr, GLEnum.StaticDraw);
                }
            }
            gl.EnableVertexAttribArray(attribute.Location);
            gl.VertexAttribPointer(attribute.Location, attribute.Size, GLEnum.Float, false, (uint)(sizeof(float) * attribute.Size), (void*)0);
        }

        if (Ebo == 0)
        {
            Ebo = gl.GenBuffer();
        }
        gl.BindBuffer(GLEnum.ElementArrayBuffer, Ebo);

        fixed (uint* indexPtr = CollectionsMarshal.AsSpan(Indices))
        {
            // 上传索引数据到 GPU
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(Indices.Count * sizeof(uint)), indexPtr, GLEnum.StaticDraw);
        }


    }

    public Geometry Clone()
    {
        return new Geometry
        {
            Indices = Indices,
            VertexAttributes = VertexAttributes,
            VertexAttributeLocations = VertexAttributeLocations
        };
    }

    public Geometry DeepClone()
    {
        return new Geometry
        {
            Indices = new List<uint>(Indices),
            VertexAttributes = VertexAttributes.ToDictionary(),
            VertexAttributeLocations = new HashSet<uint>(VertexAttributeLocations)
        };
    }
}


/// <summary>
/// 顶点属性结构体
/// </summary>
public struct VertexAttribute
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public string Name;
    /// <summary>
    /// 属性位置
    /// </summary>
    public uint Location;
    /// <summary>
    /// 属性大小（分量数）
    /// </summary>
    public int Size;
    /// <summary>
    /// 属性数据
    /// </summary>
    public List<float> Data;
    /// <summary>
    /// 是否启用上传。默认只有 location 0~6 (Position 到 Weights_0) 为 true。
    /// </summary>
    public bool Enabled;
}

/// <summary>
/// 内置顶点属性枚举
/// </summary>
public enum BuildInVertexAttribute
{
    /// <summary>
    /// 位置
    /// </summary>
    Position = 0,
    /// <summary>
    /// 第一套纹理坐标
    /// </summary>
    TexCoord_0 = 1,
    /// <summary>
    /// 法线
    /// </summary>
    Normal = 2,
    /// <summary>
    /// 切线
    /// </summary>
    Tangent = 3,
    /// <summary>
    /// 副切线
    /// </summary>
    Bitangent = 4,
    /// <summary>
    /// 第一套关节索引
    /// </summary>
    Joints_0 = 5,
    /// <summary>
    /// 第一套权重
    /// </summary>
    Weights_0 = 6,

    InstancedTransformColumn0 = 7,
    InstancedTransformColumn1 = 8,
    InstancedTransformColumn2 = 9,
    InstancedTransformColumn3 = 10,

    InstancedNormalTransformColumn0 = 11,
    InstancedNormalTransformColumn1 = 12,
    InstancedNormalTransformColumn2 = 13,
    InstancedNormalTransformColumn3 = 14,

    /// <summary>
    /// 第二套纹理坐标
    /// </summary>
    TexCoord_1 = 15,
    /// <summary>
    /// 第三套纹理坐标
    /// </summary>
    TexCoord_2 = 16,
    /// <summary>
    /// 第四套纹理坐标
    /// </summary>
    TexCoord_3 = 17,
    /// <summary>
    /// 第二套关节索引
    /// </summary>
    Joints_1 = 18,
    /// <summary>
    /// 第二套权重
    /// </summary>
    Weights_1 = 19,
}