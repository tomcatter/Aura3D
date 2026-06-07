using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Example.ViewModels;

public partial class AnimationFeaturesViewModel : ViewModelBase
{
    public AnimationFeaturesViewModel()
    {
        ResetCommand = new RelayCommand(OnReset);
    }

    [ObservableProperty]
    private ObservableCollection<string> _animations = [];

    [ObservableProperty]
    private string _selectedAnimation = string.Empty;

    /// <summary>
    /// 0 = Loop, 1 = Once, 2 = PingPong
    /// </summary>
    [ObservableProperty]
    private int _loopModeIndex = 0;

    [ObservableProperty]
    private bool _isLoopMode = true;

    [ObservableProperty]
    private bool _isOnceMode;

    [ObservableProperty]
    private bool _isPingPongMode;

    partial void OnLoopModeIndexChanged(int value)
    {
        IsLoopMode = value == 0;
        IsOnceMode = value == 1;
        IsPingPongMode = value == 2;
    }

    [ObservableProperty]
    private double _speed = 1.0;

    [ObservableProperty]
    private string _speedString = "1.0";

    partial void OnSpeedChanged(double value)
    {
        SpeedString = $"{value:F1}";
    }

    [ObservableProperty]
    private bool _isExternalUpdate;

    [ObservableProperty]
    private string _playbackStatus = "Playing (Auto)";

    /// <summary>
    /// Blend Space X axis [-1, 1]
    /// </summary>
    [ObservableProperty]
    private double _blendX;

    /// <summary>
    /// Blend Space Y axis [-1, 1]
    /// </summary>
    [ObservableProperty]
    private double _blendY;

    /// <summary>
    /// Animation Graph speed (0 → idle, 0-300 → walk, >300 → run)
    /// </summary>
    [ObservableProperty]
    private float _graphSpeed;

    /// <summary>
    /// Current demo mode: "Basic", "Graph", "BlendSpace"
    /// </summary>
    [ObservableProperty]
    private int _demoModeIndex;

    [ObservableProperty]
    private bool _isBasicMode = true;

    [ObservableProperty]
    private bool _isGraphMode;

    [ObservableProperty]
    private bool _isBlendSpaceMode;

    partial void OnDemoModeIndexChanged(int value)
    {
        IsBasicMode = value == 0;
        IsGraphMode = value == 1;
        IsBlendSpaceMode = value == 2;
    }

    public ICommand ResetCommand { get; }

    private void OnReset()
    {
        ResetRequested?.Invoke();
    }

    public event Action? ResetRequested;

    // ─── BoneAttachment LocalOffset ─────────────────────────

    [ObservableProperty]
    private double _offsetX = -9.4;

    [ObservableProperty]
    private double _offsetY = 6.7;

    [ObservableProperty]
    private double _offsetZ = 3.5;

    [ObservableProperty]
    private double _offsetYaw = -17;

    [ObservableProperty]
    private double _offsetPitch = 90;

    [ObservableProperty]
    private double _offsetRoll;
}
