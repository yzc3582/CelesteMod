namespace Celeste.Mod.VerticalCornerBoost;

public class VerticalCornerBoostModuleSettings : EverestModuleSettings {
    /// <summary>
    /// 是否启用纵向抓角加速。
    /// Mod 选项菜单的勾选框和暂停菜单里的 OnOff 共用这一个设置源。
    /// 默认关闭；玩家可以随时打开。
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 是否只在“抓握”时才触发。
    /// 读的是 Input.GrabCheck，它已经正确处理按住 / 反转 / 切换三种抓键模式；
    /// 这里判断的是“抓握状态”而不是“能否抓墙”，所以即使玩家抓不到墙也照样算抓握。
    /// Extended Variants 的 True No Grabbing 会把 GrabCheck 强制变成 false，
    /// 因此那种“完全阻断抓握”的异变也会自然地挡住触发。
    /// </summary>
    public bool RequireGrab { get; set; } = true;

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

    // ==================== Mod 选项菜单 ====================
    // 每个 Create<属性名>Entry 都会覆盖 Everest 自动生成的默认控件，
    // 这样 Mod 选项也能用同一套三色语义（金=玩家改、蓝=地图改、白=默认）。

    public void CreateEnabledEntry(TextMenu menu, bool inGame) {
        menu.Add(CornerBoostUI.EnabledItem(Dialog.Clean("modoptions_verticalcornerboostmodule_enabled")));
    }

    public void CreateRequireGrabEntry(TextMenu menu, bool inGame) {
        menu.Add(CornerBoostUI.RequireGrabItem(Dialog.Clean("modoptions_verticalcornerboostmodule_requiregrab")));
    }

    public void CreateShowPauseMenuSubMenuEntry(TextMenu menu, bool inGame) {
        menu.Add(CornerBoostUI.ShowPauseMenuSubMenuItem());
    }

    public void CreatePresetEntry(TextMenu menu, bool inGame) {
        menu.Add(CornerBoostUI.PresetItem());
    }

    public void CreateCustomSpeedEntry(TextMenu menu, bool inGame) {
        menu.Add(CornerBoostUI.CustomSpeedItem(menu, inGame));
    }

    public void CreateLimitCustomSpeedEntry(TextMenu menu, bool inGame) {
        menu.Add(CornerBoostUI.LimitCustomSpeedItem());
    }
}

/// <summary>
/// 纵向速度档位。底层数值决定 Mod 选项滑块里的排列顺序（Everest 按 enum 数值排序生成 Slider）。
/// 注意：**成员名是存档键**（YamlDotNet 按名字序列化），想改显示名请改 Dialog 里的
/// modoptions_verticalcornerboostmodule_preset_* 文案，不要重命名这里的成员。
/// </summary>
public enum CornerBoostPreset {
    Vanilla = 0,          // 原版：向上 120
    Strong = 1,           // 略有力气：向上 160
    VeryStrong = 2,       // 比较有力气：向上 200
    ExtremelyStrong = 3,  // 超级有力气：向上 300
    Mmtgd = 4,            // MMTGD：向上 400
    Custom = 5,           // 自定义（读 CustomSpeed）
}
