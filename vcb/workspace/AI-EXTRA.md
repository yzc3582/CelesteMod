# AI-EXTRA.md — 给后续 Agent 的上下文补充

> 本文件记录**已经反编译 `Celeste.dll` / 实测确认过**的事实、踩过的坑、当前待办与调参入口。
> `AGENTS.md` 是需求与约束的权威；本文件是"怎么实现才不会翻车"的补充。
> 冲突时以 `AGENTS.md` 为准，但请先看本文件的"反编译事实"一节——有些坑不看这里会重踩。

---

## 1. 反编译事实（已核实，可直接依赖）

反编译方式：`ilspycmd -t <类型> "<Celeste.txt 指向的目录>\Celeste.dll"`。
注意：**Everest 已被编译进 `Celeste.dll`**，游戏目录下没有 `Everest.dll`；
`Everest.*`、`TextMenuExt` 等类型都在 `Celeste.dll` 里。

| 事实 | 出处 |
| --- | --- |
| `private const float ClimbHopY = -120f;` | `Celeste.Player` |
| `ClimbHop()` 实际写的是 `Speed.Y = Math.Min(Speed.Y, -120f);`（**封顶**，非无条件赋值） | `Player.ClimbHop()` |
| 同理 `ClimbHopX = 100f`，但有墙时走 `hopWaitX / hopWaitXSpeed`，无墙才直接写 `Speed.X` | `Player.ClimbHop()` |
| 贴墙检测用 `CollideCheck<Solid>(Position + Vector2.UnitX * (float)Facing)`，即"判定盒整体平移 1 像素" | `Player.ClimbHop()` |
| `Gravity = 900f`，`MaxFall = 160f`，`JumpSpeed = -105f`，`WallJumpHSpeed = 130f` | `Celeste.Player` |
| `TextMenu.InnerContentMode` 只有 `OneColumn` / `TwoColumn`；原版暂停菜单是 `OneColumn` | `Celeste.TextMenu` |
| `TextMenu.Option<T>.Added()` 执行 `Container.InnerContent = InnerContentMode.TwoColumn;` | `TextMenu.Option<T>` |
| `TextMenu.OnOff(string label, bool on)`；`Option<T>.Change(Action<T>)` 只是 `OnValueChange = action` | `TextMenu.OnOff` |
| `ConfirmPressed()` 是 `Index = 1 - Index` 后回调新值，所以开关天然双向 | `TextMenu.Option<T>` |
| `TextMenuExt.SubMenu.Add(item)` 把 `item.Container` 指向**外层** TextMenu（不是自己），所以子菜单里的 Option 照样改外层 `InnerContent` | `Celeste.TextMenuExt.SubMenu` |
| `TextMenuExt.SubMenu(string label, bool enterOnSelect)`；子项渲染固定从 `position.X + ItemIndent` 开始 | 同上 |
| `Everest.Events.Player` 只有 `OnSpawn` / `OnDie`，**没有** player removal 事件 | `Everest.Events.Player` |
| `Everest.Events.Level.OnCreatePauseMenuButtons` 签名 `(Level level, TextMenu menu, bool minimal)` | `Everest.Events.Level` |
| 选项本地化键名 = `modoptions_` + **设置类名（小写、去掉结尾的 `settings`）** + `_` + **属性名小写**；标题把属性名换成 `title` | `EverestModule.CreateModMenuSection` |
| 因此本 Mod 的键前缀是 `modoptions_verticalcornerboostmodule_`（**不是** mod 名 `verticalcornerboost`） | 同上 |
| 原版 Dialog 文件名是 `English.txt`、`Simplified Chinese.txt` 等（带空格的完整语言名） | `Content/Dialog/` |

---

## 2. 暂停菜单对齐陷阱（务必别回退）

**现象**：加入本 Mod 的开关后，暂停菜单所有文字从居中变成靠左。

**原因**：见上表——任何 `TextMenu.Option` 被 `Added()` 时都会把 `Container.InnerContent` 设成 `TwoColumn`，
而 `TextMenu.Button` / `SubMenu` 的渲染在 `TwoColumn` 时 justify 从 `0.5` 变 `0`。

**已采用的做法**（`CornerBoostHooks.onCreatePauseMenuButtons`）：

1. 先记下 `menu.InnerContent`（原版是 `OneColumn`）；
2. 建一个 `TextMenuExt.SubMenu`，把 `TextMenu.OnOff` 放进子菜单；
3. `menu.Add(subMenu)` **之后**再把 `menu.InnerContent` 还原，并 `menu.RecalculateSize()`。

**为什么必须放在最后还原**：`SubMenu.Add(item)` 在 `SubMenu.Container == null` 时只把 item 放进
`delayedAddItems`，真正的 `item.Added()`（也就是改 `InnerContent` 的那一下）发生在子菜单被加进 menu 之后。

**不要**用"把原版按钮 `AlwaysCenter = true`"这种方案，那是在改原版控件。

---

## 3. 当前实现要点

