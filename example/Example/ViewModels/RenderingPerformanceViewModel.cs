using CommunityToolkit.Mvvm.ComponentModel;

namespace Example.ViewModels;

public partial class RenderingPerformanceViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _techniqueIndex;
    [ObservableProperty]
    private bool _isIndividualMode = true;
    [ObservableProperty]
    private bool _isInstancedMode;
    [ObservableProperty]
    private bool _isHISMMode;
    partial void OnTechniqueIndexChanged(int value)
    {
        IsIndividualMode = value == 0;
        IsInstancedMode = value == 1;
        IsHISMMode = value == 2;
    }

    [ObservableProperty] private string _cubeCountText = "1000";
    [ObservableProperty] private string _spacingText = "2.5";
    [ObservableProperty] private bool _enableFrustumCulling = true;

    // ─── FPS Stats ──────────────────────────────────────────

    [ObservableProperty] private int _currentFps;
    [ObservableProperty] private int _minFps = 999;
    [ObservableProperty] private int _maxFps;
    [ObservableProperty] private int _avgFps;
    [ObservableProperty] private string _detailText = "";

    /// <summary>FPS bar width (0–200), scaled to max observed</summary>
    [ObservableProperty] private double _fpsBarWidth;
    [ObservableProperty] private string _fpsBarColor = "#4CAF50";

    // ─── HISM ───────────────────────────────────────────────

    [ObservableProperty] private string _hismMaxPerGroupText = "1024";
    [ObservableProperty] private string _hismMaxDepthText = "6";

    [ObservableProperty] private string _instanceIdxText = "0";
    [ObservableProperty] private string _posXText = "0";
    [ObservableProperty] private string _posYText = "0";
    [ObservableProperty] private string _posZText = "0";
}
