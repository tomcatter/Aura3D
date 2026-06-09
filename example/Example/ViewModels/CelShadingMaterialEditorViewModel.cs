using Aura3D.Core.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Example.ViewModels;

public partial class CelShadingMaterialEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<NodeItem> _rootNodes = [];

    [ObservableProperty]
    private ObservableCollection<ChannelItem> _channels = [];

    [ObservableProperty]
    private ObservableCollection<ParameterItem> _parameters = [];

    [ObservableProperty]
    private bool _hasMaterial;
}

public class NodeItem
{
    public string DisplayName { get; }
    public Node Node { get; }
    public ObservableCollection<NodeItem> Children { get; } = [];

    public NodeItem(string displayName, Node node)
    {
        DisplayName = displayName;
        Node = node;
    }
}

public class ChannelItem
{
    public string Name { get; }
    public string TextureInfo { get; }

    public ChannelItem(string name, string textureInfo)
    {
        Name = name;
        TextureInfo = textureInfo;
    }
}

public class ParameterItem
{
    public string Key { get; }
    public string Value { get; }
    public string TypeName { get; }

    public ParameterItem(string key, string value, string typeName)
    {
        Key = key;
        Value = value;
        TypeName = typeName;
    }
}
