using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Example.ViewModels;

public partial class ParticleEditorViewModel : ViewModelBase
{
    // Emitter collection
    [ObservableProperty] private ObservableCollection<EmitterViewModel> _emitters = [];
    [ObservableProperty] private EmitterViewModel? _selectedEmitter;

    // System-level position
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

    public IRelayCommand AddEmitterCommand { get; }
    public IRelayCommand DuplicateEmitterCommand { get; }

    public ParticleEditorViewModel()
    {
        AddEmitterCommand = new RelayCommand(OnAddEmitter);
        DuplicateEmitterCommand = new RelayCommand(OnDuplicateEmitter, () => SelectedEmitter != null);

        var defaultEmitter = EmitterViewModel.CreateDefault(0);
        WireEmitterDelete(defaultEmitter);
        Emitters = [defaultEmitter];
        SelectedEmitter = defaultEmitter;
    }

    private void OnAddEmitter()
    {
        var emitter = EmitterViewModel.CreateDefault(Emitters.Count);
        WireEmitterDelete(emitter);
        Emitters.Add(emitter);
        SelectedEmitter = emitter;
        RefreshCommands();
    }

    private void OnRemoveEmitter(EmitterViewModel emitter)
    {
        var index = Emitters.IndexOf(emitter);
        Emitters.RemoveAt(index);
        if (Emitters.Count > 0)
            SelectedEmitter = Emitters[System.Math.Max(0, index - 1)];
        else
            SelectedEmitter = null;
        RefreshCommands();
    }

    private void OnDuplicateEmitter()
    {
        if (SelectedEmitter == null) return;
        var clone = EmitterViewModel.CreateDefault(Emitters.Count);
        clone.MaxParticles = SelectedEmitter.MaxParticles;
        clone.EmissionRate = SelectedEmitter.EmissionRate;
        clone.ShapeIndex = SelectedEmitter.ShapeIndex;
        clone.ShapeSizeX = SelectedEmitter.ShapeSizeX;
        clone.ShapeSizeY = SelectedEmitter.ShapeSizeY;
        clone.ShapeSizeZ = SelectedEmitter.ShapeSizeZ;
        clone.ConeAngle = SelectedEmitter.ConeAngle;
        clone.LifetimeMin = SelectedEmitter.LifetimeMin;
        clone.LifetimeMax = SelectedEmitter.LifetimeMax;
        clone.StartSizeMin = SelectedEmitter.StartSizeMin;
        clone.StartSizeMax = SelectedEmitter.StartSizeMax;
        clone.EndSizeMin = SelectedEmitter.EndSizeMin;
        clone.EndSizeMax = SelectedEmitter.EndSizeMax;
        clone.VelocityXMin = SelectedEmitter.VelocityXMin;
        clone.VelocityXMax = SelectedEmitter.VelocityXMax;
        clone.VelocityYMin = SelectedEmitter.VelocityYMin;
        clone.VelocityYMax = SelectedEmitter.VelocityYMax;
        clone.VelocityZMin = SelectedEmitter.VelocityZMin;
        clone.VelocityZMax = SelectedEmitter.VelocityZMax;
        clone.StartColorR = SelectedEmitter.StartColorR;
        clone.StartColorG = SelectedEmitter.StartColorG;
        clone.StartColorB = SelectedEmitter.StartColorB;
        clone.StartColorA = SelectedEmitter.StartColorA;
        clone.EndColorR = SelectedEmitter.EndColorR;
        clone.EndColorG = SelectedEmitter.EndColorG;
        clone.EndColorB = SelectedEmitter.EndColorB;
        clone.EndColorA = SelectedEmitter.EndColorA;
        clone.GravityY = SelectedEmitter.GravityY;
        clone.Damping = SelectedEmitter.Damping;
        clone.RotationMin = SelectedEmitter.RotationMin;
        clone.RotationMax = SelectedEmitter.RotationMax;
        clone.AngularVelocityMin = SelectedEmitter.AngularVelocityMin;
        clone.AngularVelocityMax = SelectedEmitter.AngularVelocityMax;
        WireEmitterDelete(clone);
        Emitters.Add(clone);
        SelectedEmitter = clone;
        RefreshCommands();
    }

    private void WireEmitterDelete(EmitterViewModel emitter)
    {
        emitter.SetDeleteAction(() => OnRemoveEmitter(emitter));
    }

    private void RefreshCommands()
    {
        DuplicateEmitterCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedEmitterChanged(EmitterViewModel? value)
    {
        RefreshCommands();
    }
}
