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
        // minimal（简易暂停菜单）不塞自定义项；玩家也可以在 Mod 选项里关掉这个入口
        if (minimal || !VerticalCornerBoostModule.Settings.ShowPauseMenuSubMenu) {
            return;
        }

        // 位置照 EVM：插在“选项”之前（EVM 就是插在 menu_pause_options 前面）。
        // 找不到就退回追加到末尾。
        int index = menu.Items.FindIndex(item =>
            item is TextMenu.Button button && button.Label == Dialog.Clean("menu_pause_options"));

        TextMenu.Item entry = CornerBoostMenu.BuildOpenButton(menu, level);
        if (index >= 0) {
            menu.Insert(index, entry);
        } else {
            menu.Add(entry);
        }
    }
}
