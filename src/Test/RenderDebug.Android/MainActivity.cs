using Aura3D.Core;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Renderers.PBRDeferred;
using Aura3D.Core.Scenes;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;
using System.Drawing;
using System.Numerics;

namespace Example.Test.Android;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : SilkActivity
{
    Scene scene = null;
    protected override void OnRun()
    {
        ControlRenderTarget controlRenderTarget = new ControlRenderTarget();
        Camera.ControlRenderTarget = controlRenderTarget;
        var view = Silk.NET.Windowing.Window.GetView(ViewOptions.Default with { API = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(3, 0))});

        scene = new Scene(scene => new PBRDeferredPipeline(scene));

        view.Load += () =>
        {

            scene.RenderPipeline.Initialize(str =>
            {
                view.GLContext.TryGetProcAddress(str, out var p);
                return p;
            });



            var camera = scene.MainCamera;

            camera.ClearColor = Color.Gray;
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
                list.Add(Assets.Open($"Example/Assets/Textures/skybox/{filename}"));
            }

            var cubeTexture = TextureLoader.LoadCubeTexture(list);

            foreach (var stream in list)
            {
                stream.Dispose();
            }

            camera.ClearType = ClearType.Skybox;

            camera.SkyboxTexture = cubeTexture;

            using (var stream = Assets.Open($"Example/Assets/Models/lion_head_1k.glb"))
            {
                var (model, animations) = ModelLoader.LoadGlbModelAndAnimations(stream);


                model.Position = camera.Position + camera.Forward;

                AddNode(model); 

                // camera.FitToBoundingBox(model.BoundingBox);

                DirectionalLight dl = new DirectionalLight();

                dl.CastShadow = true;

                //dl.RotationDegrees = new Vector3(-45, 45, 0);

                AddNode(dl);

            }


           


        };

        view.Render += (delta) =>
        {

            controlRenderTarget.Width = (uint)(view.Size.X);
            controlRenderTarget.Height = (uint)(view.Size.Y);
            scene.RenderPipeline.DefaultFramebuffer = (uint)0;

            scene.RenderPipeline.Render();

            scene.Update(delta);



        };

        view.Run();
    }



    void AddNode<T>(T node) where T : Node
    {
        scene.AddNode(node);
    }
}