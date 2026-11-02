using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.VerticalCornerBoost;

/// <summary>
/// 地图触发器：在玩家进入时临时改掉本 Mod 的启用状态 / 抓握要求 / 上升速度。
/// 参考 Extended Variant Mode 的触发器语义：
///   - 默认离开或死亡时恢复；
///   - 值为 Leave 表示这个字段不动；
///   - speed = 0 表示不动，正数表示向上速度（px/s）；
///   - coversScreen = true 时覆盖整个房间。
/// 实体名：VerticalCornerBoost/CornerBoostTrigger
/// </summary>
[CustomEntity("VerticalCornerBoost/CornerBoostTrigger")]
public sealed class CornerBoostTrigger : Trigger {
    public enum Mode {
        Leave,
        Enable,
        Disable,
    }

    private readonly Mode enabled;
    private readonly Mode requireGrab;
    private readonly float speed;
    private readonly bool revertOnLeave;
    private readonly bool revertOnDeath;
    private readonly bool coversScreen;

    public CornerBoostTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        enabled = ParseMode(data.Attr("enabled", "leave"));
        requireGrab = ParseMode(data.Attr("requireGrab", "leave"));
        speed = data.Float("speed", 0f);
        revertOnLeave = data.Bool("revertOnLeave", true);
        revertOnDeath = data.Bool("revertOnDeath", true);
        coversScreen = data.Bool("coversScreen", false);
    }

    // Loenn / Ahorn 写的是小写枚举名（leave / enable / disable），这里忽略大小写
    private static Mode ParseMode(string raw) {
        return Enum.TryParse(raw, true, out Mode mode) ? mode : Mode.Leave;
    }

    public override void Added(Scene scene) {
        base.Added(scene);
        if (coversScreen && scene is Level level) {
            Collider = new Hitbox(level.Bounds.Width, level.Bounds.Height, level.Bounds.X - X, level.Bounds.Y - Y);
        }
    }

    public override void OnEnter(Player player) {
        base.OnEnter(player);
        if (Scene is not Level level) {
            return;
        }

        CornerBoostMapValues values = default;
        if (enabled != Mode.Leave) {
            values.Enabled = enabled == Mode.Enable;
        }
        if (requireGrab != Mode.Leave) {
            values.RequireGrab = requireGrab == Mode.Enable;
        }
        if (speed > 0f) {
            values.SpeedUp = Math.Abs(speed);
        }

        if (values.IsEmpty) {
            return;
        }

        CornerBoostState.AddEntry(this, level, values, revertOnLeave, revertOnDeath);
    }

    public override void OnLeave(Player player) {
        base.OnLeave(player);
        if (revertOnLeave) {
            CornerBoostState.RemoveEntry(this);
        }
    }
}
