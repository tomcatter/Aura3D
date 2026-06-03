# 渲染管线

渲染管线决定了场景的视觉风格。Aura3D 提供了多套内置管线，也支持完全自定义。

## 内置管线

### BlinnPhong 管线（默认）

写实风格的前向渲染管线，使用 Blinn-Phong 光照模型。无需额外配置，直接使用 `Aura3DView` 即可。

特性：
- 支持方向光、点光、聚光灯（每类最多 4 盏）
- 支持阴影
- 支持骨骼动画
- 支持透明 / 半透明材质

### NoLight 管线

无光照管线，直接输出材质颜色，适合调试或风格化场景。

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:acr="clr-namespace:Aura3D.Core.Renderers;assembly=Aura3D.Core"
    ...>
    <a:Aura3DView x:TypeArguments="acr:NoLightPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

或通过代码指定：

```csharp
view.CreateRenderPipeline = scene => new NoLightPipeline(scene);
```

## PBR 延迟管线

基于物理的渲染（Physically Based Rendering），使用 Metallic-Roughness 工作流，采用延迟渲染架构。

### 安装

```shell
dotnet add package Aura3D.Pipeline.PBR
```

### 使用

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:pbr="clr-namespace:Aura3D.Pipeline.PBR;assembly=Aura3D.Pipeline.PBR"
    ...>
    <a:Aura3DView x:TypeArguments="pbr:PBRDeferredPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

### 材质配置

PBR 管线使用 PBR 材质参数：

```csharp
var mesh = new Mesh();
mesh.Geometry = new SphereGeometry();
mesh.Material = new Material();

// 基础色
mesh.Material.BaseColor = Texture.CreateFromColor(Color.FromArgb(255, 200, 50, 50));

// 法线贴图
mesh.Material.SetTexture("Normal",
    Texture.CreateFromColor(Color.FromArgb(128, 128, 255)));

// 金属度/粗糙度贴图：R 通道 = 金属度，G 通道 = 粗糙度
mesh.Material.SetTexture("MetallicRoughness",
    Texture.CreateFromColor(Color.FromArgb(200, 100, 0)));

view.AddNode(mesh);
```

## 卡通渲染管线

Cel Shading / Toon Shading 风格的非写实渲染。

### 安装

```shell
dotnet add package Aura3D.Pipeline.CelShading
```

### 使用

```xaml
<Window
    xmlns:a="https://github.com/CeSun/Aura3D"
    xmlns:cel="clr-namespace:Aura3D.Pipeline.CelShading;assembly=Aura3D.Pipeline.CelShading"
    ...>
    <a:Aura3DView x:TypeArguments="cel:CelShadingPipeline"
                  x:Name="aura3Dview"
                  SceneInitialized="OnSceneInitialized"/>
</Window>
```

卡通管线使用方式与默认管线一致——加载模型、设置光源后即可看到效果，渲染风格会自动变为卡通着色。

## 自定义渲染管线

Aura3D 的渲染管线由 **RenderPipeline** 和 **RenderPass** 两部分组成。自定义管线时需要实现这两个类。开发者无需处理 VAO、VBO 等底层细节，但仍需具备基本的渲染知识。

### 架构概览

```
RenderPipeline
  ├── 注册 RenderTarget（帧缓冲 + 纹理附件）
  ├── 注册 RenderPass（渲染步骤，指定输出目标）
  └── 按 RenderPassGroup 调度执行
       ├── Once — 全局执行一次（如 ShadowMap）
       └── EveryCamera — 每个摄像机执行一次（如主渲染）
```

### RenderPipeline

`RenderPipeline` 主要负责注册 RenderPass 和 RenderTarget。

