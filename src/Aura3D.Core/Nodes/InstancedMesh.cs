using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Nodes;

/// <summary>
/// 表示一个可用于实例化渲染的网格节点。
/// </summary>
public class InstancedMesh : Node, IGpuResource
{
    public int AddInstance(Matrix4x4 transform)
    {
        instanceTransforms.Add(transform);
        NeedsUpload = true;
        return instanceTransforms.Count - 1;
    }


    public void RemoveInstance(int index)
    {
        instanceTransforms.RemoveAt(index);
        NeedsUpload = true;
    }

    public void UpdateInstance(int index, Matrix4x4 transform)
    {
        instanceTransforms[index] = transform;
        NeedsUpload = true;
    }

    private List<Matrix4x4> instanceTransforms = new();

    private int instanceCount = 0;

    public Material? Material { get; set; }
    public bool NeedsUpload { get; set; }
    private Geometry geometry { get; set; }

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
        return [this];
    }

    public void Destroy(GL gl)
    {
        geometry.Destroy(gl);
    }

    public void Upload(GL gl)
    {
        geometry.Upload(gl);
    }
}
