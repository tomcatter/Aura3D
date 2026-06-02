using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Example.Pages;
using Example.ViewModels;
namespace Example;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        return data switch
        {
            BaseGeometriesViewModel baseGeometriesViewModel => new BaseGeometriesPage(),
            ModelPreviewViewModel gltfModelViewModel => new ModelPreviewPage(),
            FrustumCullingViewModel frustumCullingViewModel => new FrustumCullingPage(),
            AnimationViewModel animationViewModel => new AnimationPage(),
            RoboticArmViewModel roboticArmViewModel => new RoboticArmPage(),
            BlendSpaceViewModel blendSpaceViewModel => new BlendSpacePage(),
            AnimationGraphViewModel animationGraphViewModel => new AnimationGraphPage(),
            PbrViewModel pbrViewModel => new PbrPipelinePage(),
            CelShadingViewModel celShadingViewModel => new CelShadingPage(),
            InstancedRenderingViewModel instancedRenderingViewModel => new InstancedRenderingPage(),
            PointCloudViewModel pointCloudViewModel => new PointCloudPage(),
            _ => new TextBlock() { Text = "NotFound" }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
