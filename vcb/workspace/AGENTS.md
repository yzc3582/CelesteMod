# AGENTS.md — Vertical Corner Boost (纵向抓角加速)

## 项目身份

- **中文名**: 纵向抓角加速
- **English**: Vertical Corner Boost
- **代号 / 内部命名**: `VerticalCornerBoost`（旧分支曾用 `Corner Hop Assist`，已弃用）
- **类型**: Celeste Everest 代码 Mod，主要注入玩家移动逻辑；另提供一个地图触发器
  `VerticalCornerBoost/CornerBoostTrigger`（见下文"地图触发器"），不添加其它实体。
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

默认要求**抓握**才触发（`Settings.RequireGrab`，默认 `true`）：判定读的是 `Input.GrabCheck`
（`GrabModes` 的按住 / 反转 / 切换三种模式都已由它处理好），而不是"能不能抓墙"。
所以即使玩家抓不到墙（例如某个异变禁用了抓墙），只要处于抓握状态仍然触发；
Extended Variants 的 `True No Grabbing` 会把 `GrabCheck` 强制成 `false`，从而自然挡住触发。

Mod 默认**不启用**（`Settings.Enabled = false`），玩家在 Mod 选项或暂停菜单里打开。

## 硬性行为约束（不可违反）

1. **只补纵向速度**：不主动修改 `Speed.X`，保留玩家原有水平动量。
2. **默认档位不改变翻越速度的数值**：`Vanilla` 档必须使用原版 `ClimbHop` 的纵向速度常量
   （`-120f`，从 `Celeste.dll` 反编译确认，不要沿用 `SuperWallJump` 的 `-160f`）。
   其余档位（`Strong -160` / `VeryStrong -200` / `Custom` 自定义正数输入）是可选的加强档，
   不要求与原版等值；但**默认值必须是原版值**，且注入值与防叠加比对基准必须同源
   （统一取 `CornerBoostState.Speed`；不要写死 -120，也不要在两个文件里各算一份速度）。
3. **不与原版 ClimbHop 叠加**：如果原版已经在同一事件中赋予翻越速度，本 Mod 跳过注入。
4. **可开关**：
   - 设置项 `Settings.Enabled`，**默认 `false`**。
   - **同时**在暂停界面通过 `Everest.Events.Level.OnCreatePauseMenuButtons` 插入子菜单项，绑定同一个 `Settings.Enabled`。
   - 暂停菜单子菜单与 Mod 选项菜单共用 `CornerBoostUI`，并按 Extended Variants 的三色语义显示：
     金 = 玩家自己在游戏内改过、蓝 = 地图触发器改过、白 = 默认值。
5. **不依赖攀爬状态**：即使拓展异变禁用了抓墙/抓墙跳，机制仍然生效（因为只读取 `Speed`、`Input.GrabCheck` 和碰撞几何）。
   `RequireGrab` 判断的是“抓握状态”，不是“能否抓墙”。

## 核心 Hook

- **位置**: `On.Celeste.Player.Update`（On Hook，原版逻辑执行**之后**）。
- **几何判定**（近似，需实机微调像素距离）：
  - 玩家朝向方向前方短距离内有 `Solid`。
  - 前方上方一定高度内**没有** `Solid`（表示墙顶就在附近）。
  - `self.Speed.Y < 0`（正在向上运动）。
  - （提示：“前方短距离内有 `Solid`”可参考抓墙跳的距离）
- **注入**：`self.Speed.Y = CornerBoostState.Speed`（默认等于原版 ClimbHop 的 `-120f`，可被速度档位或地图触发器改掉）。
- **防重复**：用 `Dictionary<Player, bool>` 或附加 `Component` 标记“本次墙角事件已处理”；离开墙角区域（不再满足几何条件）时重置。

