using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Resources;

/// <summary>
/// 骨骼矩阵 Uniform Buffer Object，作为 <see cref="IGpuResource"/> 由 <see cref="IAnimationSampler"/> 持有。
/// 在 <see cref="Upload"/> 时计算蒙皮矩阵（InverseWorldMatrix × BonesTransform）并写入 UBO。
/// </summary>
public class BoneMatrixBuffer : IGpuResource
{
    /// <summary>
    /// 与 shader 中 <c>#define MAX_BONES 256</c> 一致，也是 GLES 3.0 UBO 最小保证值（16KB ÷ 64B）。
    /// </summary>
    public const int MaxBones = 256;

    /// <summary>
    /// UBO 的绑定索引，需与 shader 中 <c>layout(std140) uniform BoneBlock</c> 经
    /// <c>glUniformBlockBinding</c> 绑定到的索引一致。
    /// </summary>
    public const uint BindingIndex = 0;

    private uint _uboHandle;
    private GL? _gl;
    private Matrix4x4[] _matrices = [];

    public Skeleton Skeleton { get; }
    public IAnimationSampler? AnimationSampler { get; }

    /// <inheritdoc/>
    public bool NeedsUpload { get; set; } = true;

    public BoneMatrixBuffer(Skeleton skeleton, IAnimationSampler? animationSampler = null)
    {
        Skeleton = skeleton;
        AnimationSampler = animationSampler;
    }

    /// <inheritdoc/>
    public unsafe void Upload(GL gl)
    {
        _gl = gl;

        int boneCount = Skeleton.Bones.Count;
        if (boneCount == 0)
            return;

        // 懒创建 UBO，大小固定为 MaxBones × 64（与 shader 声明一致）
        if (_uboHandle == 0)
        {
            _uboHandle = gl.GenBuffer();
            gl.BindBuffer(GLEnum.UniformBuffer, _uboHandle);
            gl.BufferData(GLEnum.UniformBuffer, (nuint)(MaxBones * 64), (void*)0, GLEnum.DynamicDraw);
        }

        // 确保本地缓冲区足够大
        if (_matrices.Length < boneCount)
            _matrices = new Matrix4x4[boneCount];

        var sampler = AnimationSampler;
        for (int i = 0; i < boneCount; i++)
        {
            if (sampler != null && i < sampler.BonesTransform.Count)
            {
                _matrices[i] = Skeleton.Bones[i].InverseWorldMatrix * sampler.BonesTransform[i];
            }
            else
            {
                _matrices[i] = Skeleton.Bones[i].InverseWorldMatrix * Skeleton.Bones[i].WorldMatrix;
            }
        }

        int uploadCount = boneCount < MaxBones ? boneCount : MaxBones;
        fixed (Matrix4x4* ptr = _matrices)
        {
            gl.BindBuffer(GLEnum.UniformBuffer, _uboHandle);
            gl.BufferSubData(GLEnum.UniformBuffer, 0, (nuint)(uploadCount * 64), ptr);
        }
    }

    /// <summary>
    /// 在渲染前将 UBO 绑定到 binding point。
    /// </summary>
    public void Bind()
    {
        if (_gl != null && _uboHandle != 0)
        {
            _gl.BindBufferBase(GLEnum.UniformBuffer, BindingIndex, _uboHandle);
        }
    }

    /// <inheritdoc/>
    public void Destroy(GL gl)
    {
        if (_uboHandle != 0)
        {
            gl.DeleteBuffer(_uboHandle);
            _uboHandle = 0;
        }
        _gl = null;
        NeedsUpload = true;
    }
}
