using Aura3D.Avalonia;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace Example.Pages;

public partial class PointCloudPage : UserControl
{
    private CameraController _cameraController;

    InstancedMesh? instancedMesh;
    int currentPointCount = 0;

    List<double> deltaTimes = new();

    // 逐点顶点着色器：单顶点 + 实例化矩阵 + 逐实例颜色 + gl_PointSize
    private const string PointCloudVertexShader = """
        #version 300 es
        precision mediump float;

        //{{defines}}

        layout(location = 0) in vec3 position;

        #ifdef INSTANCED_MESH
        layout(location = 7) in mat4 modelMatrix;
        #endif

        layout(location = 15) in vec4 instanceColor;

        #ifndef INSTANCED_MESH
        uniform mat4 modelMatrix;
        #endif

        uniform mat4 viewMatrix;
        uniform mat4 projectionMatrix;

        out vec4 vInstanceColor;

        void main()
        {
            vec4 worldPosition = modelMatrix * vec4(position, 1.0);
            gl_Position = projectionMatrix * viewMatrix * worldPosition;
            gl_PointSize = 5.0;
            vInstanceColor = instanceColor;
        }
        """;

    // 逐点片段着色器：圆形裁剪 + 逐实例颜色
    private const string PointCloudFragmentShader = """
        #version 300 es
        precision mediump float;
        out vec4 outColor;

        in vec4 vInstanceColor;

        void main()
        {
            float dist = length(gl_PointCoord - 0.5);
            if (dist > 0.5) discard;

            outColor = vInstanceColor;
        }
        """;

    public PointCloudPage()
    {
        InitializeComponent();
        _cameraController = new CameraController(aura3Dview);
    }

    private void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        var view = sender as Aura3DView;
        if (view == null)
            return;

        view.AutoRequestNextFrameRendering = false;

        view.MainCamera.Position = new Vector3(0, 0, 25);
        view.MainCamera.RotationDegrees = new Vector3(0, 0, 0);

        var dl = new DirectionalLight();
        dl.RotationDegrees = new Vector3(-30, -15, 0);
        dl.LightColor = Color.White;
        view.AddNode(dl);

        BuildPointCloud(view, 20000);

        view.RequestNextFrameRendering();
    }

    private void BuildPointCloud(Aura3DView view, int pointCount)
    {
        if (instancedMesh != null)
        {
            view.Remove(instancedMesh);
            instancedMesh = null;
        }

        // 单顶点几何体：原点处一个点
        var geometry = new Geometry();
        geometry.PrimitiveType = Aura3D.Core.Resources.PrimitiveType.Points;
        geometry.SetVertexAttribute(BuildInVertexAttribute.Position, 3, new List<float> { 0, 0, 0 });

        var sourceMesh = new Mesh();
        sourceMesh.Geometry = geometry;

        var material = new Material
        {
            BlendMode = BlendMode.Opaque,
        };

        material.SetShaderSource("LightPass", ShaderType.Vertex, PointCloudVertexShader);
        material.SetShaderSource("LightPass", ShaderType.Fragment, PointCloudFragmentShader);

        sourceMesh.Material = material;

        instancedMesh = InstancedMesh.FromMesh(sourceMesh);

        // 不需要法线变换矩阵，禁用之
        instancedMesh.SetAttributeEnabled("InstanceNormalTransform", false);

        var rand = new Random(42);
        float radius = 10f;

        var colors = new List<Vector4>();

        for (int i = 0; i < pointCount; i++)
        {
            // 球体内随机分布（均匀体积分布用立方根）
            float theta = (float)(rand.NextDouble() * Math.PI * 2);
            float phi = (float)(Math.Acos(2 * rand.NextDouble() - 1));
            float r = (float)(radius * Math.Cbrt(rand.NextDouble()));

            float x = r * (float)(Math.Sin(phi) * Math.Cos(theta));
            float y = r * (float)(Math.Sin(phi) * Math.Sin(theta));
            float z = r * (float)(Math.Cos(phi));

            var transform = Matrix4x4.CreateTranslation(x, y, z);
            instancedMesh.AddInstance(transform);

            // 基于位置的颜色映射（RGB ← XYZ 归一化到 [0,1]）
            colors.Add(new Vector4(
                (x / radius + 1f) / 2f,
                (y / radius + 1f) / 2f,
                (z / radius + 1f) / 2f,
                1.0f));
        }

        instancedMesh.SetInstanceAttribute<Vector4>(
            BuildInVertexAttribute.TexCoord_1, 4, colors);

        view.AddNode(instancedMesh);
        currentPointCount = pointCount;

        infoText.Text = $"Points: {pointCount} | FPS: --";
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs args)
    {
        if (deltaTimes.Count >= 10)
            deltaTimes.RemoveAt(0);
        deltaTimes.Add(args.DeltaTime);
        var avgDt = deltaTimes.Average();
        infoText.Text = $"Points: {currentPointCount} | FPS: {(int)(1 / avgDt)}";

        (sender as Aura3DView)?.RequestNextFrameRendering();
    }

    private void RegenerateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (int.TryParse(pointCountTextBox.Text?.Trim(), out int count) && count > 0)
        {
            BuildPointCloud(aura3Dview, count);
        }
    }
}
