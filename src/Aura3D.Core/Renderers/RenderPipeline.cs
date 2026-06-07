using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 渲染管线创建实例的接口，用于通过场景创建对应的渲染管线实例。
/// </summary>
public interface IRenderPipelineCreateInstance
{
    /// <summary>
    /// 使用指定的场景创建渲染管线实例。
    /// </summary>
    /// <param name="scene">要渲染的场景。</param>
    /// <returns>新创建的渲染管线实例。</returns>
    public abstract static RenderPipeline CreateInstance(Scene scene);
}

/// <summary>
/// 渲染管线的抽象基类，负责管理场景中的网格、相机、光源、GPU 资源以及组织渲染流程。
/// </summary>
public abstract partial class RenderPipeline
{
    /// <summary>
    /// 初始化 <see cref="RenderPipeline"/> 类的新实例。
    /// </summary>
    /// <param name="scene">要关联的场景。</param>
    public RenderPipeline(Scene scene)
    {
        this.Scene = scene;
        this.Settings = scene.PipelineSettings;
    }

    /// <summary>
    /// 获取渲染管线的用户可配置设置。
    /// </summary>
    public PipelineSettings Settings { get; }

    /// <summary>
    /// 获取当前渲染管线是否支持 CSM（级联阴影贴图）。
    /// 默认返回 <c>false</c>；支持多级联阴影采样的管线应重写此属性返回 <c>true</c>。
    /// <see cref="ShadowMapPass"/> 在渲染时会检查此属性，
    /// 不支持 CSM 的管线即使 <see cref="PipelineSettings.CsmCascadeCount"/> 大于 1 也会退化为普通单张阴影贴图。
    /// </summary>
    public virtual bool SupportsCSM => false;

    /// <summary>
    /// 获取或设置是否启用视锥体剔除。
    /// </summary>
    public bool EnableFrustumCulling
    {
        get => Settings.EnableFrustumCulling;
        set => Settings.EnableFrustumCulling = value;
    }

    /// <summary>
    /// 获取当前渲染管线关联的场景。
    /// </summary>
    public Scene Scene { get; private set; }

    /// <summary>
    /// 获取场景中的所有网格节点列表。
    /// </summary>
    public List<Mesh> Meshes { get; } = new List<Mesh>();


    public List<InstancedMesh> InstancedMeshes { get; } = new List<InstancedMesh>();

    public List<ParticleSystem> ParticleSystems { get; } = new List<ParticleSystem>();

    /// <summary>
    /// 获取场景中的所有相机节点列表。
    /// </summary>
    public List<Camera> Cameras { get; } = new List<Camera>();

    /// <summary>
    /// 获取场景中的所有点光源列表。
    /// </summary>
    public List<PointLight> PointLights { get; } = new List<PointLight>();

    /// <summary>
    /// 获取场景中的所有聚光灯列表。
    /// </summary>
    public List<SpotLight> SpotLights { get; } = new List<SpotLight>();

    /// <summary>
    /// 获取场景中的所有方向光源列表。
    /// </summary>
    public List<DirectionalLight> DirectionalLights { get; } = new List<DirectionalLight>();

    /// <summary>
    /// 获取或设置默认的帧缓冲对象标识符。
    /// </summary>
    public uint DefaultFramebuffer { get; set; }

    /// <summary>
    /// 获取或设置 OpenGL ES 上下文对象。
    /// </summary>
    public GL? gl { get; protected set; }

    /// <summary>
    /// 获取每个相机渲染时都需要执行的渲染通道列表。
    /// </summary>
    public List<RenderPass> EveryCameraRenderPasses { get; } = new List<RenderPass>();

    /// <summary>
    /// 获取每帧仅执行一次的渲染通道列表。
    /// </summary>
    public List<RenderPass> OnceRenderPasses { get; } = new List<RenderPass>();

    /// <summary>
    /// 获取当前渲染管线管理的所有 GPU 资源集合。
    /// </summary>
    public HashSet<IGpuResource> GpuResources { get; } = new HashSet<IGpuResource>();

