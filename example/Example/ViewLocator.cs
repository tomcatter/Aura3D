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
            RoboticArmViewModel roboticArmViewModel => new RoboticArmPage(),
            PbrViewModel pbrViewModel => new PbrPipelinePage(),
            CelShadingViewModel celShadingViewModel => new CelShadingPage(),
            InstancedRenderingViewModel instancedRenderingViewModel => new InstancedRenderingPage(),
            PointCloudViewModel pointCloudViewModel => new PointCloudPage(),
            HISMViewModel hismViewModel => new HISMPage(),
            PrimitiveTypeViewModel primitiveTypeViewModel => new PrimitiveTypePage(),
            AnimationFeaturesViewModel animationFeaturesViewModel => new AnimationFeaturesPage(),
            _ => new TextBlock() { Text = "NotFound" }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
