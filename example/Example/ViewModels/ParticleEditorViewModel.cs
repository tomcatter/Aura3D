using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Numerics;

namespace Example.ViewModels;

public partial class ParticleEditorViewModel : ViewModelBase
{
    // Emitter params
    [ObservableProperty] private int _maxParticles = 50000;
    [ObservableProperty] private float _emissionRate = 500f;
    [ObservableProperty] private int _shapeIndex;
    [ObservableProperty] private float _shapeSizeX = 2f;
    [ObservableProperty] private float _shapeSizeY = 2f;
    [ObservableProperty] private float _shapeSizeZ = 2f;
    [ObservableProperty] private float _coneAngle = 30f;

    // Lifetime & size
    [ObservableProperty] private float _lifetimeMin = 0.5f;
    [ObservableProperty] private float _lifetimeMax = 2.0f;
    [ObservableProperty] private float _startSizeMin = 0.1f;
    [ObservableProperty] private float _startSizeMax = 0.5f;
    [ObservableProperty] private float _endSizeMin = 0.01f;
    [ObservableProperty] private float _endSizeMax = 0.1f;

    // Velocity
    [ObservableProperty] private float _velocityXMin = -0.5f;
    [ObservableProperty] private float _velocityXMax = 0.5f;
    [ObservableProperty] private float _velocityYMin = 3f;
    [ObservableProperty] private float _velocityYMax = 7f;
    [ObservableProperty] private float _velocityZMin = -0.5f;
    [ObservableProperty] private float _velocityZMax = 0.5f;

    // Color
    [ObservableProperty] private int _startColorR = 255;
    [ObservableProperty] private int _startColorG = 165;
    [ObservableProperty] private int _startColorB;
    [ObservableProperty] private int _startColorA = 255;
    [ObservableProperty] private int _endColorR = 255;
    [ObservableProperty] private int _endColorG = 50;
    [ObservableProperty] private int _endColorB;
    [ObservableProperty] private int _endColorA;

    // Physics
    [ObservableProperty] private float _gravityY = 1f;
    [ObservableProperty] private float _damping = 0.4f;

    // Rotation
    [ObservableProperty] private float _rotationMin;
    [ObservableProperty] private float _rotationMax = 6.28f;
    [ObservableProperty] private float _angularVelocityMin = -1f;
    [ObservableProperty] private float _angularVelocityMax = 1f;

    // Position
    [ObservableProperty] private float _posX;
    [ObservableProperty] private float _posY = 2f;
    [ObservableProperty] private float _posZ = 5f;

    // Stats
    [ObservableProperty] private int _currentFps;
    [ObservableProperty] private int _minFps = int.MaxValue;
    [ObservableProperty] private int _maxFps;
    [ObservableProperty] private int _avgFps;
    [ObservableProperty] private string _detailText = "";
    [ObservableProperty] private double _fpsBarWidth;
    [ObservableProperty] private string _fpsBarColor = "#6BCB77";

    public IRelayCommand ResetCommand { get; }

    public ParticleEditorViewModel()
    {
        ResetCommand = new RelayCommand(OnReset);
    }

    private void OnReset()
    {
        MaxParticles = 50000;
        EmissionRate = 500;
        ShapeIndex = 0;
        ShapeSizeX = 2; ShapeSizeY = 2; ShapeSizeZ = 2;
        ConeAngle = 30;
        LifetimeMin = 0.5f; LifetimeMax = 2.0f;
        StartSizeMin = 0.1f; StartSizeMax = 0.5f;
        EndSizeMin = 0.01f; EndSizeMax = 0.1f;
        VelocityXMin = -0.5f; VelocityXMax = 0.5f;
        VelocityYMin = 3; VelocityYMax = 7;
        VelocityZMin = -0.5f; VelocityZMax = 0.5f;
        StartColorR = 255; StartColorG = 165; StartColorB = 0; StartColorA = 255;
        EndColorR = 255; EndColorG = 50; EndColorB = 0; EndColorA = 0;
        GravityY = 1; Damping = 0.4f;
        RotationMin = 0; RotationMax = 6.28f;
        AngularVelocityMin = -1; AngularVelocityMax = 1;
        PosX = 0; PosY = 2; PosZ = 5;
        MinFps = int.MaxValue; MaxFps = 0; AvgFps = 0;
    }
}
