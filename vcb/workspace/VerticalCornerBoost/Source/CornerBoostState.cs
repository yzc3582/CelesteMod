using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.VerticalCornerBoost;

/// <summary>
/// 可以被地图触发器覆盖的字段。
/// 注意：**成员名是存档键**（写在 <see cref="VerticalCornerBoostModuleSession.PlayerOverrides"/> 里，
/// YamlDotNet 按名字序列化），要改名字就得同时处理旧存档。
/// </summary>
public enum CornerBoostField {
    Enabled,
    RequireGrab,
    Speed,
}

/// <summary>
/// 地图触发器给的值。null = 这个触发器不管这个字段。
/// </summary>
internal struct CornerBoostMapValues {
    public bool? Enabled;
    public bool? RequireGrab;
    public float? SpeedUp;

    public bool IsEmpty => !Enabled.HasValue && !RequireGrab.HasValue && !SpeedUp.HasValue;
}

/// <summary>
/// 运行时状态：设置 + 地图触发器覆盖 + 玩家自己在游戏内改过的值。
///
/// 优先级模仿 Extended Variant Mode：
///   玩家在游戏内改过 → 设置值
///   否则            → 地图值 ?? 设置值
/// 颜色也照 EVM 的三色规则：
///   当前值 ≠ 地图定义值            → 金色（玩家改的）
///   当前值 = 地图定义值但 ≠ 默认值 → 蓝色（地图改的）
///   否则                           → 白色（默认）
/// 这里只做参考，不依赖 EVM；没有 EVM 时这一切也照常工作。
/// </summary>
internal static class CornerBoostState {
    public const bool DefaultEnabled = false;
    public const bool DefaultRequireGrab = true;

    private const float MaxCustomSpeed = 1000f;
    // 速度都是整数像素/秒，0.5 足够容下浮点误差
    private const float SpeedEpsilon = 0.5f;

    private sealed class MapEntry {
        public Entity Owner;
        public Level Level;
        public CornerBoostMapValues Values;
        public bool RevertOnLeave;
        public bool RevertOnDeath;
        // 可选门控：只有 flag 条件成立时这条覆盖才算数（每帧现算，flag 一变立刻生效）
        public string Flag;
        public bool FlagInverted;

        public bool IsActive(Scene scene) {
            if (Owner == null || Owner.Scene == null || Level != scene) {
                return false;
            }
            if (string.IsNullOrEmpty(Flag) || Level.Session == null) {
                return true;
            }
            bool flag = Level.Session.GetFlag(Flag);
            return FlagInverted ? !flag : flag;
        }
    }

    private static readonly List<MapEntry> entries = new();
    private static readonly HashSet<CornerBoostField> playerOverrides = new();

    private static VerticalCornerBoostModuleSettings Settings => VerticalCornerBoostModule.Settings;

    /// <summary>
    /// “玩家自己在游戏内改过哪些字段”。存在 Session 里，所以跨重启仍然有效；
    /// Session 还没加载好（主菜单早期）时退回进程内的那份。
    /// </summary>
    private static HashSet<CornerBoostField> Overrides {
        get {
            VerticalCornerBoostModuleSession session = SafeSession;
            return session?.PlayerOverrides ?? playerOverrides;
        }
    }

    private static VerticalCornerBoostModuleSession SafeSession {
        get {
            try {
                return VerticalCornerBoostModule.Instance?._Session as VerticalCornerBoostModuleSession;
            } catch {
                return null;
            }
        }
    }

    /// <summary>原版 ClimbHop 的速度（正数，向上）。</summary>
    public static float DefaultSpeedUp => Math.Abs(CornerBoostLogic.ClimbHopYSpeed);

    /// <summary>当前房间里所有生效的触发器值合并后的结果（后进入的覆盖先进入的）。</summary>
    public static CornerBoostMapValues MapValues {
        get {
            CornerBoostMapValues merged = default;
            Scene scene = Engine.Scene;
            foreach (MapEntry entry in entries) {
                if (!entry.IsActive(scene)) {
                    continue;
                }
                if (entry.Values.Enabled.HasValue) {
                    merged.Enabled = entry.Values.Enabled;
                }
                if (entry.Values.RequireGrab.HasValue) {
                    merged.RequireGrab = entry.Values.RequireGrab;
                }
                if (entry.Values.SpeedUp.HasValue) {
                    merged.SpeedUp = entry.Values.SpeedUp;
                }
            }
            return merged;
        }
    }