    /// <summary>
    /// GPU 资源引用计数，跟踪每个资源被多少个节点引用。
    /// </summary>
    Dictionary<IGpuResource, int> _gpuResourceRefCounts = new();

    /// <summary>
    /// 延迟卸载缓存，ref count 归零的资源移入此处，等待超时后真正销毁。
    /// </summary>
    Dictionary<IGpuResource, DateTime> _pendingDestroyResources = new();

    /// <summary>
    /// 获取或设置延迟卸载超时时间（秒）。移除节点后 GPU 资源不会立即销毁，
    /// 而是缓存该时长，若在此期间重新添加则可复用。默认 5 秒。
    /// </summary>
    public float ResourceUnloadDelay { get; set; } = 5f;

    /// <summary>
    /// 获取需要更新上传的 GPU 资源集合。
    /// </summary>
    public HashSet<IGpuResource> NeedUpdateResources { get; } = new HashSet<IGpuResource>();

    /// <summary>
    /// 获取或设置方向光源的最大数量限制。
    /// </summary>
    public int DirectionalLightLimit
    {
        get => Settings.DirectionalLightLimit;
        set => Settings.DirectionalLightLimit = value;
    }

    /// <summary>
    /// 获取或设置点光源的最大数量限制。
    /// </summary>
    public int PointLightLimit
    {
        get => Settings.PointLightLimit;
        set => Settings.PointLightLimit = value;
    }

    /// <summary>
    /// 获取或设置聚光灯的最大数量限制。
    /// </summary>
    public int SpotLightLimit
    {
        get => Settings.SpotLightLimit;
        set => Settings.SpotLightLimit = value;
    }

    private int lastDirectionalLightLimit;

    private int lastPointLightLimit;

    private int lastSpotLightLimit;

    protected event Action<int, int, int>? LightLimitChangedEvent;

    /// <summary>
    /// 获取或设置当前相机视锥体中可见的网格列表。
    /// </summary>
    public List<Mesh> VisibleMeshesInCamera = [];

    /// <summary>
    /// 当前相机视锥体中可见的实例化网格列表。
    /// </summary>
    public List<InstancedMesh> VisibleInstancedMeshesInCamera = [];


    protected void RegisterRenderPass(RenderPass renderPass, RenderPassGroup renderPassGroup)
    {
        if (renderPassGroup == RenderPassGroup.EveryCamera)
            EveryCameraRenderPasses.Add(renderPass);
        else if (renderPassGroup == RenderPassGroup.Once)
            OnceRenderPasses.Add(renderPass);
    }

    /// <summary>
    /// 渲染通道分组枚举，用于标识渲染通道的执行频率。
    /// </summary>
    public enum RenderPassGroup
    {
        Once,
        EveryCamera,
    }

    /// <summary>
    /// 使用指定的获取函数指针委托初始化渲染管线，包括 OpenGL 上下文获取与渲染通道设置。
    /// </summary>
    /// <param name="getProcAddressFunctionPtr">用于获取 OpenGL 函数指针的委托。</param>
    public void Initialize(Func<string, nint> getProcAddressFunctionPtr)
    {
        gl = GL.GetApi(getProcAddressFunctionPtr);

        Setup();

        foreach (var renderPass in EveryCameraRenderPasses)
        {
            renderPass.Setup();
        }
        foreach (var renderPass in OnceRenderPasses)
        {
            renderPass.Setup();
        }
    }

    /// <summary>
    /// 在初始化时执行管线的自定义设置逻辑，子类可重写以添加特定资源初始化。
    /// </summary>
    public virtual void Setup()
    {

    }

