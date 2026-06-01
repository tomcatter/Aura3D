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
    public int AddInstance(Matrix4x4 transform)
    {
        instanceTransforms.Add(transform);

        var normalMatrix = transform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        instanceNormalTransforms.Add(normalMatrix);

        NeedsUpload = true;
        return instanceTransforms.Count - 1;
    }


    public void RemoveInstance(int index)
    {
        instanceTransforms.RemoveAt(index);
        instanceNormalTransforms.RemoveAt(index);
        NeedsUpload = true;
    }

    public void UpdateInstance(int index, Matrix4x4 transform)
    {
        instanceTransforms[index] = transform;

        var normalMatrix = transform.Inverse();
        normalMatrix = Matrix4x4.Transpose(normalMatrix);
        instanceNormalTransforms[index] = normalMatrix;

        NeedsUpload = true;
    }

    private List<Matrix4x4> instanceTransforms = new();

    private List<Matrix4x4> instanceNormalTransforms = new();

    private uint instanceVbo;

    private uint instanceNormalVbo;

    public Material? Material { get; set; }

    public bool NeedsUpload { get; set; }

    private Geometry geometry { get; set; }

    public uint Vao => geometry.Vao;

    public int IndicesCount => geometry.IndicesCount;

    public int InstanceCount => instanceTransforms.Count;
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


        return instancedMesh;
    }

    public override List<IGpuResource> GetGpuResources()
    {
        if (Material == null)
        {
            return [this];
        }
        else
        {
            return [this, Material];
        }
    }

    public void Destroy(GL gl)
    {
        geometry.Destroy(gl);
        if (instanceVbo != 0)
        {
            gl.DeleteBuffer(instanceVbo);
            instanceVbo = 0;
        }
        if (instanceNormalVbo != 0)
        {
            gl.DeleteBuffer(instanceNormalVbo);
            instanceNormalVbo = 0;
        }
    }


    public unsafe void Upload(GL gl)
    {
        if (instanceTransforms.Count == 0)
            return;
        if (geometry.Vao == 0)
        {
            geometry.Upload(gl);
        }

        if (instanceVbo == 0)
        {
            instanceVbo = gl.GenBuffer();
        }

        gl.BindVertexArray(geometry.Vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, instanceVbo);

        fixed(void* p = CollectionsMarshal.AsSpan(instanceTransforms))
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(instanceTransforms.Count * sizeof(Matrix4x4)), p, GLEnum.DynamicDraw);
        }

        gl.EnableVertexAttribArray((int)BuildInVertexAttribute.InstancedTransformColumn0);
        gl.VertexAttribPointer((int)BuildInVertexAttribute.InstancedTransformColumn0, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)0);
        gl.EnableVertexAttribArray((int)BuildInVertexAttribute.InstancedTransformColumn1);
        gl.VertexAttribPointer((int)BuildInVertexAttribute.InstancedTransformColumn1, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)sizeof(Vector4));
        gl.EnableVertexAttribArray((int)BuildInVertexAttribute.InstancedTransformColumn2);
        gl.VertexAttribPointer((int)BuildInVertexAttribute.InstancedTransformColumn2, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)(sizeof(Vector4) * 2));
        gl.EnableVertexAttribArray((int)BuildInVertexAttribute.InstancedTransformColumn3);
        gl.VertexAttribPointer((int)BuildInVertexAttribute.InstancedTransformColumn3, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)(sizeof(Vector4) * 3));



        gl.VertexAttribDivisor((int)BuildInVertexAttribute.InstancedTransformColumn0, 1);
        gl.VertexAttribDivisor((int)BuildInVertexAttribute.InstancedTransformColumn1, 1);
        gl.VertexAttribDivisor((int)BuildInVertexAttribute.InstancedTransformColumn2, 1);
        gl.VertexAttribDivisor((int)BuildInVertexAttribute.InstancedTransformColumn3, 1);



        if (instanceNormalVbo == 0)
        {
            instanceNormalVbo = gl.GenBuffer();
        }
        gl.BindBuffer(GLEnum.ArrayBuffer, instanceNormalVbo);

        fixed (void* p = CollectionsMarshal.AsSpan(instanceNormalTransforms))
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(instanceNormalTransforms.Count * sizeof(Matrix4x4)), p, GLEnum.DynamicDraw);
        }

        gl.EnableVertexAttribArray((int)BuildInVertexAttribute.InstancedNormalTransformColumn0);
        gl.VertexAttribPointer((int)BuildInVertexAttribute.InstancedNormalTransformColumn0, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)0);
        gl.EnableVertexAttribArray((int)BuildInVertexAttribute.InstancedNormalTransformColumn1);
        gl.VertexAttribPointer((int)BuildInVertexAttribute.InstancedNormalTransformColumn1, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)sizeof(Vector4));
        gl.EnableVertexAttribArray((int)BuildInVertexAttribute.InstancedNormalTransformColumn2);
        gl.VertexAttribPointer((int)BuildInVertexAttribute.InstancedNormalTransformColumn2, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)(sizeof(Vector4) * 2));
        gl.EnableVertexAttribArray((int)BuildInVertexAttribute.InstancedNormalTransformColumn3);
        gl.VertexAttribPointer((int)BuildInVertexAttribute.InstancedNormalTransformColumn3, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)(sizeof(Vector4) * 3));



        gl.VertexAttribDivisor((int)BuildInVertexAttribute.InstancedNormalTransformColumn0, 1);
        gl.VertexAttribDivisor((int)BuildInVertexAttribute.InstancedNormalTransformColumn1, 1);
        gl.VertexAttribDivisor((int)BuildInVertexAttribute.InstancedNormalTransformColumn2, 1);
        gl.VertexAttribDivisor((int)BuildInVertexAttribute.InstancedNormalTransformColumn3, 1);

        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindVertexArray(0);
    }
}
