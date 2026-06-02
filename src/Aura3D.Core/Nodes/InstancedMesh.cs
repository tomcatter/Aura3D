using Aura3D.Core.Math;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 表示一个可用于实例化渲染的网格节点。
/// </summary>
public class InstancedMesh : Node, IGpuResource
{
    public unsafe int AddInstance(Matrix4x4 transform)
    {
        EnsureDefaultAttributes();

        var transformAttr = InstanceAttributes["InstanceTransform"];
        var normalAttr = InstanceAttributes["InstanceNormalTransform"];

        // 追加 transform 数据（16 个 float）
        float* p = (float*)&transform;
        for (int i = 0; i < 16; i++)
            transformAttr.Data.Add(p[i]);

        // 计算并追加 normal matrix 数据
        var normalMatrix = transform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        p = (float*)&normalMatrix;
        for (int i = 0; i < 16; i++)
            normalAttr.Data.Add(p[i]);

        _instanceCount++;
        NeedsUpload = true;
        return _instanceCount - 1;
    }


    public void RemoveInstance(int index)
    {
        foreach (var attr in InstanceAttributes.Values)
        {
            int floatsPerInstance = attr.Stride / sizeof(float);
            attr.Data.RemoveRange(index * floatsPerInstance, floatsPerInstance);
        }
        _instanceCount--;
        NeedsUpload = true;
    }

    public unsafe void UpdateInstance(int index, Matrix4x4 transform)
    {
        var transformAttr = InstanceAttributes["InstanceTransform"];
        var normalAttr = InstanceAttributes["InstanceNormalTransform"];

        int baseIndex = index * 16;

        float* p = (float*)&transform;
        for (int i = 0; i < 16; i++)
            transformAttr.Data[baseIndex + i] = p[i];

        var normalMatrix = transform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        p = (float*)&normalMatrix;
        for (int i = 0; i < 16; i++)
            normalAttr.Data[baseIndex + i] = p[i];

        NeedsUpload = true;
    }

    private int _instanceCount;