    /// <summary>
    /// 更新所有 GPU 资源，上传新增或已标记需要更新的资源到 GPU，并清理已移除资源。
    /// </summary>
    public void UpdateGpuResources()
    {
        foreach(var (isAdd, gpuResource) in modifyGpuResourceList)
        {
            if (isAdd)
            {
                if (_pendingDestroyResources.Remove(gpuResource))
                {
                    // 从延迟卸载缓存中取回，GPU 数据仍在，无需重新上传
                    GpuResources.Add(gpuResource);
                    _gpuResourceRefCounts[gpuResource] = 1;
                }
                else if (GpuResources.Contains(gpuResource))
                {
                    // 已在活跃集合中，递增引用计数
                    _gpuResourceRefCounts[gpuResource]++;
                }
                else
                {
                    // 新资源
                    GpuResources.Add(gpuResource);
                    _gpuResourceRefCounts[gpuResource] = 1;
                }
            }
            else
            {
                if (_gpuResourceRefCounts.TryGetValue(gpuResource, out int refCount))
                {
                    refCount--;
                    if (refCount > 0)
                    {
                        // 仍有其他节点引用，仅递减引用计数
                        _gpuResourceRefCounts[gpuResource] = refCount;
                    }
                    else
                    {
                        // 引用归零，移入延迟卸载缓存
                        GpuResources.Remove(gpuResource);
                        _gpuResourceRefCounts.Remove(gpuResource);
                        _pendingDestroyResources[gpuResource] = DateTime.Now;
                    }
                }
            }
        }
        modifyGpuResourceList.Clear();

        // 检查延迟卸载缓存中超时的资源，真正销毁
        var now = DateTime.Now;
        var expiredResources = new List<IGpuResource>();
        foreach (var (resource, removeTime) in _pendingDestroyResources)
        {
            if ((now - removeTime).TotalSeconds >= ResourceUnloadDelay)
            {
                expiredResources.Add(resource);
            }
        }
        foreach (var resource in expiredResources)
        {
            _pendingDestroyResources.Remove(resource);
            resource.Destroy(gl!);
            resource.NeedsUpload = true;
        }

        foreach (var gpuResource in GpuResources)
        {
            if (gpuResource.NeedsUpload == true)
            {
                gpuResource.Upload(gl!);
                gpuResource.NeedsUpload = false;
            }
        }
    }

    /// <summary>
    /// 将指定的 GPU 资源添加到当前渲染管线中。
    /// </summary>
    /// <param name="gpuResource">要添加的 GPU 资源。</param>
    public void AddGpuResource(IGpuResource gpuResource)
    {
        modifyGpuResourceList.Add((true, gpuResource));
    }

    /// <summary>
    /// 从当前渲染管线中移除指定的 GPU 资源。
    /// </summary>
    /// <param name="gpuResource">要移除的 GPU 资源。</param>
    public void RemoveGpuResource(IGpuResource gpuResource)
    {
        modifyGpuResourceList.Add((false, gpuResource));
    }

    List<(bool isAdd, IGpuResource gpuResource)> modifyGpuResourceList = [];

    /// <summary>
    /// 将节点添加到当前渲染管线，并根据节点类型分类管理。
    /// </summary>
    /// <param name="node">要添加的场景节点。</param>
    public void AddNode(Node node)
    {
        switch (node)
        {
            case Mesh mesh:
                Meshes.Add(mesh);
                break;
            case InstancedMesh instancedMesh:
                InstancedMeshes.Add(instancedMesh);
                break;
            case Camera camera:
                Cameras.Add(camera);
                break;
            case PointLight pointLight:
                PointLights.Add(pointLight);
                break;
            case SpotLight spotLight:
                SpotLights.Add(spotLight);
                break;
            case DirectionalLight directionalLight:
                DirectionalLights.Add(directionalLight);
                break;
            case ParticleSystem particleSystem:
                ParticleSystems.Add(particleSystem);
                break;
        }

        foreach (var gpuResource in node.GetGpuResources())
        {
            AddGpuResource(gpuResource);
        }

        node.InitializeReportedGpuResources();
    }

