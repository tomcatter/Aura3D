using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Silk.NET.Input;

namespace RenderDebug;

public class TestView
{
    private Scene scene;

    private IInputContext inputContext;

    Vector2 point = new(-1, -1);

    double deltaTime = 0;


    Func<string, Stream> loadFileFun;

    public TestView(Scene scene, IInputContext inputContext, Func<string, Stream> loadFileFun)
    {
        this.scene = scene;
        this.inputContext = inputContext;
        this.loadFileFun = loadFileFun;
    }

    public void OnInit()
    {
        using var hdriFileStream = loadFileFun("Textures/buikslotermeerplein_1k.hdr");

        var hdriTexture = TextureLoader.LoadHdrTexture(hdriFileStream);

        var cubemap = HDRIToCubeTextureConverter.ConvertFromTexture(hdriTexture, 1024);

        scene.Background = cubemap;

        var m = inputContext.Mice.First();

        m.MouseMove += (m, p) =>
        {
            if (m.IsButtonPressed(MouseButton.Left) == false)
                return;

            var newPosition = m.Position;
          
            var delta = newPosition - point;

            if (scene.MainCamera != null)
            {
                scene.MainCamera!.RotationDegrees = new Vector3(
                    (float)(scene.MainCamera.RotationDegrees.X + (float)delta.Y * (float)deltaTime * 20),
                    (float)(scene.MainCamera.RotationDegrees.Y + (float)delta.X * (float)deltaTime * 20), 0);
            }

            point = newPosition;
        };


        m.MouseDown += (m, p) =>
        {
            point = m.Position;
        };


        var camera = scene.MainCamera;

        camera.NearPlane = 1;

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


        foreach (var filename in name)
        {
            var stream = loadFileFun($"Textures/skybox/{filename}");
            list.Add(stream);
        }

        var cubeTexture = TextureLoader.LoadCubeTexture(list);

        foreach (var stream in list)
        {
            stream.Dispose();
        }


        // scene.Background = cubeTexture;

        var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(loadFileFun("Models/lion_head_1k.glb"));


        camera.FitToBoundingBox(model.BoundingBox, 1);


        model.Position = model.Position;

        scene.AddNode(model);



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
        scene.AddNode(dl);

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
    }

    public void OnUpdate(double deltaTime)
    {
        var kb = inputContext.Keyboards.First();

        this.deltaTime = deltaTime;

        if (kb.IsKeyPressed(Key.W))
        {
            scene.MainCamera!.Position += scene.MainCamera.Forward * 0.1F * (float)deltaTime;
        }

        if (kb.IsKeyPressed(Key.S))
        {
            scene.MainCamera!.Position += scene.MainCamera.Backward * 0.1F * (float)deltaTime;
        }

        if (kb.IsKeyPressed(Key.A))
        {
            scene.MainCamera!.Position += scene.MainCamera.Left * 0.1F * (float)deltaTime;
        }

        if (kb.IsKeyPressed(Key.D))
        {
            scene.MainCamera!.Position += scene.MainCamera.Right * 0.1F * (float)deltaTime;
        }

    }

}
