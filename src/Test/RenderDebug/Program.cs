// See https://aka.ms/new-console-template for more information
using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Renderers.PBRDeferred;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Aura3D.Model;
using Silk.NET.Windowing;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Numerics;

var window = Window.Create(WindowOptions.Default);
ControlRenderTarget controlRenderTarget = new ControlRenderTarget();
Camera.ControlRenderTarget = controlRenderTarget;
Scene scene = new Scene(scene => new PBRDeferredPipeline(scene));
window.Load += () =>
{
    controlRenderTarget.Width = (uint)(window.Size.X);
    controlRenderTarget.Height = (uint)(window.Size.Y);
    controlRenderTarget.FrameBufferId = 0;

    scene.RenderPipeline.Initialize(str =>
    {
        window.GLContext.TryGetProcAddress(str, out var p);
        return p;
    });

    var camera = scene.MainCamera;

    camera.ClearColor = Color.Gray;
    camera.NearPlane = 1;
    camera.IsRenderBackground = false;

    var list = new List<Stream>();
    List<string> name =
    [
        "px.png",
        "nx.png",
        "py.png",
        "ny.png",
        "pz.png",
        "nz.png",
    ];
    foreach(var filename in name)
    {
        var stream = new StreamReader($"../../../../../../example/Example/Assets/Textures/skybox/{filename}").BaseStream;
        list.Add(stream);
    }

    var cubeTexture = TextureLoader.LoadCubeTexture(list);

    foreach (var stream in list)
    {
        stream.Dispose();
    }


    scene.Background = cubeTexture;

    var (model, animations) = ModelLoader.LoadGlbModelAndAnimations($"../../../../../../example/Example/Assets/Models/wooden_stool_02_1k.glb");

    AddNode(model);

    camera.FitToBoundingBox(model.BoundingBox, 1);


    var mesh = new Mesh();

    mesh.Geometry = new PlaneGeometry();

    mesh.Material = new Material();

    mesh.Material.BaseColor = Texture.CreateFromColor(Color.Blue);

    mesh.Material.Normal = Texture.CreateFromColor(Color.FromArgb(128, 128, 255));

    mesh.RotationDegrees = new Vector3(90, 0, 0);


    // AddNode(mesh);

    DirectionalLight dl = new DirectionalLight();

    dl.CastShadow = true;

    dl.RotationDegrees = new Vector3(-45, 45, 0);

    dl.ShadowConfig.Width = 2;
    dl.ShadowConfig.Height = 2;
    dl.ShadowConfig.NearPlane = 0.001f;
    dl.ShadowConfig.FarPlane = 1;
    AddNode(dl);

    //SpotLight sp = new SpotLight();

    //sp.Position = model.Position + model.Up * 2 ;

    //sp.RotationDegrees = new Vector3(-90, 0, 0);

    //sp.LightColor = Color.White;

    //sp.CastShadow = true;

    //sp.InnerConeAngleDegree = 50;

    //sp.OuterAngleDegree = 55;

    //sp.AttenuationRadius = 40;

    //AddNode(sp);




    //var pl = new PointLight();

    //pl.Position = camera.Position;

    //pl.AttenuationRadius = 10;

    //pl.CastShadow = true;

    //pl.Position = model.Position + model.Up * 2;

    //AddNode(pl);
};


window.Render += (delta) =>
{
    if (window.WindowState == WindowState.Minimized)
        return;

    controlRenderTarget.Width = (uint)(window.Size.X);
    controlRenderTarget.Height = (uint)(window.Size.Y);
    scene.RenderPipeline.DefaultFramebuffer = (uint)0;

    scene.RenderPipeline.Render();

    scene.Update(delta);




};

window.Run();


void AddNode<T>(T node) where T : Node
{
    scene.AddNode(node);
}
