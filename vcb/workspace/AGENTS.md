# AGENTS.md — Vertical Corner Boost (纵向抓角加速)

## 项目身份

- **中文名**: 纵向抓角加速
- **English**: Vertical Corner Boost
- **代号 / 内部命名**: `VerticalCornerBoost`（旧分支曾用 `Corner Hop Assist`，已弃用）
- **类型**: Celeste Everest 代码 Mod，**不添加任何新实体**，只注入玩家移动逻辑。
- **目标游戏**: Celeste (PC) + Everest（注：路径见目录下 `Celeste.txt`）
- 更多额外内容见目录下 `EXTRA.md`
- **AI 代理开工前必读 [`AI-EXTRA.md`](AI-EXTRA.md)**：已反编译验证的 API 事实、
  暂停菜单 `InnerContent` 对齐陷阱、本地化键名规则（前缀基于设置类名而非 mod 名）、
  手感调参入口、待办与验证状态。

## 一句话机制

当玩家在**任意来源**的向上运动中（爬墙、CB、抓墙跳、反墙跳蹭墙角等），
空间上处于“**贴着墙且墙的顶端就在头顶附近**”的墙角状态时，
自动向 `Player.Speed.Y` 注入原版 `ClimbHop` 的纵向速度。
**不依赖 `StateMachine`，不依赖 `ClimbCheck()`，不依赖抓墙/抓墙跳是否被 ExtendedVarients 禁用。**

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
  - （提示：“前方短距离内有 `Solid`”可参考抓墙跳的距离）
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

## 验证清单（部分可由人类验证）

- [ ] 编译通过，并在游戏中加载 Mod 无报错。
- [ ] 爬墙到墙角自然翻越时，纵向速度与原版 ClimbHop 一致（不叠加、不改变数值）。
- [ ] CB / 抓墙跳 / 反墙跳蹭墙角等场景能触发。
- [ ] 拓展异变禁用抓墙/抓墙跳后，机制仍然生效。
- [ ] 暂停菜单和 Mod 选项菜单中的开关均能即时启用/禁用机制。
- [ ] 水平动量未被主动修改。
- [ ] 无重复触发导致的速度异常或内存泄漏（字典在玩家移除时清理）。