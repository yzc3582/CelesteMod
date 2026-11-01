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
}