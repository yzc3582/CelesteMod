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
            // 这个按钮理论上只出现在 Mod 选项菜单（Overworld），但 SceneAs 失败时返回 null，
            // 直接 .Goto() 会空引用崩溃。加一层判空，退化成“不可用”而不是崩游戏。
            Overworld overworld = menu.SceneAs<Overworld>();
            if (overworld == null) {
                Logger.Warn("VerticalCornerBoost", "CustomSpeed: current scene is not Overworld, cannot open string input.");
                return;
            }

            Audio.Play("event:/ui/main/savefile_rename_start");
            overworld
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
    /// 速度档位、自定义速度、上限、抓握要求、启用状态、暂停菜单入口全部回到出厂值。
    /// </summary>
    /// <param name="label">显示名。Mod 选项与暂停菜单各用各的本地化键。</param>
    public static TextMenu.Item ResetToDefaultItem(string label) {
        return new TextMenu.Button(label).Pressed(() => {
            Audio.Play("event:/ui/main/button_toggle_on");
            CornerBoostState.ResetToDefaults();
        });
    }

    /// <summary>独立设置界面里的完整内容（暂停菜单入口按进去看到的就是这一套）。</summary>
    /// <param name="inGame">游戏内（暂停界面）时为 true。目前这一套控件在游戏内也都可用，
    /// 所以没有用它做禁用；保留参数是为了和 Mod 选项那边的签名一致。</param>
    public static void AddSettingsItems(TextMenu menu, bool inGame) {
        menu.Add(EnabledItem(Dialog.Clean("VCB_MENU_TOGGLE")));
        menu.Add(RequireGrabItem(Dialog.Clean("VCB_MENU_REQUIRE_GRAB")));
        menu.Add(PresetItem());

        // 地图把速度钉死了的时候，滑块只能停在最近的档位，补一行把真实数值说清楚。
        // 这一行必须**动态**：玩家在界面里改设置、或 flag 让地图覆盖生效/失效时，文字要跟着变，
        // 不然会显示一个过时的数值。
        menu.Add(new ColoredSubHeader(
            () => CornerBoostState.MapValues.SpeedUp is float mapSpeed && !CornerBoostState.IsPlayerOverridingSpeed
                ? string.Format(Dialog.Clean("VCB_MENU_MAP_SPEED"), (int)Math.Round(mapSpeed))
                : ""));

        menu.Add(LimitCustomSpeedItem());
        menu.Add(ResetToDefaultItem(Dialog.Clean("VCB_MENU_RESET")));
    }
}

/// <summary>
/// 带动态文本的 SubHeader。用来显示“地图速度：xxx”这类只在特定状态才有意义的信息。
///
/// 两个必须的细节：
/// - 文字每帧在 Render 里现算，条件不成立就置空（原版 Render 遇到空 Title 会跳过绘制）；
/// - **Height() 必须恒定**：原版 `Height()` 是 `(Title 非空 ? 字高*0.6 : 0) + (TopPadding?48:0)`，
///   文字一出现/消失，行高会相差十几像素，导致下面的菜单项整体跳一下。
///   这里覆写成固定行高，保证布局永远不抖。
/// </summary>
internal sealed class ColoredSubHeader : TextMenu.SubHeader {
    private readonly Func<string> text;

    public ColoredSubHeader(Func<string> text) : base(text()) {
        this.text = text;
    }

    public override float Height() {
        // 固定用「一行字 + 上方留白」，与当前是否有文字无关
        return ActiveFont.LineHeight * Scale + (TopPadding ? 48f : 0f);
    }

    public override void Update() {
        base.Update();
        // 每帧现算：flag 变了、玩家改了设置，这行字都要立刻跟上。
        // 在 Update 里改（而不是 Render 里）——渲染期间重算布局不安全。
        string next = text();
        if (next != Title) {
            Title = next;
            // TextMenu.Width 是缓存的，只在 RecalculateSize() 里更新；文字增减要重算宽度。
            Container?.RecalculateSize();
        }
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
        string next = label();
        if (next != Label) {
            Label = next;
            // TextMenu.Width 是**缓存的**，只在 RecalculateSize() 里更新。
            // 文字变长（120 -> 100000）却不重算，会越过菜单右边界。
            Container?.RecalculateSize();
        }
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