    /// <summary>
    /// 从当前渲染管线中移除指定节点，并清理其关联的 GPU 资源。
    /// </summary>
    /// <param name="node">要移除的场景节点。</param>
    public void RemoveNode(Node node)
    {
        switch (node)
        {
            case Mesh mesh:
                Meshes.Remove(mesh);
                break;
            case InstancedMesh instancedMesh:
                InstancedMeshes.Remove(instancedMesh);
                break;
            case Camera camera:
                Cameras.Remove(camera);
                break;
            case PointLight pointLight:
                PointLights.Remove(pointLight);
                break;
            case SpotLight spotLight:
                SpotLights.Remove(spotLight);
                break;
            case DirectionalLight directionalLight:
                DirectionalLights.Remove(directionalLight);
                break;
            case ParticleSystem particleSystem:
                ParticleSystems.Remove(particleSystem);
                break;
        }
        foreach(var gpuResource in node.GetGpuResources())
        {
            RemoveGpuResource(gpuResource);
        }

        node.ClearPipelineGpuResources();
    }

    private void UpdateLightLimit()
    {
        if (lastPointLightLimit != PointLightLimit || lastSpotLightLimit != SpotLightLimit || lastDirectionalLightLimit != DirectionalLightLimit)
        {
            lastPointLightLimit = PointLightLimit;
            lastSpotLightLimit = SpotLightLimit;
            lastDirectionalLightLimit = DirectionalLightLimit;
            LightLimitChangedEvent?.Invoke(lastDirectionalLightLimit, lastPointLightLimit, lastSpotLightLimit);
        }
    }

    /// <summary>
    /// 执行一帧的完整渲染流程，包括更新渲染目标、光源限制、GPU 资源，以及执行所有渲染通道。
    /// </summary>
    public virtual void Render()
    {
        UpdateRenderTargetsLRU();
        UpdateLightLimit();
        UpdateGpuResources();

        BeforeRender();
        foreach (var renderPass in OnceRenderPasses)
        {
            renderPass.BeforeRender();
            renderPass.Render();
            renderPass.AfterRender();
        }

        foreach (var camera in Cameras)
        {
            if (camera.Enable == false)
                continue;

            VisibleMeshesInCamera.Clear();
            VisibleInstancedMeshesInCamera.Clear();
            if (EnableFrustumCulling == true)
            {
                UpdateVisibleMeshesInCamera(camera.View, camera.Projection, VisibleMeshesInCamera);
                UpdateVisibleInstancedMeshesInCamera(camera.View, camera.Projection, VisibleInstancedMeshesInCamera);
            }
            else
            {
                VisibleMeshesInCamera.AddRange(Meshes);
                VisibleInstancedMeshesInCamera.AddRange(InstancedMeshes);
            }

            BeforeCameraRender(camera);
            foreach (var renderPass in EveryCameraRenderPasses)
            {
                renderPass.BeforeRender(camera);
                renderPass.Render(camera);
                renderPass.AfterRender(camera);
            }
            AfterRender();
        }
        AfterRender();
    }

    private Plane[] planes = new Plane[6];

    /// <summary>
    /// 根据相机的视图和投影矩阵更新当前视锥体中可见的网格列表。
    /// </summary>
    /// <param name="view">视图矩阵。</param>
    /// <param name="projection">投影矩阵。</param>
    /// <param name="meshes">用于输出可见网格的列表。</param>
    public void UpdateVisibleMeshesInCamera(Matrix4x4 view, Matrix4x4 projection, List<Mesh> meshes)
    {
        var viewProjection = view * projection;

        Matrix4x4.Invert(viewProjection, out Matrix4x4 invViewProj);

        Span<Vector3> ndcCorners = stackalloc Vector3[]
        {
            new Vector3(-1,-1,-1), new Vector3(1,-1,-1),
            new Vector3(-1, 1,-1), new Vector3(1, 1,-1),
            new Vector3(-1,-1, 1), new Vector3(1,-1, 1),
            new Vector3(-1, 1, 1), new Vector3(1, 1, 1)
        };

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var c in ndcCorners)
        {
            Vector4 p = new Vector4(c, 1.0f);
            Vector4 world = Vector4.Transform(p, invViewProj);
            world /= world.W;

            Vector3 wpos = new Vector3(world.X, world.Y, world.Z);
            min = Vector3.Min(min, wpos);
            max = Vector3.Max(max, wpos);
        }

