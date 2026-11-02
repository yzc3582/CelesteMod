using System;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.VerticalCornerBoost;

/// <summary>
/// 菜单项的构建器。Mod 选项菜单、暂停菜单里的入口、以及独立的设置界面共用这一套，
/// 保证颜色语义完全一致：金=玩家改过、蓝=地图改的、白=默认。
/// </summary>
internal static class CornerBoostUI {
    public const string PresetPrefix = "modoptions_verticalcornerboostmodule_preset_";

    public static TextMenu.Item EnabledItem(string label) {
        ColoredOnOff item = new ColoredOnOff(
            label,
            CornerBoostState.Enabled,
            () => CornerBoostState.EnabledColor,
            () => CornerBoostState.Enabled);
        item.Change(CornerBoostState.SetEnabledFromMenu);
        return item;
    }

    public static TextMenu.Item RequireGrabItem(string label) {
        ColoredOnOff item = new ColoredOnOff(
            label,
            CornerBoostState.RequireGrab,
            () => CornerBoostState.RequireGrabColor,
            () => CornerBoostState.RequireGrab);
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
            () => CornerBoostState.SpeedColor,
            () => CornerBoostState.PresetIndex);
        slider.Change(index => CornerBoostState.SetPresetFromMenu((CornerBoostPreset)index));
        return slider;
    }

    public static TextMenu.Item CustomSpeedItem(TextMenu menu, bool inGame) {
        ColoredButton button = new ColoredButton(
            () => Dialog.Clean("modoptions_verticalcornerboostmodule_customspeed")
                  + ": " + VerticalCornerBoostModule.Settings.CustomSpeed,
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
            () => CornerBoostState.LimitCustomSpeedColor,
            () => VerticalCornerBoostModule.Settings.LimitCustomSpeed);
        item.Change(on => VerticalCornerBoostModule.Settings.LimitCustomSpeed = on);
        return item;
    }

    public static TextMenu.Item ShowPauseMenuSubMenuItem() {
        ColoredOnOff item = new ColoredOnOff(
            Dialog.Clean("modoptions_verticalcornerboostmodule_showpausemenusubmenu"),
            VerticalCornerBoostModule.Settings.ShowPauseMenuSubMenu,
            () => CornerBoostState.ShowPauseMenuSubMenuColor,
            () => VerticalCornerBoostModule.Settings.ShowPauseMenuSubMenu);
        item.Change(on => VerticalCornerBoostModule.Settings.ShowPauseMenuSubMenu = on);
        return item;
    }

    /// <summary>
    /// 恢复默认值。语义照 EVM：按下就立刻把值改回默认，不动玩家设置以外的任何东西。
    /// 速度档位、自定义速度、上限、抓握要求、启用状态全部回到出厂值。
    /// </summary>
    public static TextMenu.Item ResetToDefaultItem() {
        return new TextMenu.Button(Dialog.Clean("VCB_MENU_RESET")).Pressed(() => {
            Audio.Play("event:/ui/main/button_toggle_on");
            CornerBoostState.ResetToDefaults();
        });
    }

    /// <summary>独立设置界面里的完整内容（暂停菜单入口按进去看到的就是这一套）。</summary>
    public static void AddSettingsItems(TextMenu menu, bool inGame) {
        menu.Add(EnabledItem(Dialog.Clean("VCB_MENU_TOGGLE")));
        menu.Add(RequireGrabItem(Dialog.Clean("VCB_MENU_REQUIRE_GRAB")));
        menu.Add(PresetItem());

        // 地图把速度钉死了的时候，滑块只能停在最近的档位，补一行把真实数值说清楚
        if (CornerBoostState.MapValues.SpeedUp is float mapSpeed && !CornerBoostState.IsPlayerOverridingSpeed) {
            menu.Add(new TextMenu.SubHeader(string.Format(Dialog.Clean("VCB_MENU_MAP_SPEED"), (int)Math.Round(mapSpeed))));
        }

        menu.Add(LimitCustomSpeedItem());
        menu.Add(ResetToDefaultItem());
    }
}

/// <summary>带动态颜色的 OnOff。</summary>
internal sealed class ColoredOnOff : TextMenu.OnOff {
    private readonly Func<Color> color;
    private readonly Func<bool> syncValue;

    /// <param name="color">每帧现算的颜色。</param>
    /// <param name="syncValue">每帧现算的“当前真实值”；外部（恢复默认值 / 地图触发器）改了状态时，
    /// 开关也能跟着翻到正确的一侧。</param>
    public ColoredOnOff(string label, bool value, Func<Color> color, Func<bool> syncValue) : base(label, value) {
        this.color = color;
        this.syncValue = syncValue;
    }

    public override void Update() {
        base.Update();
        // Values 里 0 = 关、1 = 开（TextMenu.OnOff 的构造顺序）
        int target = syncValue() ? 1 : 0;
        if (target != Index && target >= 0 && target < Values.Count) {
            // 只同步显示，不调 OnValueChange，避免反过来又写一遍设置
            Index = target;
            PreviousIndex = target;
        }
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
    private readonly Func<int> syncIndex;

    /// <param name="color">每帧现算的颜色。</param>
    /// <param name="syncIndex">每帧现算的“当前应该停在哪一格”；地图改了速度时滑块也能跟着动。</param>
    public ColoredSlider(string label, Func<int, string> values, int min, int max, int value, Func<Color> color, Func<int> syncIndex)
        : base(label, values, min, max, value) {
        this.color = color;
        this.syncIndex = syncIndex;
    }

    public override void Update() {
        base.Update();
        int target = syncIndex();
        if (target >= 0 && target < Values.Count && target != Index) {
            // 直接改 Index 而不是调 OnValueChange：这只是“显示同步”，不该反过来写设置
            Index = target;
            PreviousIndex = target;
        }
    }

    public override void Render(Vector2 position, bool highlighted) {
        UnselectedColor = color();
        base.Render(position, highlighted);
    }
}

/// <summary>带动态颜色和动态文本的按钮（TextMenu.Button 没有 UnselectedColor 字段，只能重画）。</summary>
internal sealed class ColoredButton : TextMenu.Button {
    private readonly Func<string> label;
    private readonly Func<Color> color;

    public ColoredButton(Func<string> label, Func<Color> color) : base(label()) {
        this.label = label;
        this.color = color;
    }

    public override void Update() {
        base.Update();
        // 自定义速度是文本输入，改完回到菜单时要能看到新值
        Label = label();
    }

    public override void Render(Vector2 position, bool highlighted) {
        float alpha = Container.Alpha;
        Color textColor = Disabled
            ? Color.DarkSlateGray
            : (highlighted ? Container.HighlightColor : color()) * alpha;
        Color strokeColor = Color.Black * (alpha * alpha * alpha);

        // 和原版 Button.Render 逐行对应：OneColumn 居中，TwoColumn（或被强制定中）另算
        bool twoColumn = Container.InnerContent == TextMenu.InnerContentMode.TwoColumn && !AlwaysCenter;
        Vector2 drawPosition = position + (twoColumn ? Vector2.Zero : new Vector2(Container.Width * 0.5f, 0f));
        Vector2 justify = twoColumn ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);

        ActiveFont.DrawOutline(Label, drawPosition, justify, Vector2.One, textColor, 2f, strokeColor);
    }
}
