using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Example.ViewModels;

public partial class DebugTestViewModel : ViewModelBase
{
    // ═══════════════════════════════════════════════
    //  Debug Draw
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private bool _showAxes = true;

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _debugEnable;

    [ObservableProperty]
    private bool _showBoundingBox;

    [ObservableProperty]
    private bool _showDirectionalLight;

    [ObservableProperty]
    private bool _showPointLight;

    [ObservableProperty]
    private bool _showSpotLight;

    [ObservableProperty]
    private bool _showCamera;

    [ObservableProperty]
    private bool _showBone;

    // ═══════════════════════════════════════════════
    //  Picking
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private bool _enablePicking = true;

    [ObservableProperty]
    private string _pickInfo = "点击 3D 视图中的模型进行拾取";

    // ═══════════════════════════════════════════════
    //  Lights
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private bool _dirLightEnabled = true;

    [ObservableProperty]
    private float _dirLightRotX = -40f;

    [ObservableProperty]
    private float _dirLightRotY = -30f;

    [ObservableProperty]
    private bool _pointLightEnabled = true;

    [ObservableProperty]
    private float _pointLightX = -8f;

    [ObservableProperty]
    private float _pointLightY = 6f;

    [ObservableProperty]
    private float _pointLightZ = 3f;

    [ObservableProperty]
    private float _pointLightRadius = 10f;

    [ObservableProperty]
    private bool _spotLightEnabled = true;

    [ObservableProperty]
    private float _spotLightX = 8f;

    [ObservableProperty]
    private float _spotLightY = 6f;

    [ObservableProperty]
    private float _spotLightZ = 3f;

    [ObservableProperty]
    private float _spotLightRotX = -75f;

    [ObservableProperty]
    private float _spotLightRotY = -20f;

    [ObservableProperty]
    private float _spotLightRotZ;

    [ObservableProperty]
    private float _spotLightInnerAngle = 10f;

    [ObservableProperty]
    private float _spotLightOuterAngle = 25f;

    [ObservableProperty]
    private float _spotLightRadius = 12f;

    // ═══════════════════════════════════════════════
    //  Culling & Performance
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private bool _enableFrustumCulling = true;

    [ObservableProperty]
    private float _boundingBoxPadding;

    [ObservableProperty]
    private int _staticMeshCount = 12;

    [ObservableProperty]
    private int _skinnedMeshCount = 25;

    [ObservableProperty]
    private int _visibleCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _detailText = "点击 Build 构建场景";

    // ═══════════════════════════════════════════════
    //  FPS
    // ═══════════════════════════════════════════════

    [ObservableProperty]
    private int _currentFps;

    [ObservableProperty]
    private int _minFps = int.MaxValue;

    [ObservableProperty]
    private int _maxFps;

    [ObservableProperty]
    private int _avgFps;

    [ObservableProperty]
    private double _fpsBarWidth;

    [ObservableProperty]
    private string _fpsBarColor = "#6BCB77";

    // ═══════════════════════════════════════════════
    //  Build Command
    // ═══════════════════════════════════════════════

    /// <summary>代码隐藏通过此事件订阅构建逻辑。</summary>
    public event Action BuildRequested;

    [RelayCommand]
    private void Build() => BuildRequested?.Invoke();
}
