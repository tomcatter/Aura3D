using Aura3D.Core.Renderers;
using System.Drawing;

namespace Aura3D.Core.Resources;

/// <summary>
/// 材质类，定义物体的表面属性和渲染行为
/// </summary>
public class Material : IClone<Material>, IGpuResource
{
    /// <summary>
    /// 是否需要上传到GPU
    /// </summary>
    public bool NeedsUpload { get; set; } = false;
    /// <summary>
    /// 材质通道列表
    /// </summary>
    public List<Channel> Channels { get; set; } = [];

    private Dictionary<string, object> parameters  { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// 混合模式
    /// </summary>
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;

    /// <summary>
    /// 是否双面渲染
    /// </summary>
    public bool DoubleSided { get; set; } = false;

    /// <summary>
    /// 透明度阈值
    /// </summary>
    public float AlphaCutoff { get; set; } = 0.5f;

    /// <summary>
    /// 是否有自定义着色器
    /// </summary>
    public bool HasShader { get; set; } = false;
    /// <summary>
    /// 顶点着色器字典（只读）
    /// </summary>
    public IReadOnlyDictionary<string, string> VertexShaders => _vertexShaders;

    private Dictionary<string, string> _vertexShaders = new Dictionary<string, string>();

    private Dictionary<string, string> _fragmentShaders = new Dictionary<string, string>();

    /// <summary>
    /// 片段着色器字典（只读）
    /// </summary>
    public IReadOnlyDictionary<string, string> FragmentShaders => _fragmentShaders;

    /// <summary>
    /// 着色器字典
    /// </summary>
    public Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

    /// <summary>
    /// 尝试获取参数值
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="key">参数键名</param>
    /// <param name="value">参数值</param>
    /// <returns>是否成功获取</returns>
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

    /// <summary>
    /// 设置参数值
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="key">参数键名</param>
    /// <param name="value">参数值</param>
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
        material._vertexShaders = [];
        foreach (var vs in _vertexShaders)
        {
            material._vertexShaders.Add(vs.Key, vs.Value);
        }

        material._fragmentShaders = [];
        foreach (var fs in _fragmentShaders)
        {
            material._fragmentShaders.Add(fs.Key, fs.Value);
        }

        material.ShaderPassParametersCallbacks = [];
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
            Channels.Add(new Channel { Name = name, Texture = texture });
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

/// <summary>
/// 材质通道类，包含纹理通道
/// </summary>

/// <summary>
/// 通道名称
/// </summary>
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