# 渲染专题

涵盖阴影、点云、图元渲染以及节点操作等渲染相关的进阶主题。

## 阴影

三种光源均支持阴影投射，通过 `CastShadow` 属性开启。

### 方向光阴影

```csharp
var dl = new DirectionalLight();
dl.CastShadow = true;
dl.ShadowConfig.FarPlane = 500;    // 远裁剪面
dl.ShadowConfig.NearPlane = 1;     // 近裁剪面
dl.ShadowConfig.Width = 2048;      // Shadow Map 分辨率宽度
dl.ShadowConfig.Height = 2048;     // Shadow Map 分辨率高度
```

### 点光阴影

```csharp
var pl = new PointLight();
pl.CastShadow = true;
pl.AttenuationRadius = 10f;  // 同时影响光照范围和阴影范围
```

### 聚光灯阴影

```csharp
var sp = new SpotLight();
sp.CastShadow = true;
```

> **注意**：阴影渲染会带来额外性能开销。建议仅在需要时开启，并合理设置 Shadow Map 分辨率。

## 点云渲染

利用 `InstancedMesh` + 单顶点几何体 + 自定义着色器实现高性能点云：

```csharp
private void BuildPointCloud(Aura3DView view, int pointCount)
{
    // 创建单点几何体
    var geometry = new Geometry();
    geometry.PrimitiveType = PrimitiveType.Points;
    geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3,
        new List<float> { 0, 0, 0 });

    var sourceMesh = new Mesh { Geometry = geometry };

    var material = new Material { BlendMode = BlendMode.Opaque };

    // 自定义着色器
    material.SetShaderSource("LightPass", ShaderType.Vertex, pointCloudVertShader);
    material.SetShaderSource("LightPass", ShaderType.Fragment, pointCloudFragShader);
    sourceMesh.Material = material;

    // 创建实例
    var instancedMesh = InstancedMesh.FromMesh(sourceMesh);
    instancedMesh.SetAttributeEnabled("InstanceNormalTransform", false);

    // 生成球体内随机点
    var rand = new Random(42);
    float radius = 10f;
    var colors = new List<Vector4>();

    for (int i = 0; i < pointCount; i++)
    {
        // 球体均匀分布
        float theta = (float)(rand.NextDouble() * Math.PI * 2);
        float phi = (float)(Math.Acos(2 * rand.NextDouble() - 1));
        float r = (float)(radius * Math.Cbrt(rand.NextDouble()));

        float x = r * (float)(Math.Sin(phi) * Math.Cos(theta));
        float y = r * (float)(Math.Sin(phi) * Math.Sin(theta));
        float z = r * (float)(Math.Cos(phi));

        instancedMesh.AddInstance(Matrix4x4.CreateTranslation(x, y, z));
        colors.Add(new Vector4(
            (x / radius + 1f) / 2f,
            (y / radius + 1f) / 2f,
            (z / radius + 1f) / 2f,
            1.0f));
    }

    instancedMesh.SetInstanceAttribute<Vector4>(
        BuildInVertexAttribute.TexCoord_1, 4, colors);

    view.AddNode(instancedMesh);
}
```

点云着色器要点：
- 顶点着色器中设置 `gl_PointSize`
- 片段着色器中用 `gl_PointCoord` 裁剪为圆形，避免方形点

## 图元渲染

除了标准的三角形渲染，Aura3D 支持所有 OpenGL 图元类型：

```csharp
var geometry = new Geometry();

// 选择图元类型
geometry.PrimitiveType = PrimitiveType.Triangles;      // 三角形（默认）
// geometry.PrimitiveType = PrimitiveType.Points;       // 点
// geometry.PrimitiveType = PrimitiveType.Lines;        // 独立线段
// geometry.PrimitiveType = PrimitiveType.LineStrip;    // 折线
// geometry.PrimitiveType = PrimitiveType.LineLoop;     // 闭合折线
// geometry.PrimitiveType = PrimitiveType.TriangleStrip;// 三角形带
// geometry.PrimitiveType = PrimitiveType.TriangleFan;  // 三角形扇

// 设置顶点数据
geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, new List<float>
{
    -0.5f, -0.5f, 0,
     0.5f, -0.5f, 0,
     0.0f,  0.5f, 0,
});

// 可选：设置索引（Triangles 类型推荐使用索引）
geometry.SetIndices(new List<uint> { 0, 1, 2 });

// 使用自定义着色器
var material = new Material { BlendMode = BlendMode.Opaque };
material.SetShaderSource("LightPass", ShaderType.Vertex, solidColorVertShader);
material.SetShaderSource("LightPass", ShaderType.Fragment, solidColorFragShader);
material.SetShaderPassParametersCallback("LightPass", pass =>
{
    pass.UniformVector4("uColor", new Vector4(1, 0, 0, 1));
});

var mesh = new Mesh { Geometry = geometry, Material = material };
view.AddNode(mesh);
```

## 材质高级用法

### 材质通道

```csharp
var material = new Material
{
    BlendMode = BlendMode.Opaque,
    DoubleSided = false,
};

// 设置基础色通道
material.Channels = new List<Channel>
{
    new Channel
    {
        Name = "BaseColor",
        Texture = texture,
    }
};

// 或快速设置 BaseColor
material.BaseColor = texture;
```

### 混合模式

```csharp
material.BlendMode = BlendMode.Opaque;       // 不透明（默认）
// material.BlendMode = BlendMode.Masked;     // Alpha 裁剪
// material.BlendMode = BlendMode.Translucent; // 半透明
```

## 节点操作技巧

### 模型包围盒

```csharp
// 获取模型的包围盒
var bbox = model.BoundingBox;  // BoundingBox { Min, Max }

// 让摄像机自动适配到包围盒
camera.FitToBoundingBox(bbox, padding: 0.5f);
```

### 克隆与共享

```csharp
// 共享几何体和纹理数据（推荐用于大量实例）
var cloned = model.Clone(CopyType.SharedResourceData);
```

### 直接操作子部件

从加载的模型中按名称查找并独立控制部件：

```csharp
var part = model.Meshes.First(m => m.Name == "wheel_front_left");
part.RotationDegrees = new Vector3(rotationAngle, 0, 0);
```

### 批量变换更新

频繁修改 Position、Rotation、Scale 会触发多次世界矩阵重算。如需同时修改多个属性，使用 `BeginTransformUpdate` 批量操作：

```csharp
using (node.BeginTransformUpdate(UpdateTransformMode.LocalOnly))
{
    node.Position = new Vector3(10, 0, 5);
    node.RotationDegrees = new Vector3(0, 90, 0);
    node.Scale = new Vector3(2f);
}
// using 块结束时一次性重算世界矩阵
```

### 查找子节点

```csharp
// 递归查找节点树下所有指定类型的子节点
var allMeshes = model.GetNodesInChildren<Mesh>();
var allLights = view.Scene.MainCamera.GetNodesInChildren<Light>();

// 过滤特定节点
var specificNodes = model.GetNodesInChildren<Node>()
    .Where(n => n.Tags.Contains("pickable"));
```

## 多摄像机与资源管理

多摄像机渲染和 GPU 资源生命周期管理请参阅 [渲染管线文档](./pipelines.md#多摄像机渲染)。
