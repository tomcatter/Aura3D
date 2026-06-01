// See https://aka.ms/new-console-template for more information
using Aura3D.Core;
using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Renderers;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Aura3D.Model;
using RenderDebug;
using Silk.NET.Input;
using Silk.NET.Windowing;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Numerics;

var window = Window.Create(WindowOptions.Default);
ControlRenderTarget controlRenderTarget = new ControlRenderTarget();
Camera.ControlRenderTarget = controlRenderTarget;
Scene scene = new Scene(scene => new BlinnPhongPipeline(scene));


TestView? testView = null;

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

    var inputContext = window.CreateInput();

    testView = new TestView(scene, inputContext, name => File.OpenRead($"../../../../../../example/Example/Assets/{name}"));

    testView.OnInit();

  
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


    testView.OnUpdate(delta);


};

window.Run();
