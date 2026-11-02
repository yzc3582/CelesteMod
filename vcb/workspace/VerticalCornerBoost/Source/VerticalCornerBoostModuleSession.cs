using System.Collections.Generic;

namespace Celeste.Mod.VerticalCornerBoost;

public class VerticalCornerBoostModuleSession : EverestModuleSession {
    /// <summary>
    /// 玩家在游戏内自己改过的字段。和设置值一起存进存档会话，
    /// 这样“玩家改过”这个状态跨重启仍然成立（否则地图值会在下次进图时把玩家设置盖回去）。
    /// </summary>
    public HashSet<CornerBoostField> PlayerOverrides { get; set; } = new();
}
