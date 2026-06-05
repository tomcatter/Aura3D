using CommunityToolkit.Mvvm.ComponentModel;

namespace Example.ViewModels;

public partial class SkinnedMeshCullingViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _soldierCount = 25;

    [ObservableProperty]
    private bool _enableFrustumCulling = true;

    [ObservableProperty]
    private bool _showBoundingBox;

    [ObservableProperty]
    private int _currentFps;

    [ObservableProperty]
    private int _minFps = int.MaxValue;

    [ObservableProperty]
    private int _maxFps;

    [ObservableProperty]
    private int _avgFps;

    [ObservableProperty]
    private int _visibleSoldiers;

    [ObservableProperty]
    private int _totalSoldiers;

    [ObservableProperty]
    private string _detailText = "Ready";

    [ObservableProperty]
    private double _fpsBarWidth;

    [ObservableProperty]
    private string _fpsBarColor = "#6BCB77";
}