        var cameraBoundingBox = new BoundingBox(min, max);

        MatrixHelper.ExtractPlanes(viewProjection, planes);

        // 八叉树查询（所有网格统一走八叉树）
        this.Scene.MeshOctree.Query(boundingBox =>
        {
            if (cameraBoundingBox.Intersects(boundingBox))
            {
                if (boundingBox.IsBoxInsideFrustum(planes))
                {
                    return true;
                }
            }
            return false;

        }, meshes);
    }

    /// <summary>
    /// 根据相机的视图和投影矩阵更新当前视锥体中可见的实例化网格列表。
    /// 对每个 InstancedMesh 的合并世界包围盒进行视锥体测试，
    /// 同时尊重每个 InstancedMesh 自身的 <see cref="InstancedMesh.EnableFrustumCulling"/> 设置。
    /// </summary>
    /// <param name="view">视图矩阵。</param>
    /// <param name="projection">投影矩阵。</param>
    /// <param name="instancedMeshes">用于输出可见实例化网格的列表。</param>
    public void UpdateVisibleInstancedMeshesInCamera(Matrix4x4 view, Matrix4x4 projection, List<InstancedMesh> instancedMeshes)
    {
        var viewProjection = view * projection;
        MatrixHelper.ExtractPlanes(viewProjection, planes);

        foreach (var im in InstancedMeshes)
        {
            if (im.Enable == false)
                continue;
            if (im.InstanceCount == 0)
                continue;
            if (im.EnableFrustumCulling == false)
            {
                instancedMeshes.Add(im);
                continue;
            }
            if (im.IsInsideFrustum(planes))
            {
                instancedMeshes.Add(im);
            }
        }
    }

    /// <summary>
    /// 在所有相机渲染之前执行的逻辑，子类可重写。
    /// </summary>
    public virtual void BeforeRender()
    {

    }

    /// <summary>
    /// 在所有相机渲染之后执行的逻辑，子类可重写。
    /// </summary>
    public virtual void AfterRender()
    {

    }

    /// <summary>
    /// 在单个相机渲染之前执行的逻辑，子类可重写。
    /// </summary>
    /// <param name="camera">当前要渲染的相机。</param>
    public virtual void BeforeCameraRender(Camera camera)
    {

    }

    /// <summary>
    /// 在单个相机渲染之后执行的逻辑，子类可重写。
    /// </summary>
    /// <param name="camera">当前已渲染完成的相机。</param>
    public virtual void AfterCameraRender(Camera camera)
    {
    }

    /// <summary>
    /// 根据网格与相机的距离对网格列表进行排序，用于透明物体的正确渲染。
    /// </summary>
    /// <param name="Meshes">要排序的网格列表。</param>
    /// <param name="camera">用于计算距离的相机。</param>
    public virtual void SortMeshes(List<Mesh> Meshes, Camera camera)
    {
        var m = camera.View;

        Meshes.Sort((mesh1, mesh2) =>
        {
            var location1 = Vector3.Transform(mesh1.Position, mesh1.WorldTransform * m);

            var location2 = Vector3.Transform(mesh2.Position, mesh2.WorldTransform * m);

            var l1 = location1.Length();

            var l2 = location2.Length();

            return (l1).CompareTo(l2);

        });
    }

    private InternalCube? _internalCube;

    private InternalQuad? _internalQuad;

    /// <summary>
    /// 渲染一个全屏单位立方体，常用于天空盒或环境贴图渲染。
    /// </summary>
    public void RenderCube()
    {
        if (gl == null)
            return;
        if (_internalCube == null)
        {
            _internalCube = new InternalCube();
            _internalCube.Upload(gl);
            _internalCube.NeedsUpload = false;
            GpuResources.Add(_internalCube);
            _gpuResourceRefCounts[_internalCube] = 1;
        }
        gl.BindVertexArray(_internalCube.Vao);
        gl.DrawArrays(GLEnum.Triangles, 0, 36);
    }

    /// <summary>
    /// 渲染一个全屏四边形，常用于后处理全屏效果的绘制。
    /// </summary>
    public unsafe void RenderQuad()
    {
        if (gl == null)
            return;
        if (_internalQuad == null)
        {
            _internalQuad = new InternalQuad();
            _internalQuad.Upload(gl!);
            _internalQuad.NeedsUpload = false;
            GpuResources.Add(_internalQuad);
            _gpuResourceRefCounts[_internalQuad] = 1;
        }
        gl.BindVertexArray(_internalQuad.Vao);
        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
    }

    /// <summary>
    /// 销毁当前渲染管线及其管理的所有 GPU 资源和渲染通道。
    /// </summary>
    public virtual void Destroy()
    {
        foreach(var gpuResource in GpuResources)
        {
            gpuResource.Destroy(gl!);
            gpuResource.NeedsUpload = true;
        }

        GpuResources.Clear();
        _gpuResourceRefCounts.Clear();

        foreach (var gpuResource in _pendingDestroyResources.Keys)
        {
            gpuResource.Destroy(gl!);
            gpuResource.NeedsUpload = true;
        }
        _pendingDestroyResources.Clear();

        foreach (var pass in OnceRenderPasses)
        {
            pass.Destroy();
        }
        foreach (var pass in EveryCameraRenderPasses)
        {
            pass.Destroy();
        }

        Meshes.Clear();

        Cameras.Clear();

        PointLights.Clear();

        SpotLights.Clear();
    }
}

