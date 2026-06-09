using Aura3D.Core.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Example.ViewModels;

public partial class CelShadingMaterialEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<NodeItem> _rootNodes = [];
}

public class NodeItem
{
    public string DisplayName { get; }
    public ObservableCollection<NodeItem> Children { get; } = [];

    public NodeItem(string displayName)
    {
        DisplayName = displayName;
    }
}
