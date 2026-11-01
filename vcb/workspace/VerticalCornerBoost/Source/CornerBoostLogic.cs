using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.VerticalCornerBoost;

/// <summary>
/// 只做一件事：在“贴墙上冲 + 墙顶就在头顶附近”时，把原版 ClimbHop 的纵向速度注入 Speed.Y。
/// 不碰 Speed.X、不读 StateMachine、不调用 ClimbCheck()。
/// </summary>
internal static class CornerBoostLogic {
    // 原版 ClimbHop 的纵向速度：运行时从 Celeste.dll 的 Player.ClimbHopY 常量读取，
    // 读不到才退回 -120f。注意它是 private const，只能反射拿——直接写 Player.ClimbHopY
    // 会被编译器在编译期内联成字面量，反而读不到运行中这份 DLL 的值。
    // 另外 ClimbHop() 里写的是 Speed.Y = Math.Min(Speed.Y, -120f)，是“封顶”而不是无条件赋值。
    public static readonly float ClimbHopYSpeed = ReadVanillaClimbHopY();

    private const float FallbackClimbHopY = -120f;

    private static float ReadVanillaClimbHopY() {
        try {
            FieldInfo field = typeof(Player).GetField("ClimbHopY", BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null) {
                // const 字段走 GetRawConstantValue()，万一哪天变成 static readonly 也能兜住
                object raw = field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null);
                if (raw is float value && value < 0f) {
                    return value;
                }
            }
        } catch {
            // 反射失败不是致命问题，用已知值继续
        }

        return FallbackClimbHopY;
    }

    // 自定义档位的上限，防止手滑输入很大的数把玩家弹飞
    private const float MaxCustomSpeed = 1000f;

    /// <summary>
    /// 当前档位对应的纵向速度（负值 = 向上）。
    /// 注入值和“是否已被原版生效”的比对基准都必须用它：写死 -120 的话，换档后判定就错了。
    /// </summary>
    public static float TargetSpeed {
        get {
            switch (VerticalCornerBoostModule.Settings.Preset) {
                case CornerBoostPreset.Strong:
                    return -160f;
                case CornerBoostPreset.VeryStrong:
                    return -200f;
                case CornerBoostPreset.Custom:
                    return -CustomSpeedUp();
                default:
                    return ClimbHopYSpeed;
            }
        }
    }

    // 玩家填正数，这里取绝对值再转成向上速度；填不合法就退回原版值
    private static float CustomSpeedUp() {
        string raw = VerticalCornerBoostModule.Settings.CustomSpeed;
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            && !float.IsNaN(value)
            && value > 0f) {
            return VerticalCornerBoostModule.Settings.LimitCustomSpeed ? Math.Min(value, MaxCustomSpeed) : value;
        }

        return -ClimbHopYSpeed;
    }

    // 判定盒朝面向方向平移的像素数，和原版 ClimbHop()/ClimbCheck() 的“1 像素贴墙”一致
    private const float WallGlueDistance = 1f;

    // 允许墙顶高过玩家头顶的像素数（实机微调就从这里开始）
    private const float WallTopMargin = 2f;

    // “这一次贴墙上冲已经处理过”，防止同一段墙角被反复注入
    private static readonly Dictionary<Player, bool> hasHoppedThisWall = new();

    public static void Update(Player self) {
        if (self.Scene == null || self.Dead) {
            hasHoppedThisWall.Remove(self);
            return;
        }

        if (!IsInWallCorner(self)) {
            // 离开墙角区域 -> 本次事件结束，下一次贴墙上冲可以再触发
            hasHoppedThisWall.Remove(self);
            return;
        }

        if (!VerticalCornerBoostModule.Settings.Enabled) {
            // 关掉开关时也清标记，免得重新打开后同一个墙角被“上一次”的记录挡住
            hasHoppedThisWall.Remove(self);
            return;
        }

        if (self.Speed.Y >= 0f) {
            // 只处理“正在向上运动”，下落中不标记，之后上冲仍有机会触发
            return;
        }

        if (hasHoppedThisWall.ContainsKey(self)) {
            return;
        }

        float target = TargetSpeed;
        hasHoppedThisWall[self] = true;

        // 双重保险 1：速度已经是目标值，视为原版（或别的来源）这一帧已经给过，不叠加
        if (Math.Abs(self.Speed.Y - target) < 0.1f) {
            return;
        }

        // 已经比目标速度更快（CB / 超级墙跳），跟着原版的 Math.Min 语义走，不主动降速
        if (self.Speed.Y < target) {
            return;
        }

        self.Speed.Y = target;
    }

    private static bool IsInWallCorner(Player self) {
        // 把整个判定盒朝面向方向推 1 像素：推完还撞墙，说明人就贴着这面墙
        Vector2 ahead = self.Position + Vector2.UnitX * (float)self.Facing * WallGlueDistance;
        if (!self.CollideCheck<Solid>(ahead)) {
            return false;
        }

        // 再把这个判定盒上移 (身高 + 余量)：如果上方还有实心块，说明墙还往上长，不是墙角
        return !self.CollideCheck<Solid>(ahead - Vector2.UnitY * (self.Height + WallTopMargin));
    }

    public static void Clear() {
        hasHoppedThisWall.Clear();
    }
}