class InternalCube : IGpuResource
{
    public uint Vao;

    public uint Vbo;

    public bool NeedsUpload { get; set; } = true;

    public void Destroy(GL gl)
    {
        if (Vao != 0)
        {
            gl.DeleteVertexArray(Vao);
            Vao = 0;
        }
        if (Vbo != 0)
        {
            gl.DeleteBuffer(Vbo);
            Vbo = 0;
        }
    }

    public unsafe void Upload(GL gl)
    {
        float[] vertices =
            [
                // back face
                -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 0.0f, 0.0f, // bottom-left
                 1.0f,  1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 1.0f, 1.0f, // top-right
                 1.0f, -1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 1.0f, 0.0f, // bottom-right         
                 1.0f,  1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 1.0f, 1.0f, // top-right
                -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 0.0f, 0.0f, // bottom-left
                -1.0f,  1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 0.0f, 1.0f, // top-left
                // front face
                -1.0f, -1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f, 0.0f, // bottom-left
                 1.0f, -1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f, 0.0f, // bottom-right
                 1.0f,  1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f, 1.0f, // top-right
                 1.0f,  1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f, 1.0f, // top-right
                -1.0f,  1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f, 1.0f, // top-left
                -1.0f, -1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f, 0.0f, // bottom-left
                // left face
                -1.0f,  1.0f,  1.0f, -1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-right
                -1.0f,  1.0f, -1.0f, -1.0f,  0.0f,  0.0f, 1.0f, 1.0f, // top-left
                -1.0f, -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-left
                -1.0f, -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-left
                -1.0f, -1.0f,  1.0f, -1.0f,  0.0f,  0.0f, 0.0f, 0.0f, // bottom-right
                -1.0f,  1.0f,  1.0f, -1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-right
                // right face
                 1.0f,  1.0f,  1.0f,  1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-left
                 1.0f, -1.0f, -1.0f,  1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-right
                 1.0f,  1.0f, -1.0f,  1.0f,  0.0f,  0.0f, 1.0f, 1.0f, // top-right         
                 1.0f, -1.0f, -1.0f,  1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-right
                 1.0f,  1.0f,  1.0f,  1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-left
                 1.0f, -1.0f,  1.0f,  1.0f,  0.0f,  0.0f, 0.0f, 0.0f, // bottom-left     
                // bottom face
                -1.0f, -1.0f, -1.0f,  0.0f, -1.0f,  0.0f, 0.0f, 1.0f, // top-right
                 1.0f, -1.0f, -1.0f,  0.0f, -1.0f,  0.0f, 1.0f, 1.0f, // top-left
                 1.0f, -1.0f,  1.0f,  0.0f, -1.0f,  0.0f, 1.0f, 0.0f, // bottom-left
                 1.0f, -1.0f,  1.0f,  0.0f, -1.0f,  0.0f, 1.0f, 0.0f, // bottom-left
                -1.0f, -1.0f,  1.0f,  0.0f, -1.0f,  0.0f, 0.0f, 0.0f, // bottom-right
                -1.0f, -1.0f, -1.0f,  0.0f, -1.0f,  0.0f, 0.0f, 1.0f, // top-right
                // top face
                -1.0f,  1.0f, -1.0f,  0.0f,  1.0f,  0.0f, 0.0f, 1.0f, // top-left
                 1.0f,  1.0f , 1.0f,  0.0f,  1.0f,  0.0f, 1.0f, 0.0f, // bottom-right
                 1.0f,  1.0f, -1.0f,  0.0f,  1.0f,  0.0f, 1.0f, 1.0f, // top-right     
                 1.0f,  1.0f,  1.0f,  0.0f,  1.0f,  0.0f, 1.0f, 0.0f, // bottom-right
                -1.0f,  1.0f, -1.0f,  0.0f,  1.0f,  0.0f, 0.0f, 1.0f, // top-left
                -1.0f,  1.0f,  1.0f,  0.0f,  1.0f,  0.0f, 0.0f, 0.0f  // bottom-left        
            ];
        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        // fill buffer
        gl.BindVertexArray(Vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, Vbo);
        fixed (void* p = vertices)
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)vertices.Length * sizeof(float), p, GLEnum.StaticDraw);
        }
        // link vertex attributes
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, 8 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, GLEnum.Float, false, 8 * sizeof(float), (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 2, GLEnum.Float, false, 8 * sizeof(float), (void*)(6 * sizeof(float)));
        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindVertexArray(0);
    }
}

