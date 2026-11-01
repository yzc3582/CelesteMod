using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.VerticalCornerBoost;

/// <summary>
/// 只做一件事：在“贴墙上冲 + 墙顶就在头顶附近”时，把原版 ClimbHop 的纵向速度注入 Speed.Y。
/// 不碰 Speed.X、不读 StateMachine、不调用 ClimbCheck()。
/// </summary>
internal static class CornerBoostLogic {
    // 原版 Player.ClimbHopY（反编译 Celeste.dll 确认）。
    // 注意 ClimbHop() 里写的是 Speed.Y = Math.Min(Speed.Y, -120f)，是“封顶”而不是无条件赋值。
    public const float ClimbHopYSpeed = -120f;

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

        hasHoppedThisWall[self] = true;

        // 双重保险 1：原版这一帧已经给过翻越速度，不叠加
        if (Math.Abs(self.Speed.Y - ClimbHopYSpeed) < 0.1f) {
            return;
        }

        // 已经比翻越速度更快（CB / 超级墙跳），跟着原版的 Math.Min 语义走，不主动降速
        if (self.Speed.Y < ClimbHopYSpeed) {
            return;
        }

        self.Speed.Y = ClimbHopYSpeed;
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