```csharp
public class NoLightPipeline : RenderPipeline
{
    public NoLightPipeline(Scene scene)
    {
        var noLightPass = new NoLightPass(this);

        // 注册 RenderPass（按顺序执行）
        RegisterRenderPass(
            new BackgroundPass(this).SetOutPutRenderTarget("BaseRenderTarget"),
            RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            noLightPass.SetOutPutRenderTarget("BaseRenderTarget"),
            RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            new GammaCorrectionPass(this, "BaseRenderTarget", "Color")
                .SetOutPutRenderTarget("GammaOutput"),
            RenderPassGroup.EveryCamera);

        RegisterRenderPass(
            new FxaaPass(this, "GammaOutput", "Color"),
            RenderPassGroup.EveryCamera);

        // 注册 RenderTarget（帧缓冲）
        RegisterRenderTarget("BaseRenderTarget")
            .AddTexture("Color", TextureFormat.Rgba16f)
            .SetDepthTexture(TextureFormat.DepthComponent16);

        RegisterRenderTarget("GammaOutput")
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(TextureFormat.DepthComponent16);
    }
}
```

**关键 API：**

| 方法 | 说明 |
|---|---|
| `RegisterRenderPass(pass, group)` | 注册渲染步骤，`group` 决定执行时机 |
| `RegisterRenderTarget(name)` | 注册帧缓冲，返回配置器 |
| `AddTexture(name, format)` | 给 RenderTarget 添加颜色附件 |
| `SetDepthTexture(format)` | 给 RenderTarget 添加深度附件 |

**RenderPassGroup 枚举：**
- `EveryCamera` — 每个摄像机执行一次（大多数 Pass 用这个）
- `Once` — 全局执行一次（如 ShadowMap 渲染）

### RenderPass

`RenderPass` 是一段着色器渲染流程。一般一个 Shader（含变体）对应一个 RenderPass。

```csharp
public class NoLightPass : RenderPass
{
    public NoLightPass(RenderPipeline renderPipeline) : base(renderPipeline)
    {
        // 指定着色器源码
        this.FragmentShader = ShaderResource.NoLightFrag;
        this.VertexShader = ShaderResource.NoLightVert;
    }

    public override void Render(Camera camera)
    {
        // 渲染不透明非骨骼网格
        UseShader();
        RenderVisibleMeshesInCamera(
            mesh => !mesh.IsSkinnedMesh
                 && (mesh.Material == null
                     || mesh.Material.BlendMode == BlendMode.Opaque),
            camera.View,
            camera.Projection);

        // 渲染不透明骨骼网格（使用 SKINNED_MESH 宏变体）
        UseShader("SKINNED_MESH");
        RenderVisibleMeshesInCamera(
            mesh => mesh.IsSkinnedMesh
                 && (mesh.Material == null
                     || mesh.Material.BlendMode == BlendMode.Opaque),
            camera.View,
            camera.Projection);
    }
}
```

> 这是简化后的示例写法。实际内置的 `NoLightPipeline` 先遍历所有网格再自行筛选，新编写的管线建议直接使用剔除版本。`mesh.IsSkinnedMesh` / `mesh.IsStaticMesh` 是 `Mesh` 的属性，替代手动判断骨骼逻辑。

**关键 API：**