    // ==================== 生效值 ====================

    public static bool Enabled =>
        Overrides.Contains(CornerBoostField.Enabled)
            ? Settings.Enabled
            : MapValues.Enabled ?? Settings.Enabled;

    public static bool RequireGrab =>
        Overrides.Contains(CornerBoostField.RequireGrab)
            ? Settings.RequireGrab
            : MapValues.RequireGrab ?? Settings.RequireGrab;

    /// <summary>当前生效的上升速度（正数）。</summary>
    public static float SpeedUp =>
        Overrides.Contains(CornerBoostField.Speed)
            ? PlayerSpeedUp
            : MapValues.SpeedUp ?? PlayerSpeedUp;

    /// <summary>注入 Speed.Y 用的值（负数 = 向上）。</summary>
    public static float Speed => -SpeedUp;

    /// <summary>玩家设置（不含地图覆盖）对应的上升速度。</summary>
    public static float PlayerSpeedUp {
        get {
            switch (Settings.Preset) {
                case CornerBoostPreset.Strong:
                    return 160f;
                case CornerBoostPreset.VeryStrong:
                    return 200f;
                case CornerBoostPreset.ExtremelyStrong:
                    return 300f;
                case CornerBoostPreset.Mmtgd:
                    return 400f;
                case CornerBoostPreset.Custom:
                    return CustomSpeedUp();
                default:
                    return DefaultSpeedUp;
            }
        }
    }

    private static float CustomSpeedUp() {
        string raw = Settings.CustomSpeed;
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            && !float.IsNaN(value)
            && value > 0f) {
            return Settings.LimitCustomSpeed ? Math.Min(value, MaxCustomSpeed) : value;
        }

