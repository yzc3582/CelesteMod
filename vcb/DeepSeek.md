下面按你的四个要求逐一说明，最后把完整计划整理进 `AGENTS.md` 代码块。

---

## 1. 把开关放进暂停界面（参考“拓展异变”的做法）

“拓展异变”（Extended Variant Mode）的暂停菜单选项，本质上是通过 Everest 的 **`Everest.Events.Level.OnCreatePauseMenuButtons`** 事件挂载的。这个事件在每次 `Level.Pause` 时触发，你可以往传入的 `TextMenu` 里插入自己的开关项。

**在 `Module.cs` 中订阅**：

```csharp
public override void Load() {
    Everest.Events.Level.OnCreatePauseMenuButtons += Level_OnCreatePauseMenuButtons;
}
public override void Unload() {
    Everest.Events.Level.OnCreatePauseMenuButtons -= Level_OnCreatePauseMenuButtons;
}

private void Level_OnCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
    // 只在非 minimal 的完整暂停菜单里显示（避免 TAS 等场景干扰）
    if (minimal) return;

    // 创建一个“开关”项，绑定到你的 Mod Settings
    var toggle = new TextMenu.OnOff(
        "Vertical Corner Boost",        // 显示名称
        MyMechanicModule.Settings.Enabled // 当前值
    );
    toggle.Change(value => {
        MyMechanicModule.Settings.Enabled = value; // 写入设置
    });

    // 插入到暂停菜单中。具体插入位置可以用 menu.Insert(index, toggle)，
    // 或者 menu.Add(toggle) 追加到末尾。
    menu.Add(toggle);
}
```

这样，暂停界面里就会出现一个可直接切换的开关，和“拓展异变”的交互方式一致。注意：`OnOff` 只是最简形式；如果你需要“多档位”或“数值滑块”，可以换成 `TextMenu.Slider` 或自定义 `TextMenu.Item`，但“开关”用 `OnOff` 完全足够。

> **与 Mod 选项菜单的关系**：你之前在 `MyModuleSettings` 中声明的 `Enabled` 属性，Everest 会自动在“Mod 选项”里生成勾选框。暂停菜单里的这个 `OnOff` 和它是**同一个数据源**（`Settings.Enabled`），两处修改互相同步。

---

## 2. 让 AI “如虎添翼”的 AGENTS.md 类资源

**首推：`universal-modder`（rehan-remade/universal-modder，MIT 许可）**。它是目前最直接服务于“AI 代理制作游戏 Mod”的工具包，**明确包含 .NET/XNA 引擎 playbook，Celeste 在其中**，并且指令就写在 `AGENTS.md` 里，`CLAUDE.md` 和 `GEMINI.md` 都指向它。它提供：

- **12 个引擎 playbook**，其中 .NET/XNA 分支覆盖 Celeste，配套 ILSpy 反编译流程。
- **MCP 配置**（`.mcp.json`），让 Claude Code / Cursor / Codex 等代理直接调用工具链。
- **硬性安全规则**：拒绝联网、反作弊、多人作弊、DRM 绕过，只做单机离线 Mod。

安装方式（官方 README 路径）：

```bash
# 需要 Python 3.10+、ffmpeg、uv（或 pipx）
git clone https://github.com/rehan-remade/universal-modder.git
cd universal-modder
# 按照 README 安装 um CLI 和 MCP 配置
um scan                    # 自动发现 Celeste，识别引擎和版本
um kb search celeste       # 在知识库中检索 Celeste 相关 field notes
```

**次选：Everest 官方 `ExampleMod`**。它不是 AGENTS.md，但作为“代码 Mod 的功能百科全书”，AI 可以把它当作参考语料来检索 `EverestModule`、Hook、`TextMenu`、`Session` 等标准写法。

**补充：`writing-for-agents` 技能**。如果你希望自己写的 `AGENTS.md` 对 AI 更“友好”（上下文负载控制、指针优化），这个技能提供了编写 agent 消费文档的规范。

---

## 3. 本地开发需要安装的东西

| 组件                                               | 版本 / 说明                                                                                                    |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| **Celeste**                                        | PC 版（Steam 或其他），你已经拥有                                                                              |
| **Everest**                                        | 通过 Olympus 或 Everest 官网安装到 Celeste 目录                                                                |
| **.NET SDK**                                       | **9.0.0 或更高**（Everest 当前构建要求）                                                                       |
| **.NET 8.0 运行时**                                | 构建过程需要                                                                                                   |
| **Visual Studio**（Windows）或 **VS Code / Rider** | 用来编辑 C# 和调试                                                                                             |
| **ILSpy 或 dnSpy**                                 | 反编译 `Celeste.dll`，确认 `Player.ClimbHop` 的准确速度常量、`ClimbUpdate` 的 IL 结构                          |
| **CelesteMod.Templates**（可选但推荐）             | `dotnet new install CelesteMod.Templates`，用 `dotnet new celestemod` 快速生成项目骨架，跳过手动配置 `.csproj` |

**关键提醒**：Everest 官方 README 强调 **“publish 而非单纯 build”**，否则依赖 DLL 不会被复制到输出目录。在 Visual Studio 中使用 **Publish**，或在 CLI 中使用 `dotnet publish`，然后把 publish 目录的内容复制到 Celeste 的 `Mods/你的Mod名/` 下。

> 如果你的 Everest 版本较旧，可能仍要求 .NET SDK 7.0.200+ 和 .NET 6.0 运行时；以你本地 Everest 的 README 为准。但当前主线已转向 .NET 8/9。

---