判定顺序（`CornerBoostLogic.Update`，顺序本身有含义）：
`Scene 不是 Level` → 清标记；`Prune` 掉别的房间的地图条目；`Dead` → 移除 `revertOnDeath` 条目并清标记；
`不满足墙角几何` → 清标记；`!CornerBoostState.Enabled` → 清标记；
`RequireGrab && !Input.GrabCheck` → 清标记；`Speed.Y >= 0` → 直接返回（不标记）；
`已标记` → 返回；标记；`|Speed.Y - target| < 0.1` → 返回；`Speed.Y < target` → 返回；否则注入目标速度。

## 暂停菜单与 Mod 选项

- 订阅 `Everest.Events.Level.OnCreatePauseMenuButtons`；`minimal == false` 且 `Settings.ShowPauseMenuSubMenu` 时插入子菜单。
- 子菜单和 Mod 选项菜单都通过 `CornerBoostUI` 构建控件（`ColoredOnOff` / `ColoredSlider` / `ColoredButton`），
  颜色每帧按 `CornerBoostState` 现算：金 = 玩家在游戏内改过、蓝 = 地图触发器改过、白 = 默认。
- Mod 选项菜单通过设置类里的 `public void Create<属性名>Entry(TextMenu, bool)` 覆盖 Everest 的默认项
  （Everest 一旦发现子类重写了 `CreateModMenuSection` 或存在这些方法，就不再自动生成对应控件）。
- `CustomSpeed` 用 `OuiModOptionString` 输入，`inGame` 时按钮禁用（与 Everest 默认行为一致）。

## 地图触发器（Helper 式）

- 实体名 `VerticalCornerBoost/CornerBoostTrigger`（`[CustomEntity]`，Loenn 定义在 `Loenn/triggers/cornerBoostTrigger.lua`）。
- 字段：`enabled` / `requireGrab`（`leave` / `enable` / `disable`，默认 `leave` 即不改）、
  `speed`（正数 = 向上速度 px/s，`0` = 不改）、`revertOnLeave`（默认 true）、`revertOnDeath`（默认 true）、
  `coversScreen`（默认 false，为 true 时覆盖整间房间）、
  `flag` / `flagInverted`（flag 非空时，只有该 flag 成立（可反转）这条覆盖才生效，每帧现算）。
- 覆盖值**不写入 Settings**，只存在于运行时（`CornerBoostState` 的条目表），因此不会污染存档；
  玩家在游戏内改值时该字段转为“玩家覆盖”，优先级高于地图，改回与地图相同的值则退回地图值（重新变蓝）。
- 语义借鉴 Extended Variants 的触发器，但**不依赖 EVM**：不引用其程序集、`everest.yaml` 不加其依赖。

## 防叠加策略（双重保险）

1. **速度值检测**：注入前检查 `Math.Abs(self.Speed.Y - ClimbHopYSpeed) < 0.1f`，若是则视为原版已生效，跳过。
2. **事件标志**：`hasHoppedThisWall[player]` 在本次“贴墙上升 → 离开墙角”的完整生命周期内只允许注入一次，无论原版是否在同一帧触发。

## 暂停菜单开关实现

见上文“暂停菜单与 Mod 选项”：子菜单通过 `CornerBoostUI.AddPauseMenuItems` 构建，
`Change` 回调统一走 `CornerBoostState.SetXxxFromMenu`，与 Mod 选项菜单共享同一设置源。

### 对齐陷阱（别回退）

`TextMenu.Option<T>.Added()` 会把 `Container.InnerContent` 改成 `TwoColumn`，导致暂停菜单所有文字靠左。
所以必须先记下原值，`menu.Add(subMenu)` **之后**再还原并 `RecalculateSize()`。

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
- [ ] 三种抓键模式（按住 / 反转 / 切换）下 `RequireGrab` 行为都正确。
- [ ] EVM 打开 `True No Grabbing` 后不再触发；只禁用抓墙时仍然触发。
- [ ] 地图触发器四种组合（enable/disable × revertOnLeave 开关）行为符合预期，离开房间不残留。
- [ ] 三色显示：默认白、地图改蓝、玩家改金，改回地图值重新变蓝。