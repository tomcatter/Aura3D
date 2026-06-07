namespace Aura3D.Core.Renderers;

/// <summary>
/// 调试可视化设置。所有属性均为运行时可变，修改后下帧即刻生效。
/// </summary>
public class DebugSettings
{
    /// <summary>
    /// 调试绘制总开关。设为 <c>false</c> 时，以下所有可视化全部隐藏。
    /// </summary>
    public bool Enable { get; set; } = false;

    /// <summary>
    /// 是否显示网格包围盒。
    /// </summary>
    public bool ShowBoundingBox { get; set; } = false;

    /// <summary>
    /// 是否显示方向光源调试可视化。
    /// </summary>
    public bool ShowDirectionalLight { get; set; } = false;

    /// <summary>
    /// 是否显示点光源调试可视化。
    /// </summary>
    public bool ShowPointLight { get; set; } = false;

    /// <summary>
    /// 是否显示聚光灯调试可视化。
    /// </summary>
    public bool ShowSpotLight { get; set; } = false;

    /// <summary>
    /// 是否显示相机调试可视化。
    /// </summary>
    public bool ShowCamera { get; set; } = false;

    /// <summary>
    /// 是否显示骨骼调试可视化。
    /// </summary>
    public bool ShowBone { get; set; } = false;

    /// <summary>
    /// 是否显示粒子系统包围盒（橙色线框）。
    /// </summary>
    public bool ShowParticleBounds { get; set; } = false;
}
