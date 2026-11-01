namespace Celeste.Mod.VerticalCornerBoost;

public class VerticalCornerBoostModuleSettings : EverestModuleSettings {
    /// <summary>
    /// 是否启用纵向抓角加速。
    /// Mod 选项菜单的勾选框和暂停菜单里的 OnOff 共用这一个设置源。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 暂停菜单里是否显示本 Mod 的子菜单（子菜单内含启用开关）。
    /// </summary>
    public bool ShowPauseMenuSubMenu { get; set; } = true;

    /// <summary>
    /// 速度档位。Vanilla 与原版 ClimbHop 等值（-120f），其余档位是可选的加强。
    /// </summary>
    public CornerBoostPreset Preset { get; set; } = CornerBoostPreset.Vanilla;

    /// <summary>
    /// 自定义上升速度：填正数（例如 160），程序会自动转成向上速度（-160）。
    /// 仅在 Preset = Custom 时生效；填错会退回原版值。
    /// </summary>
    [SettingMaxLength(6)]
    public string CustomSpeed { get; set; } = "120";

    /// <summary>
    /// 是否限制自定义速度的上限（1000 px/s）。
    /// 默认开启。关掉后想填多大都行，代价是可能出现穿墙观感、摄像机乱飞甚至卡顿，不推荐。
    /// 真正的输入兜底是 SettingMaxLength(6)，最多 6 个字符，不会出现天文数字。
    /// </summary>
    public bool LimitCustomSpeed { get; set; } = true;
}

/// <summary>
/// 纵向速度档位。底层数值决定 Mod 选项滑块里的排列顺序（Everest 按 enum 数值排序生成 Slider）。
/// </summary>
public enum CornerBoostPreset {
    Vanilla = 0,
    Strong = 1,
    VeryStrong = 2,
    Custom = 3,
}