| 文件 | 职责 |
| --- | --- |
| `VerticalCornerBoostModule.cs` | 模板自带的 Module，`Load/Unload` 只转发给 `CornerBoostHooks` |
| `CornerBoostHooks.cs` | 挂/摘 Hook：`On.Celeste.Player.Update`、`Everest.Events.Level.OnCreatePauseMenuButtons` |
| `CornerBoostLogic.cs` | 几何判定 + 注入 + 双重防叠加；**手感常量都在这里** |
| `VerticalCornerBoostModuleSettings.cs` | `Enabled`（是否启用）、`ShowPauseMenuSubMenu`（暂停菜单是否显示子菜单） |
| `Dialog/English.txt`、`Dialog/Simplified Chinese.txt` | 中英双语本地化 |

判定顺序（`CornerBoostLogic.Update`，顺序本身有含义，别随意调换）：

```text
Scene == null || Dead            -> 清标记，return
不满足墙角几何                    -> 清标记，return   （离开墙角才重置，字典条目因此天然有界，不需要额外清理）
Settings.Enabled == false        -> 清标记，return   （关掉开关要清，否则重开后旧标记会挡住本次墙角）
Speed.Y >= 0                     -> return           （只处理向上运动，且不标记，之后上冲仍能触发）
已标记                            -> return           （同一段墙角只处理一次）
标记 = true
|Speed.Y - (-120)| < 0.1         -> return           （原版这一帧已经给过了，不叠加）
Speed.Y < -120                   -> return           （CB/超级墙跳已更快，跟着原版 Math.Min 语义不降速）
Speed.Y = -120
```

几何判定用"判定盒整体平移"而不是点检测（`Entity.CollideCheck<T>(Vector2 at)`）：

- `ahead = Position + UnitX * Facing * WallGlueDistance`（默认 1f）→ 撞墙 = 贴墙
- `!CollideCheck<Solid>(ahead - UnitY * (Height + WallTopMargin))`（默认 2f）→ 墙顶高不过头顶 2 像素

---

## 4. 手感调参入口（开发者看这里）

**唯一的速度数值**：`CornerBoostLogic.cs` 里的

```csharp
public const float ClimbHopYSpeed = -120f;   // ← 想让翻越更"有力"就只改这里
```

为什么 -120 感觉"没力气"：`Speed.Y` 单位是像素/秒，`Gravity = 900`，
额外获得的高度约 `v² / (2g)`。

| 取值 | 额外上升高度（估算） |
| --- | --- |
| -120（原版 ClimbHop） | ≈ 8 px |
| -140 | ≈ 11 px |
| -160（= MaxFall，也是 SuperWallJump 的手感量级） | ≈ 14 px |
| -200 | ≈ 22 px |

配套可调项（同在 `CornerBoostLogic.cs`）：

- `WallGlueDistance`（默认 1f）：贴墙判定的平移量。调大 = 更容易判定到墙，但可能"离墙还挺远就触发"。
- `WallTopMargin`（默认 2f）：允许墙顶高过玩家头顶的像素数。实机没反应时**优先调这个**（改 4~8）。

**注意**：`ClimbHopYSpeed` 同时被用作"原版是否已生效"的比对基准和注入值，
改它不会破坏防叠加逻辑；但它一旦不再是 -120，`AGENTS.md` 的硬性约束 2
（"不改变翻越速度的数值"）就不再成立，需要同步更新 AGENTS.md，并明确这是"加强版"而非"还原版"。

---

## 5. 待办 / 状态

- [x] 编译通过（`dotnet publish -c Debug -p:CelestePrefix="<Celeste 目录>"` 实测无 error）
- [x] 暂停菜单开关双向可用（实机确认：关掉后不再触发）
- [x] 暂停菜单改为子菜单 + 还原 `InnerContent` 修居中（**实机待确认**）
- [x] Mod 选项新增 `ShowPauseMenuSubMenu`（**实机待确认**）
- [x] 中英双语本地化（`Dialog/`，键前缀 `modoptions_verticalcornerboostmodule_`）
- [ ] 抓墙被 Extended Variants 禁用时是否仍触发
- [ ] CB / 抓墙跳 / 反墙跳蹭墙角各场景实测
- [ ] 手感微调（`WallTopMargin`，必要时 `ClimbHopYSpeed`）

---

## 6. 构建备忘

- 工作区**没有** `Celeste.dll`，必须传 `CelestePrefix` 指向 `Celeste.txt` 里的目录：
  ```bat
  cd VerticalCornerBoost\Source
  dotnet publish -c Debug -p:CelestePrefix="D:\...\Steam\steamapps\common\Celeste"
  ```
- 产物落在 `VerticalCornerBoost\bin\VerticalCornerBoost.dll`（`everest.yaml` 里写的就是 `bin/...`）。
- 装进游戏：把整个 `VerticalCornerBoost` 文件夹复制到 `<Celeste>\Mods\`。
- 反编译临时文件不要写进工作区（用 `%TEMP%`）。
