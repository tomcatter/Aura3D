namespace Aura3D.Core.Renderers;

/// <summary>
/// 渲染管线的用户可配置设置。
/// 构造时确定的设置在管线创建时生效，运行时设置修改后下帧即刻生效。
/// </summary>
public class PipelineSettings
{
    // ═══════════════════════════════════════════════
    //  构造时确定（修改后需重建 Pipeline 才能生效）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 渲染目标的默认深度纹理格式。
    /// 大场景或深度精度不足时可设置为 <see cref="TextureFormat.DepthComponent32f"/>。
    /// </summary>
    public TextureFormat DepthFormat { get; set; } = TextureFormat.DepthComponent32f;

    /// <summary>
    /// 方向光源最大数量（影响 Shader UBO 分配）。
    /// </summary>
    public int DirectionalLightLimit { get; set; } = 4;

    /// <summary>
    /// 点光源最大数量（影响 Shader UBO 分配）。
    /// </summary>
    public int PointLightLimit { get; set; } = 4;

    /// <summary>
    /// 聚光灯最大数量（影响 Shader UBO 分配）。
    /// </summary>
    public int SpotLightLimit { get; set; } = 4;

    // ═══════════════════════════════════════════════
    //  运行时可变（修改后下帧即刻生效）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 色调映射曝光度。
    /// </summary>
    public float ToneMappingExposure { get; set; } = 0.7f;

    /// <summary>
    /// HDR 亮度钳制上限。
    /// </summary>
    public float BrightnessClamp { get; set; } = 4.0f;

    /// <summary>
    /// 全局环境光强度。
    /// </summary>
    public float AmbientIntensity { get; set; } = 0.1f;

    /// <summary>
    /// 是否启用 FXAA 抗锯齿。
    /// </summary>
    public bool EnableFxaa { get; set; } = true;

    /// <summary>
    /// 是否启用视锥体剔除。
    /// </summary>
    public bool EnableFrustumCulling { get; set; } = true;

    /// <summary>
    /// 调试可视化设置。
    /// </summary>
    public DebugSettings Debug { get; set; } = new DebugSettings();

    // ═══════════════════════════════════════════════
    //  CSM（级联阴影贴图）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 方向光 CSM 级联数量。设为 1 退化为普通阴影，建议值 3。
    /// 更多级联 = 更好的大场景阴影质量，但会增加 GPU 开销。
    /// </summary>
    public int CsmCascadeCount { get; set; } = 3;

    /// <summary>
    /// PSSM 分割参数。0 = 均匀分割，1 = 对数分割（近处更密），默认 0.5。
    /// 场景越大越建议增大该值，让近处级联覆盖更小的范围。
    /// </summary>
    public float CsmSplitLambda { get; set; } = 0.5f;

    /// <summary>
    /// CSM 每级联的阴影贴图分辨率（宽高相同）。默认 1024，大场景建议 2048。
    /// </summary>
    public int CsmShadowMapResolution { get; set; } = 1024;
}
