using Aura3D.Core.Renderers;
using System.Drawing;

namespace Aura3D.Core.Resources;

public class Material : IClone<Material>, IGpuResource
{
    public bool NeedsUpload { get; set; } = false;
    public List<Channel> Channels { get; set; } = [];
    
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    public bool DoubleSided { get; set; } = false;

    public float AlphaCutoff { get; set; } = 0.5f;

    public bool HasShader { get; set; } = false;
    public IReadOnlyDictionary<string, string> VertexShaders => _vertexShaders;

    private Dictionary<string, string> _vertexShaders = new Dictionary<string, string>();

    private Dictionary<string, string> _fragmentShaders = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> FragmentShaders => _fragmentShaders;

    public Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

    public Material Clone()
    {
        return new Material
        {
            BlendMode = this.BlendMode,
            DoubleSided = this.DoubleSided,
            AlphaCutoff = this.AlphaCutoff,
            HasShader = this.HasShader,
            Channels = Channels,
            _vertexShaders = _vertexShaders,
            _fragmentShaders = _fragmentShaders,
            ShaderPassParametersCallbacks = ShaderPassParametersCallbacks

        };
    }

    public Material DeepClone()
    {
        var material = Clone();

        material.Channels = new List<Channel>();

        foreach(var channel in Channels)
        {
            var newChannel = new Channel
            {
                Name = channel.Name,
                Texture = channel.Texture is Texture texture? texture.Clone() : null
            };
            material.Channels.Add(newChannel);
        }
        foreach(var vs in _vertexShaders)
        {
            material._vertexShaders.Add(vs.Key, vs.Value);
        }
        foreach (var fs in _fragmentShaders)
        {
            material._fragmentShaders.Add(fs.Key, fs.Value);
        }

        foreach (var callback in ShaderPassParametersCallbacks)
        {
            material.ShaderPassParametersCallbacks.Add(callback.Key, callback.Value);
        }

        return material;
    }

    public Dictionary<string, Action<RenderPass>> ShaderPassParametersCallbacks = [];
    public void SetShaderPassParametersCallback(string key, Action<RenderPass> callback)
    {
        ShaderPassParametersCallbacks[key] = callback;
    }

    public void RemoveShaderPassParametersCallback(string key)
    {
        ShaderPassParametersCallbacks.Remove(key);
    }

    public Action<RenderPass>? GetShaderPassParametersCallback(string key)
    {
        Action<RenderPass>? callback = null;

        ShaderPassParametersCallbacks.TryGetValue(key, out callback);

        return callback;
    }

    public void SetShaderSource(string key, ShaderType shaderType, string shader)
    {
        if (shaderType == ShaderType.Fragment)
        {
            _fragmentShaders[key] = shader;
        }
        else if (shaderType == ShaderType.Vertex)
        {
            _vertexShaders[key] = shader;
        }
        HasShader = true;
    }

    public (string? vertexShader, string? fragmentShader) GetShaderSource(string key)
    {
        string? vertexShader = null;

        string? fragmentShader = null;


        _vertexShaders.TryGetValue(key, out vertexShader);

        _fragmentShaders.TryGetValue(key, out fragmentShader);

       return (vertexShader, fragmentShader);
    }

    public void RemoveShader(string key, ShaderType shaderType)
    {
        if (shaderType == ShaderType.Fragment)
        {
            _fragmentShaders.Remove(key);
        }
        else if (shaderType == ShaderType.Vertex)
        {
            _vertexShaders.Remove(key);
        }
        if (_fragmentShaders.Count == 0 && _vertexShaders.Count == 0)
        {
            HasShader = false;
        }
    }


    public void Upload(Silk.NET.OpenGLES.GL gl)
    {
    }

    public void Destroy(Silk.NET.OpenGLES.GL gl)
    {
        foreach(var shader in Shaders)
        {
            gl.DeleteProgram(shader.Value.ProgramId);
        }
        Shaders.Clear();
    }

    public ITexture? BaseColor
    {
        get => GetTexture("BaseColor");
        set => SetTexture("BaseColor", value);
    }

    public ITexture? Normal
    {
        get => GetTexture("Normal");
        set => SetTexture("Normal", value);
    }

    public void SetTexture(string name, ITexture? texture)
    {
        var channel = Channels.FirstOrDefault(c => c.Name == name);
        if (channel != null)
        {
            channel.Texture = texture;
        }
        else
        {
            Channels.Add(new Channel { Name = "Normal", Texture = texture });
        }
    }

    public ITexture? GetTexture(string name)
    {
        var channel = Channels.FirstOrDefault(c => c.Name == name);
        if (channel != null)
        {
             return channel.Texture;
        }
        else
        {
            return null;
        }
    }


}


public class Channel
{
    public string Name { get; set; } = string.Empty;

    public ITexture? Texture { get; set; }
}

public enum BlendMode
{
    Opaque,
    Masked,
    Translucent,
}

public enum ShaderType
{
    Vertex,
    Fragment,
}