## 4. 整理进 `AGENTS.md` 的完整计划

以下代码块可以直接保存为项目根目录的 `AGENTS.md`。它同时覆盖了机制逻辑、暂停菜单开关、防叠加策略、命名，以及给 AI 代理的操作约束。

```markdown
# AGENTS.md — Vertical Corner Boost (纵向抓角加速)

## 项目身份
- **中文名**: 纵向抓角加速
- **English**: Vertical Corner Boost
- **代号 / 内部命名**: `VerticalCornerBoost`（旧分支曾用 `Corner Hop Assist`，已弃用）
- **类型**: Celeste Everest 代码 Mod，**不添加任何新实体**，只注入玩家移动逻辑。
- **目标游戏**: Celeste (PC) + Everest

## 一句话机制
当玩家在**任意来源**的向上运动中（爬墙、CB、抓墙跳、反墙跳蹭墙角等），
空间上处于“**贴着墙且墙的顶端就在头顶附近**”的墙角状态时，
自动向 `Player.Speed.Y` 注入原版 `ClimbHop` 的纵向速度。
**不依赖 `StateMachine`，不依赖 `ClimbCheck()`，不依赖抓墙/抓墙跳是否被变体禁用。**

## 硬性行为约束（不可违反）
1. **只补纵向速度**：不主动修改 `Speed.X`，保留玩家原有水平动量。
2. **不改变翻越速度的数值**：使用原版 `ClimbHop` 的纵向速度常量（从 `Celeste.dll` 反编译确认，不要沿用 `SuperWallJump` 的 `-160f`）。
3. **不与原版 ClimbHop 叠加**：如果原版已经在同一事件中赋予翻越速度，本 Mod 跳过注入。
4. **可开关**：
   - 设置项 `Settings.Enabled`（`EverestModuleSettings` 自动生成 Mod 选项菜单勾选框）。
   - **同时**在暂停界面通过 `Everest.Events.Level.OnCreatePauseMenuButtons` 插入 `TextMenu.OnOff`，绑定同一个 `Settings.Enabled`。
5. **不依赖攀爬状态**：即使拓展异变禁用了抓墙/抓墙跳，机制仍然生效（因为只读取 `Speed` 和碰撞几何）。

## 核心 Hook
- **位置**: `On.Celeste.Player.Update`（On Hook，原版逻辑执行**之后**）。
- **几何判定**（近似，需实机微调像素距离）：
  - 玩家朝向方向前方短距离内有 `Solid`。
  - 前方上方一定高度内**没有** `Solid`（表示墙顶就在附近）。
  - `self.Speed.Y < 0`（正在向上运动）。
- **注入**：`self.Speed.Y = <原版 ClimbHop 纵向速度>`。
- **防重复**：用 `Dictionary<Player, bool>` 或附加 `Component` 标记“本次墙角事件已处理”；离开墙角区域（不再满足几何条件）时重置。

## 防叠加策略（双重保险）
1. **速度值检测**：注入前检查 `Math.Abs(self.Speed.Y - ClimbHopYSpeed) < 0.1f`，若是则视为原版已生效，跳过。
2. **事件标志**：`hasHoppedThisWall[player]` 在本次“贴墙上升 → 离开墙角”的完整生命周期内只允许注入一次，无论原版是否在同一帧触发。

## 暂停菜单开关实现
- 订阅 `Everest.Events.Level.OnCreatePauseMenuButtons`。
- 在 `minimal == false` 时创建 `TextMenu.OnOff("Vertical Corner Boost", Settings.Enabled)`。
- `Change` 回调写入 `Settings.Enabled`，与 Mod 选项菜单共享同一设置源。
- 在 `Unload` 中取消订阅。

## 开发环境
- Celeste PC + Everest（通过 Olympus 安装）。
- **.NET SDK ≥ 9.0.0** + **.NET 8.0 运行时**（以本地 Everest README 为准）。
- Visual Studio / VS Code / Rider。
- **ILSpy 或 dnSpy**（用于确认 `Player.ClimbHop` 的纵向速度常量和 `ClimbUpdate` 的 IL 结构）。
- 可选：`CelesteMod.Templates`（`dotnet new celestemod`）。
- **构建时必须使用 `publish` 而非 `build`**，否则依赖 DLL 不会复制到输出目录。

## AI 代理可用资源
- **universal-modder**（https://github.com/rehan-remade/universal-modder）：
  读取 AGENTS.md，提供 .NET/XNA（含 Celeste）playbook、ILSpy 流程、MCP 配置。
- **EverestAPI/ExampleMod**：作为标准 Everest 代码模式的参考语料。
- **Everest Wiki / `#modding_help`（Discord）**：API 和 Hook 问题的最终依据。

## 验证清单
- [ ] 编译通过，并在游戏中加载 Mod 无报错。
- [ ] 爬墙到墙角自然翻越时，纵向速度与原版 ClimbHop 一致（不叠加、不改变数值）。
- [ ] CB / 抓墙跳 / 反墙跳蹭墙角等场景能触发。
- [ ] 拓展异变禁用抓墙/抓墙跳后，机制仍然生效。
- [ ] 暂停菜单和 Mod 选项菜单中的开关均能即时启用/禁用机制。
- [ ] 水平动量未被主动修改。
- [ ] 无重复触发导致的速度异常或内存泄漏（字典在玩家移除时清理）。
```

如果你愿意，下一步我可以帮你把 `ClimbHop` 的准确纵向速度常量从反编译结果中定位出来，或者写出 `OnCreatePauseMenuButtons` 中插入 `TextMenu.OnOff` 的完整可编译版本。
