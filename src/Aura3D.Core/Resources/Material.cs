using System.Drawing;

namespace Aura3D.Core.Resources;

public class Material : IClone<Material>
{
    public List<Channel> Channels { get; set; } = [];

    private Dictionary<string, object> parameters  { get; set; } = new Dictionary<string, object>();

    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    public bool DoubleSided { get; set; } = false;

    public float AlphaCutoff { get; set; } = 0.5f;

    // The type of the value to be retrieved must be consistent with the type of the generic; otherwise, the value cannot be retrieved.
    public bool TryGetParameterValue<T>(string key, out T value)
    {
        if (parameters.TryGetValue(key, out var obj) && obj is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    public void SetParameterValue<T>(string key, T value)
    {
        if(value != null)
        {
            parameters[key] = value;
        }
    }


    public Material Clone()
    {
        return new Material
        {
            BlendMode = this.BlendMode,
            DoubleSided = this.DoubleSided,
            AlphaCutoff = this.AlphaCutoff,
            Channels = Channels
        };
    }

    public Material DeepClone()
    {
        var material = Clone();

        material.Channels = new List<Channel>(Channels.Count);

        foreach(var channel in Channels)
        {
            var newChannel = new Channel
            {
                Name = channel.Name,
                Color = channel.Color,
                Texture = channel.Texture is Texture texture? texture.Clone() : null
            };
            material.Channels.Add(newChannel);
        }

        return material;
    }
}


public class Channel
{
    public string Name { get; set; } = string.Empty;

    public ITexture? Texture { get; set; }

    public Color Color { get; set; } = Color.White;
}

public enum BlendMode
{
    Opaque,
    Masked,
    Translucent,
}