namespace Celeste.Mod.VerticalCornerBoost;

/// <summary>
/// 只负责挂/摘 Hook，具体判定放在 <see cref="CornerBoostLogic"/>。
/// </summary>
internal static class CornerBoostHooks {
    public static void Load() {
        On.Celeste.Player.Update += onPlayerUpdate;
        Everest.Events.Level.OnCreatePauseMenuButtons += onCreatePauseMenuButtons;
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
        if (minimal) {
            return;
        }

        menu.Add(new TextMenu.OnOff("Vertical Corner Boost", VerticalCornerBoostModule.Settings.Enabled)
            .Change(on => VerticalCornerBoostModule.Settings.Enabled = on));
    }
}