class InternalQuad : IGpuResource
{
    public uint Vao;

    public uint Vbo;

    public uint Ebo;

    public bool NeedsUpload { get; set; } = true;

    struct QuadVertex
    {
        public Vector3 Location;
        public Vector2 TexCoord;
    }

    public void Destroy(GL gl)
    {
        if (Vao != 0)
        {
            gl.DeleteVertexArray(Vao);
            Vao = 0;
        }
        if (Vbo != 0)
        {
            gl.DeleteBuffer(Vbo);
            Vbo = 0;
        }
        if (Ebo != 0)
        {
            gl.DeleteBuffer(Ebo);
            Ebo = 0;
        }
    }

    public unsafe void Upload(GL gl)
    {
        QuadVertex* vertices = stackalloc QuadVertex[4] {
            new () {Location = new Vector3(-1, 1, 0), TexCoord = new Vector2(0, 1) },
            new () {Location = new Vector3(-1, -1, 0), TexCoord = new Vector2(0, 0) },
            new () {Location = new Vector3(1, -1, 0), TexCoord = new Vector2(1, 0) },
            new () {Location = new Vector3(1, 1, 0), TexCoord = new Vector2(1, 1) },
        };

        uint* indices = stackalloc uint[6]
        {
            0, 1, 2, 2, 3,0
        };

        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ebo = gl.GenBuffer();

        gl.BindVertexArray(Vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, Vbo);
        gl.BufferData(GLEnum.ArrayBuffer, (nuint)(4 * sizeof(QuadVertex)), vertices, GLEnum.StaticDraw);
        
        gl.BindBuffer(GLEnum.ElementArrayBuffer, Ebo);
        gl.BufferData(GLEnum.ElementArrayBuffer, 6 * sizeof(uint), indices, GLEnum.StaticDraw);
        
        // Location
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(QuadVertex), (void*)0);
        // TexCoord
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, (uint)sizeof(QuadVertex), (void*)sizeof(Vector3));
        gl.BindVertexArray(0);
    }
}