    private static readonly InstanceAttributePointer[] DefaultTransformPointers =
    [
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn0, ComponentCount = 4, Offset = 0 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn1, ComponentCount = 4, Offset = sizeof(float) * 4 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn2, ComponentCount = 4, Offset = sizeof(float) * 8 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedTransformColumn3, ComponentCount = 4, Offset = sizeof(float) * 12 },
    ];

    private static readonly InstanceAttributePointer[] DefaultNormalTransformPointers =
    [
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn0, ComponentCount = 4, Offset = 0 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn1, ComponentCount = 4, Offset = sizeof(float) * 4 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn2, ComponentCount = 4, Offset = sizeof(float) * 8 },
        new() { Location = (uint)BuildInVertexAttribute.InstancedNormalTransformColumn3, ComponentCount = 4, Offset = sizeof(float) * 12 },
    ];

    /// <summary>
    /// 逐实例属性字典，key 为属性名称。
    /// </summary>
    public Dictionary<string, InstanceAttribute> InstanceAttributes { get; } = new();

    public Material? Material { get; set; }

    public bool NeedsUpload { get; set; }

    private Geometry geometry { get; set; }

    public uint Vao => geometry.Vao;

    public int IndicesCount => geometry.IndicesCount;

    public int InstanceCount => _instanceCount;

    private unsafe void EnsureDefaultAttributes()
    {
        if (!InstanceAttributes.ContainsKey("InstanceTransform"))
        {
            InstanceAttributes["InstanceTransform"] = new InstanceAttribute
            {
                Name = "InstanceTransform",
                Stride = sizeof(Matrix4x4),
                Pointers = new List<InstanceAttributePointer>(DefaultTransformPointers)
            };
        }
        if (!InstanceAttributes.ContainsKey("InstanceNormalTransform"))
        {
            InstanceAttributes["InstanceNormalTransform"] = new InstanceAttribute
            {
                Name = "InstanceNormalTransform",
                Stride = sizeof(Matrix4x4),
                Pointers = new List<InstanceAttributePointer>(DefaultNormalTransformPointers)
            };
        }
    }
    /// <summary>
    /// 从给定的网格创建一个实例化网格节点。
    /// </summary>
    /// <param name="mesh">要实例化的网格。</param>
    /// <returns>创建的实例化网格节点。</returns>
    public static InstancedMesh FromMesh(Mesh mesh)
    {
        if (mesh.Geometry == null)
        {
            throw new ArgumentException("The provided mesh does not contain geometry.");
        }

        var geometry = mesh.Geometry.DeepClone();

        var material = mesh.Material?.DeepClone();

        var instancedMesh = new InstancedMesh
        {
            geometry = geometry,
            Material = material
        };

        instancedMesh.EnsureDefaultAttributes();

        return instancedMesh;
    }

    public override List<IGpuResource> GetGpuResources()
    {
        var list = new List<IGpuResource>()
        {
            this
        };

        if (Material != null)
        {
            foreach (var channel in Material.Channels)
            {
                if (channel.Texture != null && channel.Texture is IGpuResource gpuResource)
                {
                    list.Add(gpuResource);
                }
            }
        }
        return list;
    }

    /// <summary>
    /// 开启或关闭指定逐实例属性的上传。
    /// </summary>
    /// <param name="name">属性名称。</param>
    /// <param name="enabled">是否启用上传。</param>
    public void SetAttributeEnabled(string name, bool enabled)
    {
        if (InstanceAttributes.TryGetValue(name, out var attr))
        {
            attr.Enabled = enabled;
            NeedsUpload = true;
        }
    }

    /// <summary>
    /// 设置通用的逐实例自定义属性。
    /// </summary>
    /// <typeparam name="T">非托管值类型，每个实例的数据元素。</typeparam>
    /// <param name="attribute">内置顶点属性枚举，同时作为名称和 location。</param>
    /// <param name="componentCount">分量数：1=float, 2=vec2, 3=vec3, 4=vec4。</param>
    /// <param name="data">逐实例数据列表，数量必须与 <see cref="InstanceCount"/> 一致。</param>
    public unsafe void SetInstanceAttribute<T>(BuildInVertexAttribute attribute, int componentCount, IReadOnlyList<T> data)
        where T : unmanaged
    {
        if (data.Count != _instanceCount)
            throw new ArgumentException($"数据数量 ({data.Count}) 与实例数量 ({_instanceCount}) 不一致。");

        var name = attribute.ToString();
        int elementSize = sizeof(T) / sizeof(float);
        int stride = componentCount * sizeof(float);
        var floatData = new List<float>(data.Count * componentCount);

        foreach (var item in data)
        {
            float* ptr = (float*)&item;
            for (int i = 0; i < componentCount; i++)
            {
                floatData.Add(i < elementSize ? ptr[i] : 0f);
            }
        }

        InstanceAttributes[name] = new InstanceAttribute
        {
            Name = name,
            Stride = stride,
            Data = floatData,
            Pointers = new List<InstanceAttributePointer>
            {
                new() { Location = (uint)attribute, ComponentCount = componentCount, Offset = 0 }
            }
        };

        NeedsUpload = true;
    }

    public void Destroy(GL gl)
    {
        geometry.Destroy(gl);
        foreach (var attr in InstanceAttributes.Values)
        {
            if (attr.Vbo != 0)
            {
                gl.DeleteBuffer(attr.Vbo);
                attr.Vbo = 0;
            }
        }
    }


    public unsafe void Upload(GL gl)
    {
        if (_instanceCount == 0)
            return;
        if (geometry.Vao == 0)
        {
            geometry.Upload(gl);
        }

        EnsureDefaultAttributes();

        gl.BindVertexArray(geometry.Vao);

        foreach (var attr in InstanceAttributes.Values)
        {
            if (!attr.Enabled)
                continue;

            if (attr.Vbo == 0)
            {
                attr.Vbo = gl.GenBuffer();
            }

            gl.BindBuffer(GLEnum.ArrayBuffer, attr.Vbo);

            fixed (float* p = CollectionsMarshal.AsSpan(attr.Data))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(attr.Data.Count * sizeof(float)), p, GLEnum.DynamicDraw);
            }

            foreach (var ptr in attr.Pointers)
            {
                gl.EnableVertexAttribArray(ptr.Location);
                gl.VertexAttribPointer(ptr.Location, ptr.ComponentCount, GLEnum.Float, false, (uint)attr.Stride, (void*)ptr.Offset);
                gl.VertexAttribDivisor(ptr.Location, 1);
            }
        }

        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindVertexArray(0);
    }
}
