using System;
using Monocle;

namespace Celeste.Mod.VerticalCornerBoost;

/// <summary>
/// 游戏内独立设置界面。做法照 Extended Variant Mode 的 AbstractSubmenu：
/// 暂停菜单里放一个按钮，按下去把父菜单从场景里摘掉，再往 Level 里塞一个独立的 TextMenu；
/// ESC / 取消时关掉它并把父菜单放回去。
///
/// 这样做而不是用 TextMenuExt.SubMenu 的原因：下拉式子菜单会把所有行挤进同一个
/// 固定宽度里，行一多就互相叠字；独立界面用的是原版 TextMenu 的完整布局，
/// 有 header、会滚动、选择和返回行为都和原版一致。
/// </summary>
internal static class CornerBoostMenu {
    /// <summary>暂停菜单里的入口按钮（插在“返回地图”之前，和 EVM 放 Options 附近的做法一致）。</summary>
    public static TextMenu.Item BuildOpenButton(TextMenu parentMenu, Level level) {
        return new TextMenu.Button(Dialog.Clean("VCB_MENU_TITLE")).Pressed(() => Open(parentMenu, level));
    }

    private static void Open(TextMenu parentMenu, Level level) {
        Audio.Play("event:/ui/main/button_select");

        // 和原版“选项”按钮一样：先把父菜单摘掉，告诉暂停逻辑我们不再处于主菜单
        bool comesFromPauseMainMenu = level.PauseMainMenuOpen;
        parentMenu.RemoveSelf();
        level.PauseMainMenuOpen = false;

        TextMenu menu = new TextMenu();
        menu.Add(new TextMenu.Header(Dialog.Clean("VCB_MENU_TITLE")));
        CornerBoostUI.AddSettingsItems(menu, inGame: true);
        menu.Add(new TextMenu.Button(Dialog.Clean("VCB_MENU_BACK")).Pressed(back));

        void back() {
            // 官方 CloseAndRun 会先 Focused=false 再关；这里必须照做。
            // 否则这一帧里 OnESC/OnCancel/“返回”按钮可能都触发一次，
            // 导致 level.Add(parentMenu) 被调用多次（父菜单在列表里出现重复条目）。
            if (!menu.Focused) {
                return;
            }
            menu.Focused = false;

            Audio.Play("event:/ui/main/button_back");
            VerticalCornerBoostModule.Instance.SaveSettings();
            menu.Close();
            level.Add(parentMenu);
            level.PauseMainMenuOpen = comesFromPauseMainMenu;
        }

        menu.OnESC = back;
        menu.OnCancel = back;

        // 直接在暂停菜单里按暂停键：走 Level.Unpause()，它会自己关掉 TextMenu、
        // 播解除暂停音效并重置 freeze timer；自己再关一次会重复播音效。
        menu.OnPause = () => {
            VerticalCornerBoostModule.Instance.SaveSettings();
            level.Unpause();
        };

        level.Add(menu);
    }
}
