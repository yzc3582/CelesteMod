using System;
using System.Collections.Generic;
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

    // 判定盒朝面向方向平移的像素数，和原版 ClimbHop()/ClimbCheck() 的“1 像素贴墙”一致
    private const float WallGlueDistance = 1f;

    // 允许墙顶高过玩家头顶的像素数（实机微调就从这里开始）
    private const float WallTopMargin = 2f;

    // “这一次贴墙上冲已经处理过”，防止同一段墙角被反复注入。
    // 用 Player 作 key 时必须保证条目会被清掉，否则死亡/换图后旧 Player 引用会被一直持有。
    private static readonly Dictionary<Player, bool> hasHoppedThisWall = new();

    /// <summary>
    /// 清掉已经离开场景的 Player 条目。虽然 Update 的各个分支都会 Remove(self)，
    /// 但“换图时旧 Player 直接被销毁、新 Player 是另一个对象”这种情况只能靠这里兜底。
    /// </summary>
    private static void PruneStale(Level level) {
        if (hasHoppedThisWall.Count == 0) {
            return;
        }

        // 正常情况下同时只会有 1 个 Player 在表里；这里只做小规模过滤，不需要复杂结构。
        List<Player> stale = null;
        foreach (Player player in hasHoppedThisWall.Keys) {
            if (player == null || player.Scene != level) {
                (stale ??= new List<Player>()).Add(player);
            }
        }

        if (stale != null) {
            foreach (Player player in stale) {
                hasHoppedThisWall.Remove(player);
            }
        }
    }

    public static void Update(Player self) {
        if (self.Scene is not Level level) {
            hasHoppedThisWall.Clear();
            return;
        }

        CornerBoostState.Prune(level);
        PruneStale(level);

        if (self.Dead) {
            // 死亡后触发器按 revertOnDeath 恢复，等重生再重新进触发器。
            // 注意：Dead 一旦置 true 就不会再变回 false（Player 会被销毁重建），
            // 所以这里每帧都会执行；RemoveEntriesOnDeath 是幂等的，代价可忽略。
            CornerBoostState.RemoveEntriesOnDeath();
            hasHoppedThisWall.Remove(self);
            return;
        }

        if (!IsInWallCorner(self)) {
            // 离开墙角区域 -> 本次事件结束，下一次贴墙上冲可以再触发
            hasHoppedThisWall.Remove(self);
            return;
        }

        if (!CornerBoostState.Enabled) {
            // 关掉开关时也清标记，免得重新打开后同一个墙角被“上一次”的记录挡住
            hasHoppedThisWall.Remove(self);
            return;
        }

        if (CornerBoostState.RequireGrab && !Input.GrabCheck) {
            // 只认“抓握状态”，不认“能不能抓墙”：即使玩家抓不到墙，只要抓着也照样触发。
            // Input.GrabCheck 已经把按住 / 反转 / 切换三种抓键模式都算好了；
            // EVM 的 True No Grabbing 会把 getter 强制返回 false，因此也会挡住这里。
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

        float target = CornerBoostState.Speed;
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
        CornerBoostState.Clear();
    }
}
