using System;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.VerticalCornerBoost;

/// <summary>
/// 菜单项的构建器。Mod 选项菜单（CreateXxxEntry）和暂停菜单子菜单共用这一套，
/// 保证两边的颜色语义完全一致：金=玩家改过、蓝=地图改的、白=默认。
/// </summary>
internal static class CornerBoostUI {
    public const string PresetPrefix = "modoptions_verticalcornerboostmodule_preset_";

    public static TextMenu.Item EnabledItem(string label) {
        ColoredOnOff item = new ColoredOnOff(label, CornerBoostState.Enabled, () => CornerBoostState.EnabledColor);
        item.Change(CornerBoostState.SetEnabledFromMenu);
        return item;
    }

    public static TextMenu.Item RequireGrabItem(string label) {
        ColoredOnOff item = new ColoredOnOff(label, CornerBoostState.RequireGrab, () => CornerBoostState.RequireGrabColor);
        item.Change(CornerBoostState.SetRequireGrabFromMenu);
        return item;
    }

    public static TextMenu.Item PresetItem() {
        ColoredSlider slider = new ColoredSlider(
            Dialog.Clean("modoptions_verticalcornerboostmodule_preset"),
            index => Dialog.Clean(PresetPrefix + ((CornerBoostPreset)index).ToString().ToLowerInvariant()),
            0,
            (int)CornerBoostPreset.Custom,
            CornerBoostState.PresetIndex,
            () => CornerBoostState.SpeedColor);
        slider.Change(index => CornerBoostState.SetPresetFromMenu((CornerBoostPreset)index));
        return slider;
    }

    public static TextMenu.Item CustomSpeedItem(TextMenu menu, bool inGame) {
        ColoredButton button = new ColoredButton(
            Dialog.Clean("modoptions_verticalcornerboostmodule_customspeed") + ": " + VerticalCornerBoostModule.Settings.CustomSpeed,
            () => CornerBoostState.CustomSpeedColor);
        button.Pressed(() => {
            Audio.Play("event:/ui/main/savefile_rename_start");
            menu.SceneAs<Overworld>()
                .Goto<OuiModOptionString>()
                .Init<OuiModOptions>(
                    VerticalCornerBoostModule.Settings.CustomSpeed,
                    CornerBoostState.SetCustomSpeedFromMenu,
                    6,
                    1);
        });
        // 游戏内不能打开字符串输入界面（Everest 自己也是这么处理的）
        button.Disabled = inGame;
        return button;
    }

    public static TextMenu.Item LimitCustomSpeedItem() {
        ColoredOnOff item = new ColoredOnOff(
            Dialog.Clean("modoptions_verticalcornerboostmodule_limitcustomspeed"),
            VerticalCornerBoostModule.Settings.LimitCustomSpeed,
            () => CornerBoostState.LimitCustomSpeedColor);
        item.Change(on => VerticalCornerBoostModule.Settings.LimitCustomSpeed = on);
        return item;
    }

    public static TextMenu.Item ShowPauseMenuSubMenuItem() {
        ColoredOnOff item = new ColoredOnOff(
            Dialog.Clean("modoptions_verticalcornerboostmodule_showpausemenusubmenu"),
            VerticalCornerBoostModule.Settings.ShowPauseMenuSubMenu,
            () => CornerBoostState.ShowPauseMenuSubMenuColor);
        item.Change(on => VerticalCornerBoostModule.Settings.ShowPauseMenuSubMenu = on);
        return item;
    }

    /// <summary>暂停菜单子菜单：只放游玩中真正会改的东西。</summary>
    public static void AddPauseMenuItems(TextMenuExt.SubMenu subMenu) {
        subMenu.Add(EnabledItem(Dialog.Clean("VCB_MENU_TOGGLE")));
        subMenu.Add(RequireGrabItem(Dialog.Clean("VCB_MENU_REQUIRE_GRAB")));
        subMenu.Add(PresetItem());
        subMenu.Add(LimitCustomSpeedItem());

        // 地图固定了速度时，滑块只能显示最近的档位，补一行把真实数值说清楚
        if (CornerBoostState.MapValues.SpeedUp is float mapSpeed && !CornerBoostState.IsPlayerOverridingSpeed) {
            subMenu.Add(new TextMenu.SubHeader(string.Format(Dialog.Clean("VCB_MENU_MAP_SPEED"), (int)Math.Round(mapSpeed))));
        }
    }
}

/// <summary>带动态颜色的 OnOff。</summary>
internal sealed class ColoredOnOff : TextMenu.OnOff {
    private readonly Func<Color> color;

    public ColoredOnOff(string label, bool value, Func<Color> color) : base(label, value) {
        this.color = color;
    }

    public override void Render(Vector2 position, bool highlighted) {
        // 原版 Render 直接读这个字段，所以每帧刷新一下就能得到动态颜色
        UnselectedColor = color();
        base.Render(position, highlighted);
    }
}

/// <summary>带动态颜色的档位滑块。</summary>
internal sealed class ColoredSlider : TextMenu.Slider {
    private readonly Func<Color> color;

    public ColoredSlider(string label, Func<int, string> values, int min, int max, int value, Func<Color> color)
        : base(label, values, min, max, value) {
        this.color = color;
    }

    public override void Render(Vector2 position, bool highlighted) {
        UnselectedColor = color();
        base.Render(position, highlighted);
    }
}

/// <summary>带动态颜色的按钮（TextMenu.Button 没有 UnselectedColor 字段，只能重画）。</summary>
internal sealed class ColoredButton : TextMenu.Button {
    private readonly Func<Color> color;

    public ColoredButton(string label, Func<Color> color) : base(label) {
        this.color = color;
    }

    public override void Render(Vector2 position, bool highlighted) {
        float alpha = Container.Alpha;
        Color textColor = Disabled
            ? Color.DarkSlateGray
            : (highlighted ? Container.HighlightColor : color()) * alpha;
        Color strokeColor = Color.Black * (alpha * alpha * alpha);

        // 和暂停菜单里的原版按钮一样：OneColumn 居中，TwoColumn 靠左
        bool twoColumn = Container.InnerContent == TextMenu.InnerContentMode.TwoColumn && !AlwaysCenter;
        Vector2 drawPosition = position + (twoColumn ? Vector2.Zero : new Vector2(Container.Width * 0.5f, 0f));
        Vector2 justify = twoColumn ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);

        ActiveFont.DrawOutline(Label, drawPosition, justify, Vector2.One, textColor, 2f, strokeColor);
    }
}
