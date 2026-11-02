namespace Celeste.Mod.VerticalCornerBoost;

/// <summary>
/// 只负责挂/摘 Hook，具体判定放在 <see cref="CornerBoostLogic"/>。
/// </summary>
internal static class CornerBoostHooks {
    public static void Load() {
        On.Celeste.Player.Update += onPlayerUpdate;
        Everest.Events.Level.OnCreatePauseMenuButtons += onCreatePauseMenuButtons;

        // 把运行时读到的原版常量打进日志（log.txt），方便确认反射有没有成功
        Logger.Log(LogLevel.Info, "VerticalCornerBoost", $"Vanilla ClimbHopY = {CornerBoostLogic.ClimbHopYSpeed}");
    }

    public static void Unload() {
        On.Celeste.Player.Update -= onPlayerUpdate;
        Everest.Events.Level.OnCreatePauseMenuButtons -= onCreatePauseMenuButtons;
        CornerBoostLogic.Clear();
    }

    // On Hook：原版逻辑跑完之后再看一眼几何，只在原版没给速度时补上
    private static void onPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self) {
        orig(self);
        CornerBoostLogic.Update(self);
    }

    private static void onCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
        // minimal（简易暂停菜单）不塞自定义项；玩家也可以在 Mod 选项里关掉这个子菜单
        if (minimal || !VerticalCornerBoostModule.Settings.ShowPauseMenuSubMenu) {
            return;
        }

        // 传入 Option 时 Option.Added() 会把 Container.InnerContent 改成 TwoColumn，
        // 那会让暂停菜单所有原版按钮从居中对齐变成靠左。先记下原值（原版是 OneColumn）。
        TextMenu.InnerContentMode originalInnerContent = menu.InnerContent;

        TextMenuExt.SubMenu subMenu = new TextMenuExt.SubMenu(Dialog.Clean("VCB_MENU_TITLE"), enterOnSelect: false);
        CornerBoostUI.AddPauseMenuItems(subMenu);
        menu.Add(subMenu);

        // 必须在 menu.Add(subMenu) 之后还原：子菜单里的项要等子菜单挂进 menu 才会触发 Added()
        menu.InnerContent = originalInnerContent;
        menu.RecalculateSize();
    }
}
