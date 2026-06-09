using System.Drawing;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 渲染管线的渲染目标管理部分，负责渲染目标的注册、缓存和回收。
/// </summary>
public abstract partial class RenderPipeline
{
    Dictionary<string, Dictionary<Size, (RenderTarget, DateTime)>> RenderTargets = new();

    Dictionary<string, RenderTargetConf> RenderTargetConfs = new();

    private void UpdateRenderTargetsLRU()
    {
        foreach (var (name, rtMap) in RenderTargets)
        {
            var expiredSizes = new List<Size>();
            foreach (var (rtSize, (rt, dateTime)) in rtMap)
            {
                if (DateTime.Now - dateTime > TimeSpan.FromSeconds(1))
                {
                    rt.Destroy(gl!);
                    expiredSizes.Add(rtSize);
                }
            }
            foreach (var rtSize in expiredSizes)
            {
                rtMap.Remove(rtSize);
            }
        }
    }

    /// <summary>
    /// 注册一个具有指定名称的渲染目标，并返回其配置对象。
    /// </summary>
    /// <param name="name">渲染目标的名称。</param>
    /// <returns>渲染目标配置对象。</returns>
    public RenderTargetConf RegisterRenderTarget(string name)
    {
        if (RenderTargetConfs.ContainsKey(name) == false)
        {
            var rtc = new RenderTargetConf();
            RenderTargetConfs.Add(name, rtc);
            return rtc;
        }
        return RenderTargetConfs[name];
    }

    /// <summary>
    /// 获取指定名称和大小的渲染目标实例，若不存在则自动创建。
    /// </summary>
    /// <param name="name">渲染目标的名称。</param>
    /// <param name="size">渲染目标的尺寸。</param>
    /// <returns>渲染目标实例。</returns>
    /// <exception cref="KeyNotFoundException">当渲染目标未注册时抛出。</exception>
    public RenderTarget GetRenderTarget(string name, Size size)
    {
        if (RenderTargetConfs.TryGetValue(name, out var rtConf))
        {
            if (RenderTargets.TryGetValue(name, out var rtMap) == false)
            {
                rtMap = new Dictionary<Size, (RenderTarget, DateTime)>();
                RenderTargets.Add(name, rtMap);
            }

            if (rtMap.TryGetValue(size, out var rt) == false)
            {
                rt = (new RenderTarget()
                    .SetSize((uint)size.Width, (uint)size.Height)
                    .SetDepthTexture(rtConf.DepthTextureFormat), DateTime.Now);

                foreach(var (textureName, textureFormat) in rtConf.Textures)
                {
                    rt.Item1.AddRenderTexture(textureName, textureFormat);
                }
                EnsureUploaded(rt.Item1);
                rtMap.Add(size, rt);
            }
            else
            {
                rt.Item2 = DateTime.Now;
                rtMap[size] = rt;
            }
            return rt.Item1;
        }

        throw new KeyNotFoundException($"RenderTarget '{name}' not found. Ensure the render target is registered before use.");
    }
}

/// <summary>
/// 渲染目标配置类，用于定义渲染目标所包含的颜色纹理和深度纹理格式。
/// </summary>
public class RenderTargetConf
{
    /// <summary>
    /// 获取渲染目标中所有颜色纹理的列表。
    /// </summary>
    public List<(string, TextureFormat)> Textures = new ();

    HashSet<string> TextureNames = new HashSet<string>();

    /// <summary>
    /// 获取或设置深度纹理的格式。
    /// </summary>
    public TextureFormat DepthTextureFormat;

    /// <summary>
    /// 向渲染目标添加一个颜色纹理。
    /// </summary>
    /// <param name="name">纹理名称。</param>
    /// <param name="internalFormat">纹理内部格式。</param>
    /// <returns>当前的 <see cref="RenderTargetConf"/> 实例。</returns>
    /// <exception cref="ArgumentException">当同名纹理已存在时抛出。</exception>
    public RenderTargetConf AddTexture(string name, TextureFormat internalFormat)
    {
        if (TextureNames.Contains(name))
            throw new ArgumentException($"Texture '{name}' already exists in render target configuration.", nameof(name));
        Textures.Add((name, internalFormat));
        TextureNames.Add(name);
        return this;
    }

    /// <summary>
    /// 设置渲染目标的深度纹理格式。
    /// </summary>
    /// <param name="textureFormat">深度纹理格式。</param>
    /// <returns>当前的 <see cref="RenderTargetConf"/> 实例。</returns>
    public RenderTargetConf SetDepthTexture(TextureFormat textureFormat)
    {
        DepthTextureFormat = textureFormat;
        return this;
    }
}
