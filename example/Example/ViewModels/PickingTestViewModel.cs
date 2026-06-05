using CommunityToolkit.Mvvm.ComponentModel;

namespace Example.ViewModels;

public partial class PickingTestViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _pickInfo = "点击 3D 视图中的模型进行拾取";

    [ObservableProperty]
    private bool _enablePicking = true;

    [ObservableProperty]
    private string _modelCount = "模型: 加载中...";

    [ObservableProperty]
    private bool _showBoundingBox;
}