| 方法 | 说明 |
|---|---|
| `UseShader(params string[] defines)` | 设置着色器宏定义（替换模式），详见 [着色器宏系统](#着色器宏系统) |
| `AddDefines(params string[] defines)` | 追加宏定义（追加模式），`UseShader` 之后调用 |
| `RenderVisibleMeshesInCamera(filter, view, proj)` | 渲染通过视锥体剔除的网格 |

**Mesh 关键属性：**

| 属性 | 说明 |
|---|---|
| `mesh.IsStaticMesh` | 非骨骼网格（返回值 = `!IsSkinnedMesh`） |
| `mesh.IsSkinnedMesh` | 绑定了骨骼的网格（返回值 = `Model != null && Skeleton != null`） |

### 为单个 Mesh 传参

重写 `RenderMesh` 方法，在渲染特定网格前设置 Uniform：

```csharp
public override void RenderMesh(Mesh mesh, Matrix4x4 view, Matrix4x4 projection)
{
    if (someCondition)
    {
        UniformFloat("someParameter", value);
        UniformVector4("someColor", new Vector4(1, 0, 0, 1));
    }

    // 必须设置这些基础矩阵
    UniformMatrix4("viewMatrix", view);
    UniformMatrix4("projectionMatrix", projection);

    base.RenderMesh(mesh, view, projection);
}
```

### 着色器宏系统

Aura3D 的着色器变体通过三个方法协作实现。理解它们的关系是自定义管线的关键。

#### 三个方法的分工

| 方法 | 作用 | GPU 操作 |
|---|---|---|
| `UseShader(params string[] defines)` | **替换** defines 列表 | 无 |
| `AddDefines(params string[] defines)` | **追加** 到已有 defines 列表 | 无 |
| `UseShader_Internal` | 读取 defines，编译/缓存/激活着色器 | `gl.UseProgram` |

`UseShader` 和 `AddDefines` 是**声明式**的——只记录意图，不碰 GPU。真正的编译和绑定发生在 `UseShader_Internal`，它由 `RenderVisibleMeshesInCamera` 等渲染方法在每个 Mesh 渲染前自动调用。

#### 工作流程

典型 Pass 中的执行顺序：

```
1. UseShader("SKINNED_MESH")       → defines = ["SKINNED_MESH"]
2. RenderVisibleMeshesInCamera(...)
   ├─ for each mesh:
   │   UseShader_Internal(mesh)    → 读到 defines = ["SKINNED_MESH"]
   │      缓存 key = "SKINNED_MESH"
   │      命中 → gl.UseProgram     （首次 → 编译 + 缓存）
   │   RenderMesh(mesh, ...)       → 设置 Uniform、gl.DrawElements
   │
3. UseShader("SKINNED_MESH", "BLENDMODE_MASKED")
                                   → defines = ["SKINNED_MESH", "BLENDMODE_MASKED"]
4. RenderVisibleMeshesInCamera(...)
   └─ for each mesh:
       UseShader_Internal(mesh)    → 读到 defines = [...]
          缓存 key = "SKINNED_MESH;BLENDMODE_MASKED"  （不同的 key，不同的变体）
```

#### AddDefines 的使用场景

当一组 Mesh 共享大部分宏定义、仅个别不同时，用 `AddDefines` 追加而非重复声明：

```csharp
// 基础变体
UseShader("SKINNED_MESH");
RenderVisibleMeshesInCamera(filter1, camera.View, camera.Projection);

// 追加一个宏，编译出 SKINNED_MESH + BLENDMODE_MASKED 变体
AddDefines("BLENDMODE_MASKED");
RenderVisibleMeshesInCamera(filter2, camera.View, camera.Projection);
```

#### UseShader_Internal 的两个细节

**1. 两级缓存**

| 缓存层 | 存储位置 | 使用条件 |
|---|---|---|
| Pass 级 | `RenderPass.Shaders["key"]` | 材质无自定义着色器时 |
| Material 级 | `Material.Shaders["key"]` | 材质通过 `SetShaderSource` 覆盖了着色器源码时 |

同一个 defines 组合只编译一次，后续帧直接复用缓存的 `glUseProgram`。

**2. 编译流程**

1. 将 `defines` 列表用 `;` 拼接为缓存 key（如 `"SKINNED_MESH;BLENDMODE_MASKED"`）
2. 若 Material 提供了自定义源码 → 查 Material 缓存，未命中则用 Material 源码编译
3. 否则查 Pass 缓存，未命中则用 Pass 的 `VertexShader`/`FragmentShader` 编译
4. 编译时把 `#define SKINNED_MESH\n#define BLENDMODE_MASKED` 注入到 `//{{defines}}` 位置
5. macOS 自动将 `#version 300 es` 替换为 `#version 330 core`
6. 链接着色器、枚举所有 Uniform 位置并缓存

> **注意**：defines 的顺序影响缓存 key。`UseShader("A").AddDefines("B")` 产生 key `"A;B"`，而 `UseShader("A", "B")` 也产生 `"A;B"`，二者一致。但若先 `UseShader("B")` 再 `AddDefines("A")` 则 key 为 `"B;A"`，是不同变体。建议始终用 `UseShader` 一次性声明所有需要的宏。

#### 着色器源码中的宏标记

GLSL 源码使用 `//{{defines}}` 作为宏注入点：

```glsl
#version 300 es
precision mediump float;

//{{defines}}   ← 编译时自动替换为 #define SKINNED_MESH 等

layout(location = 0) in vec3 position;

#ifdef INSTANCED_MESH
layout(location = 7) in mat4 modelMatrix;
#endif

#ifndef INSTANCED_MESH
uniform mat4 modelMatrix;
#endif
```

#### 手动调用 UseShader_Internal

`UseShader_Internal` 通常由 `RenderVisibleMeshesInCamera` 等网格渲染方法在每个 Mesh 前自动调用。但如果你的 Pass 不遍历 Mesh——例如后处理 Pass 渲染全屏四边形——则需要**手动调用**它。

后处理 Pass 的标准流程：

```
UseShader()           → 声明宏（可选）
UseShader_Internal()  → 编译/激活对应变体
UniformTexture(...)   → 设置输入纹理等 Uniform
RenderQuad()          → 绘制全屏四边形
```

实际例子——伽马校正 Pass（[GammaCorrectionPass.cs](../../src/Aura3D.Core/Renderers/Common/GammaCorrectionPass.cs)）：

```csharp
public override void Render(Camera camera)
{
    BindOutPutRenderTarget(camera);

    var rt = GetRenderTarget(inputRenderTargetName,
        new Size((int)camera.RenderTarget.Width, (int)camera.RenderTarget.Height));

    gl.Disable(EnableCap.DepthTest);
    gl.Disable(EnableCap.Blend);

    UseShader();               // 无宏变体，可省略
    ClearTextureUnit();         // 清空纹理单元计数器
    UseShader_Internal();       // ← 手动激活！当前无 Material 上下文，传入 null
    UniformTexture("colorTexture", rt.GetTexture(inputTextureName));
    RenderQuad();               // 绘制全屏四边形，采样 inputTexture 做伽马校正
}
```

FXAA Pass 同样如此（[FxaaPass.cs](../../src/Aura3D.Core/Renderers/Common/FxaaPass.cs)）：

```csharp
UseShader();
ClearTextureUnit();
UseShader_Internal();
UniformTexture("u_texture", rt.GetTexture(inputTextureName));
UniformVector2("u_textureSize", new Vector2(texWidth, texHeight));
RenderQuad();
```

带有宏变体的后处理——PBR IBL 环境光 Pass（[IBLAmbientPass.cs](../../src/Aura3D.Pipeline.PBR/IBLAmbientPass.cs)）：

```csharp
UseShader("ENBALE_DEFERRED_SHADING");  // 声明宏
UseShader_Internal();                   // 编译带宏的变体并激活
ClearTextureUnit();
UniformTexture("gBufferBaseColor", gBufferBaseColor);
UniformTexture("gBufferNormalRoughness", gBufferNormalRoughness);
// ... 更多 Uniform ...
UniformMatrix4("u_viewMatrix", camera.View);
UniformMatrix4("u_projMatrix", camera.Projection);
RenderQuad();
```

> **关键规则**：`UseShader` / `AddDefines` 必须在 `UseShader_Internal` **之前**调用。`UseShader_Internal` 读取当前 defines 列表来决定激活哪个变体，之后修改 defines 不会影响已激活的着色器。

`RenderQuad()` 和 `RenderCube()` 是 `RenderPass` 提供的内置方法，分别绘制一个覆盖 NDC 空间的四边形和单位立方体，用于后处理和调试。

### 自定义材质的着色器

除了创建完整的 RenderPass，你也可以为单个材质的特定 Pass 替换着色器：

```csharp
var material = new Material();

// 为名为 "LightPass" 的渲染步骤设置自定义着色器
material.SetShaderSource("LightPass", ShaderType.Vertex, vertexShaderSource);
material.SetShaderSource("LightPass", ShaderType.Fragment, fragmentShaderSource);

// 设置着色器参数回调
material.SetShaderPassParametersCallback("LightPass", pass =>
{
    pass.UniformVector4("uColor", new Vector4(1, 0, 0, 1));
});
```

这种方式适合局部定制——只想改变某个特定材质的渲染方式，而不需要创建整个管线。

## 视锥体剔除

任何管线都可以开启视锥体剔除，大幅减少不可见物体的绘制调用：

```csharp
// 开启
view.Scene.RenderPipeline.EnableFrustumCulling = true;

// 关闭
view.Scene.RenderPipeline.EnableFrustumCulling = false;
```

建议在场景中有大量物体时开启，可以显著提升性能。

## Pipeline 生命周期钩子

`RenderPipeline` 和 `RenderPass` 提供了多个虚方法，可在渲染流程的不同阶段插入逻辑：

### RenderPipeline 钩子

```csharp
public class MyPipeline : RenderPipeline
{
    // GL 初始化完成后调用一次（注册 RenderTarget/RenderPass 之后）
    public override void Setup() { }

    // 渲染整帧之前（每帧一次，在所有摄像机之前）
    public override void BeforeRender() { }

    // 渲染整帧之后（每帧一次，在所有摄像机之后）
    public override void AfterRender() { }

    // 每个摄像机渲染前
    public override void BeforeCameraRender(Camera camera) { }

    // 每个摄像机渲染后
    public override void AfterCameraRender(Camera camera) { }

    // 自定义网格排序（如透明度物体按距离排序）
    public override void SortMeshes(List<Mesh> meshes, Camera camera)
    {
        // 默认按材质排序，可覆写
        base.SortMeshes(meshes, camera);
    }
}
```

### RenderPass 钩子

```csharp
public class MyPass : RenderPass
{
    // Pass 首次初始化时调用一次
    public override void Setup() { }

    // 每帧渲染前（Once 类型的 Pass 用这个）
    public override void BeforeRender() { }
    public override void AfterRender() { }

    // 每个摄像机渲染前/后（EveryCamera 类型的 Pass 用这个）
    public override void BeforeRender(Camera camera) { }
    public override void AfterRender(Camera camera) { }
}
```

### 自定义网格筛选

默认应使用带视锥体剔除的渲染方法。剔除版本自动跳过不可见网格，是性能最优的选择。

**首选 — 剔除后渲染：**

```csharp
// 渲染通过视锥体剔除的网格（Mesh）
RenderVisibleMeshesInCamera(filter, camera.View, camera.Projection);

// 渲染通过视锥体剔除的实例化网格（InstancedMesh）
RenderVisibleInstancedMeshesInCamera(filter, camera.View, camera.Projection);
```

典型的不透明 Pass 示例：

```csharp
public override void Render(Camera camera)
{
    // 渲染不透明静态网格
    UseShader();
    RenderVisibleMeshesInCamera(
        mesh => mesh.IsStaticMesh
             && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque),
        camera.View, camera.Projection);

    // 渲染不透明骨骼网格（启用蒙皮宏变体）
    UseShader("SKINNED_MESH");
    RenderVisibleMeshesInCamera(
        mesh => mesh.IsSkinnedMesh
             && (mesh.Material == null || mesh.Material.BlendMode == BlendMode.Opaque),
        camera.View, camera.Projection);

    // 渲染实例化网格
    RenderVisibleInstancedMeshesInCamera(
        im => im.EnableFrustumCulling,
        camera.View, camera.Projection);
}
```

**备选 — 全量渲染（跳过剔除，仅在以下场景使用）：**

- 渲染的对象数量极少，剔除开销大于收益
- 需要按类型遍历而非按可见性（如 `RenderStaticMeshes` / `RenderSkinnedMeshes`）
- 从外部预先筛选好的列表渲染（`RenderMeshesFromList`）
- 调试时临时关闭剔除排查问题

```csharp
// 全部 Mesh（不区分静态/骨骼，不限可见性）
RenderMeshes(filter, camera.View, camera.Projection);

// 仅静态 Mesh
RenderStaticMeshes(filter, camera.View, camera.Projection);

// 仅骨骼 Mesh
RenderSkinnedMeshes(filter, camera.View, camera.Projection);

// 全部实例化网格
RenderInstancedMeshes(filter, camera.View, camera.Projection);

// 从指定列表渲染
RenderMeshesFromList(myMeshList, filter, camera.View, camera.Projection);
```

### 渲染方法速查

| 方法 | 类型 | 剔除 | 推荐度 |
|---|---|---|---|
| `RenderVisibleMeshesInCamera(filter, view, proj)` | Mesh | ✅ | ⭐ 首选 |
| `RenderVisibleInstancedMeshesInCamera(filter, view, proj)` | InstancedMesh | ✅ | ⭐ 首选 |
| `RenderMeshesFromList(list, filter, view, proj)` | Mesh | ❌ | 外部列表场景 |
| `RenderStaticMeshes(filter, view, proj)` | Mesh | ❌ | 按类型遍历 |
| `RenderSkinnedMeshes(filter, view, proj)` | Mesh | ❌ | 按类型遍历 |
| `RenderMeshes(filter, view, proj)` | Mesh | ❌ | 调试/少量物体 |
| `RenderInstancedMeshes(filter, view, proj)` | InstancedMesh | ❌ | 调试/少量物体 |

## 多摄像机渲染

Aura3D 支持同时渲染多个摄像机视角，例如分屏或小地图。

### 创建额外摄像机

```csharp
// 在 SceneInitialized 中创建第二个摄像机
var secondCamera = new Camera
{
    Position = new Vector3(10, 5, 0),
    IsRenderBackground = false  // 第二个视角不重复渲染天空盒
};
secondCamera.LookAt(Vector3.Zero);

// 配置独立的 RenderTarget
scene.AddNode(secondCamera);
```

场景中的所有 `Camera` 节点会被 `RenderPipeline` 自动发现并逐一渲染。每个注册为 `RenderPassGroup.EveryCamera` 的 Pass 会对每个摄像机执行一次。

### 渲染到纹理

通过 `ControlRenderTarget` 可以将某个摄像机的画面渲染到纹理，用于小地图、监控画面等：

```csharp
// 创建离屏渲染目标
var renderTarget = new ControlRenderTarget(width, height);
secondCamera.RenderTarget = renderTarget;

// 渲染后 readTarget 中即为该摄像机的画面
// 可在 SceneUpdated 中读取 RenderTarget 的纹理用作材质输入
```

## 资源管理

### GPU 资源生命周期

所有实现 `IGpuResource` 的对象（Geometry、Material、Texture、RenderTarget 等）由 `RenderPipeline` 统一管理生命周期：

```csharp
// 手动添加资源到管线（通常无需手动调用，AddNode 时自动处理）
view.Scene.RenderPipeline.AddGpuResource(myResource);

// 手动移除
view.Scene.RenderPipeline.RemoveGpuResource(myResource);
```

**IGpuResource 接口：**

| 成员 | 说明 |
|---|---|
| `NeedsUpload` (bool) | 是否需要上传到 GPU |
| `Upload(GL gl)` | 上传数据到 GPU |
| `Destroy(GL gl)` | 销毁 GPU 资源 |

### 遍历模型的所有 GPU 资源

```csharp
// 获取某模型下的全部 GPU 资源（几何体、材质纹理等）
var resources = model.GetGpuResources();
foreach (var res in resources)
{
    // 例如检查是否需要上传
    if (res.NeedsUpload) { /* ... */ }
}
```

### RenderPass 中的上下文方法

```csharp
// 获取指定名称的 RenderTarget
var rt = GetRenderTarget("BaseRenderTarget", new Size(1920, 1080));

// 绑定输出 RenderTarget（由 SetOutPutRenderTarget 自动处理，一般不需手动调用）
BindOutPutRenderTarget(camera);

// 渲染全屏四边形（后处理常用）
RenderQuad();

// 渲染单位立方体（调试/环境贴图用）
RenderCube();
```
