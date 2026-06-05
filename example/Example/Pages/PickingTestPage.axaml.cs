using Aura3D.Avalonia;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Model;
using Avalonia.Controls;
using Avalonia.Platform;
using Example.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;

namespace Example.Pages;

public partial class PickingTestPage : UserControl
{
    private PickingTestViewModel? _vm;
    private CameraController? _cameraController;
    private readonly List<Node> _loadedModels = [];

    public PickingTestPage()
    {
        InitializeComponent();
    }

    private async void Aura3DView_SceneInitialized(object? sender, InitializedRoutedEventArgs e)
    {
        var view = sender as Aura3DView;
        if (view == null) return;

        _vm = DataContext as PickingTestViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(PickingTestViewModel.EnablePicking))
                    view.EnablePicking = _vm.EnablePicking;
                if (args.PropertyName == nameof(PickingTestViewModel.ShowBoundingBox))
                    view.Scene!.RenderPipeline.Settings.ShowBoundingBox = _vm.ShowBoundingBox;
            };
            view.EnablePicking = _vm.EnablePicking;
        }

        view.ObjectPicked += OnObjectPicked;
        _cameraController = new CameraController(view);

        var scene = view.Scene!;
        scene.ShowAxisGizmo = true;
        scene.ShowGrid = true;

        // --- 光照 ---
        var dl = new DirectionalLight
        {
            RotationDegrees = new Vector3(-40, 30, 0),
            LightColor = Color.White
        };
        view.AddNode(dl);

        var pl = new PointLight
        {
            Position = new Vector3(3, 4, 3),
            LightColor = Color.FromArgb(255, 255, 240, 220),
            LuminousIntensity = 300000
        };
        view.AddNode(pl);

        // 调整相机
        view.MainCamera.Position = new Vector3(5, 4, 7);
        view.MainCamera.LookAt(Vector3.Zero);

        // 异步加载模型
        await LoadModelsAsync(view);

        view.AutoRequestNextFrameRendering = false;
        view.RequestNextFrameRendering();
    }

    private async Task LoadModelsAsync(Aura3DView view)
    {
        var loadTasks = new List<Task<Model?>>();

        // lion_head: 雕像，放在左侧
        loadTasks.Add(LoadModelFromAsset("avares://Example/Assets/Models/lion_head_1k.glb",
            m => { m.Position = new Vector3(-1.8f, 0.1f, 0); m.RotationDegrees = new Vector3(0, 20, 0); m.Scale = new Vector3(0.6f); }));

        // coffee_table: 圆桌，放在右前
        loadTasks.Add(LoadModelFromAsset("avares://Example/Assets/Models/coffee_table_round_01_1k.glb",
            m => { m.Position = new Vector3(1.8f, 0, 1.2f); m.Scale = new Vector3(0.8f); }));

        // wooden_stool: 凳子，放在左前
        loadTasks.Add(LoadModelFromAsset("avares://Example/Assets/Models/wooden_stool_02_1k.glb",
            m => { m.Position = new Vector3(0, 0, 1.8f); m.Scale = new Vector3(1.2f); }));

        // lightbulb: 灯泡，放在右后方高处
        loadTasks.Add(LoadModelFromAsset("avares://Example/Assets/Models/lightbulb_01_1k.glb",
            m => { m.Position = new Vector3(1.5f, 1.0f, -1.5f); m.Scale = new Vector3(0.6f); }));

        // Soldier: 角色骨骼模型，放在右侧
        loadTasks.Add(LoadModelFromAsset("avares://Example/Assets/Models/Soldier.glb",
            m => { m.Position = new Vector3(-1.5f, 0, -1.5f); m.RotationDegrees = new Vector3(0, 180, 0); m.Scale = new Vector3(0.8f); }));

        var models = await Task.WhenAll(loadTasks);

        foreach (var model in models)
        {
            if (model != null)
            {
                view.AddNode(model);
                _loadedModels.Add(model);
            }
        }

        // --- InstancedMesh: 黄色小方块排成一行 ---
        var smallBox = new BoxGeometry(0.25f, 0.25f, 0.25f);
        var tex = Texture.CreateFromColor(Color.Gold);
        var instanced = InstancedMesh.FromMesh(new Mesh
        {
            Geometry = smallBox,
            Material = new Material
            {
                Channels = [new Channel { Name = "BaseColor", Texture = tex }],
                BlendMode = BlendMode.Opaque,
                DoubleSided = true
            }
        });
        instanced.Name = "Gold Instanced Boxes";

        for (int i = 0; i < 6; i++)
        {
            var t = Matrix4x4.CreateTranslation(new Vector3(-2.5f + i * 1.0f, -0.1f, -2.2f));
            instanced.AddInstance(t);
        }
        view.AddNode(instanced);
        _loadedModels.Add(instanced);

        if (_vm != null)
            _vm.ModelCount = $"模型: {_loadedModels.Count} 个";
    }

    private static async Task<Model?> LoadModelFromAsset(string uri, Action<Model> configure)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var stream = AssetLoader.Open(new Uri(uri));
                var model = ModelLoader.LoadGlbModel(stream);
                if (model != null)
                    configure(model);
                return model;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load {uri}: {ex.Message}");
            return null;
        }
    }

    private void OnObjectPicked(object? sender, ObjectPickedEventArgs e)
    {
        if (_vm == null) return;

        var result = e.PickResult;
        string typeName = result.Node switch
        {
            InstancedMesh => "InstancedMesh",
            Mesh => "Mesh",
            Model => "Model",
            _ => result.Node.GetType().Name
        };

        string instanceInfo = result.InstanceIndex.HasValue
            ? $" [实例#{result.InstanceIndex.Value}]"
            : "";

        bool isSkinned = (result.Node is Model m && m.IsSkinnedModel) ||
                         (result.Node is Mesh mesh && mesh.IsSkinnedMesh);

        string skinnedTag = isSkinned ? " [骨骼动画]" : "";

        _vm.PickInfo = $"✓ [{typeName}]{skinnedTag} {result.Node.Name}{instanceInfo} | "
            + $"距离: {result.Distance:F2} | ({result.WorldPosition.X:F2}, {result.WorldPosition.Y:F2}, {result.WorldPosition.Z:F2})";
    }

    private void Aura3DView_SceneUpdated(object? sender, UpdateRoutedEventArgs e)
    {
        (sender as Aura3DView)?.RequestNextFrameRendering();
    }
}