        // 填错就退回原版值，和以前的行为一致
        return DefaultSpeedUp;
    }

    public static bool IsPlayerOverridingSpeed => Overrides.Contains(CornerBoostField.Speed);

    /// <summary>滑块该停在哪一格：优先反映“当前生效速度”，这样地图改速度时能看出来。</summary>
    public static int PresetIndex {
        get {
            CornerBoostMapValues map = MapValues;
            if (map.SpeedUp.HasValue && !Overrides.Contains(CornerBoostField.Speed)) {
                return PresetIndexFor(map.SpeedUp.Value);
            }
            return (int)Settings.Preset;
        }
    }

    private static int PresetIndexFor(float speedUp) {
        if (Near(speedUp, DefaultSpeedUp)) {
            return (int)CornerBoostPreset.Vanilla;
        }
        if (Near(speedUp, 160f)) {
            return (int)CornerBoostPreset.Strong;
        }
        if (Near(speedUp, 200f)) {
            return (int)CornerBoostPreset.VeryStrong;
        }
        if (Near(speedUp, 300f)) {
            return (int)CornerBoostPreset.ExtremelyStrong;
        }
        if (Near(speedUp, 400f)) {
            return (int)CornerBoostPreset.Mmtgd;
        }
        return (int)CornerBoostPreset.Custom;
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < SpeedEpsilon;

    // ==================== 颜色（EVM 规则） ====================

    public static Color EnabledColor => BoolColor(MapValues.Enabled, Enabled, DefaultEnabled);

    public static Color RequireGrabColor => BoolColor(MapValues.RequireGrab, RequireGrab, DefaultRequireGrab);

    public static Color LimitCustomSpeedColor => BoolColor(null, Settings.LimitCustomSpeed, true);

    public static Color ShowPauseMenuSubMenuColor => BoolColor(null, Settings.ShowPauseMenuSubMenu, true);

    public static Color SpeedColor {
        get {
            float mapDefined = MapValues.SpeedUp ?? DefaultSpeedUp;
            if (!Near(SpeedUp, mapDefined)) {
                return Color.Goldenrod;
            }
            if (!Near(mapDefined, DefaultSpeedUp)) {
                return Color.DeepSkyBlue;
            }
            return Color.White;
        }
    }

    /// <summary>
    /// 自定义速度输入框的标色。玩家的自定义数字本身不标黄——它只是“速度”的一种表示，
    /// 真正该关注的是上面那条档位滑块。只有地图给速度赋了值、而玩家又把它改成别的数时才标黄。
    /// </summary>
    public static Color CustomSpeedColor {
        get {
            if (MapValues.SpeedUp is float mapSpeed) {
                if (Overrides.Contains(CornerBoostField.Speed) && !Near(PlayerSpeedUp, mapSpeed)) {
                    return Color.Goldenrod;
                }
                if (!Near(mapSpeed, DefaultSpeedUp)) {
                    return Color.DeepSkyBlue;
                }
            }
            return Color.White;
        }
    }

    private static Color BoolColor(bool? mapValue, bool current, bool defaultValue) {
        bool mapDefined = mapValue ?? defaultValue;
        if (current != mapDefined) {
            return Color.Goldenrod;
        }
        if (mapDefined != defaultValue) {
            return Color.DeepSkyBlue;
        }
        return Color.White;
    }

    // ==================== 菜单写值 ====================

    public static void SetEnabledFromMenu(bool value) {
        Settings.Enabled = value;
        bool? map = MapValues.Enabled;
        UpdateOverride(CornerBoostField.Enabled, map.HasValue, value == (map ?? value));
    }

    public static void SetRequireGrabFromMenu(bool value) {
        Settings.RequireGrab = value;
        bool? map = MapValues.RequireGrab;
        UpdateOverride(CornerBoostField.RequireGrab, map.HasValue, value == (map ?? value));
    }

    public static void SetPresetFromMenu(CornerBoostPreset preset) {
        Settings.Preset = preset;
        float? mapSpeed = MapValues.SpeedUp;
        UpdateOverride(CornerBoostField.Speed, mapSpeed.HasValue, Near(PlayerSpeedUp, mapSpeed ?? 0f));
    }

    public static void SetCustomSpeedFromMenu(string raw) {
        Settings.CustomSpeed = raw;
        Settings.Preset = CornerBoostPreset.Custom;
        float? mapSpeed = MapValues.SpeedUp;
        UpdateOverride(CornerBoostField.Speed, mapSpeed.HasValue, Near(PlayerSpeedUp, mapSpeed ?? 0f));
    }

    // 玩家选了和地图一样的值，就等于“回到地图设置”，不该再标黄
    private static void UpdateOverride(CornerBoostField field, bool hasMapValue, bool matchesMap) {
        if (hasMapValue && !matchesMap) {
            Overrides.Add(field);
        } else {
            Overrides.Remove(field);
        }
    }

    /// <summary>
    /// 恢复默认值（语义照 EVM 的 Reset to default）：
    /// 所有设置回到出厂值，并清掉“玩家改过”的标记，让地图触发器重新说了算。
    /// 只动本 Mod 自己的设置，不碰别的 Mod。
    /// </summary>
    public static void ResetToDefaults() {
        Settings.Enabled = DefaultEnabled;
        Settings.RequireGrab = DefaultRequireGrab;
        Settings.Preset = CornerBoostPreset.Vanilla;
        Settings.CustomSpeed = "120";
        Settings.LimitCustomSpeed = true;

        // 清掉覆盖标记：值已经回到默认，再留着标记会让控件一直显示成“玩家改过”
        Overrides.Clear();

        Logger.Log(LogLevel.Info, "VerticalCornerBoost", "Settings reset to defaults.");
    }

    // ==================== 地图触发器登记 ====================

    public static void AddEntry(Entity owner, Level level, CornerBoostMapValues values, bool revertOnLeave, bool revertOnDeath,
                                string flag = null, bool flagInverted = false) {
        entries.RemoveAll(e => e.Owner == owner);
        entries.Add(new MapEntry {
            Owner = owner,
            Level = level,
            Values = values,
            RevertOnLeave = revertOnLeave,
            RevertOnDeath = revertOnDeath,
            Flag = flag,
            FlagInverted = flagInverted,
        });
    }

    public static void RemoveEntry(Entity owner) => entries.RemoveAll(e => e.Owner == owner);

    /// <summary>玩家死亡时清掉“死亡后恢复”的触发器值（和 EVM 的 revertOnDeath 一致）。</summary>
    public static void RemoveEntriesOnDeath() => entries.RemoveAll(e => e.RevertOnDeath);

    /// <summary>清掉已经离开场景、或者属于别的房间的残留登记。</summary>
    public static void Prune(Level level) {
        entries.RemoveAll(e => e.Owner == null || e.Owner.Scene == null || e.Level != level);
    }

    public static void Clear() {
        entries.Clear();
        // 玩家覆盖存在 Session 里，属于存档状态，这里不清（只清运行时的地图条目）
    }
}
