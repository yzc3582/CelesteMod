namespace Celeste.Mod.VerticalCornerBoost;

public class VerticalCornerBoostModuleSettings : EverestModuleSettings {
    /// <summary>
    /// 是否启用纵向抓角加速。
    /// Mod 选项菜单的勾选框和暂停菜单里的 OnOff 共用这一个设置源。
    /// </summary>
    public bool Enabled { get; set; } = true;


}