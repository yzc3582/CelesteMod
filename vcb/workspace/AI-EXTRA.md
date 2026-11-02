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
| `private const float ClimbHopY = -120f;`（literal const；运行时用 `FieldInfo.GetRawConstantValue()` 反射读取，直接写 `Player.ClimbHopY` 会被编译期内联） | `Celeste.Player` |
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
| `enum` 属性 → `TextMenu.Slider`，**按 enum 数值排序**；每项键为 `modoptions_<前缀>_<属性小写>_<枚举值小写>` | `EverestModule.CreateModMenuSection` |
| `string` 属性 → `TextMenu.Button` 打开 `OuiModOptionString` 文本输入；**inGame（暂停菜单里）该控件会被禁用** | 同上 |
| `[SettingMaxLength(n)]` 默认 12、`[SettingMinLength(n)]` 默认 1（string 属性） | 同上 |
| `Celeste.Input.GrabCheck` 是 `public static bool` 属性，getter 为 `Settings.Instance.GrabMode switch { GrabModes.Invert => !Grab.Check, GrabModes.Toggle => grabToggle, _ => Grab.Check }`；`enum GrabModes { Hold, Invert, Toggle }`；`Input.UpdateGrab()` 在 Toggle 模式下按一下翻转 `grabToggle`，`ResetGrab()` 置 false | `Celeste.Input` |
| 因此**直接读 `Input.GrabCheck` 就能同时正确处理三种抓键模式**，不要自己去读 `Input.Grab.Check` | 同上 |
| EVM 的 `TrueNoGrabbing` 是用 `MonoMod.RuntimeDetour.Hook` 钩 `Input.GrabCheck` 的 getter 并直接 `return false`；我们只要读 `Input.GrabCheck` 就自动尊重它 | ExtendedVariantMode 源码 `Variants/TrueNoGrabbing.cs` |
| `EverestModule.CreateModMenuSection` 会先找设置类上的 `public void Create<属性名>Entry(TextMenu menu, bool inGame)`，找到就调用它并跳过默认控件；子类重写 `CreateModMenuSection` 时 Everest 只加 header | `Celeste.Mod.EverestModule` |
| `TextMenu.Option<T>` 有 `public Color UnselectedColor = Color.White;`，Render 里 `highlighted ? Container.HighlightColor : UnselectedColor`；子类在 `Render` 里先赋值再 `base.Render` 即可动态标色 | `Celeste.TextMenu.Option<T>` |
| `TextMenu.Button` **没有**颜色字段，要靠重写 `Render` 自己画（注意 `InnerContent == TwoColumn && !AlwaysCenter` 时靠左） | `Celeste.TextMenu.Button` |
| `Celeste.Mod.UI.OuiModOptionString.Init<T>(string value, Action<string> onValueChange, int maxValueLength, int minValueLength)` | `Celeste.Mod.UI.OuiModOptionString` |
| `Celeste.Mod.Entities.CustomEntityAttribute` 在 `Celeste.Mod.Entities` 命名空间（**不是** `Celeste.Mod`），要单独 `using` | 同上 |
| `Celeste.Trigger`：`public Trigger(EntityData data, Vector2 offset)` 里 `Collider = new Hitbox(data.Width, data.Height)`；`public bool PlayerIsInside { get; private set; }`；`OnEnter/OnStay/OnLeave(Player)` 都是 `virtual` | `Celeste.Trigger` |

---

## 2. 暂停菜单：为什么是独立界面而不是子菜单

**现状**：暂停菜单里只放一个入口按钮（插在“选项”之前），按进去是**独立界面** `CornerBoostMenu`，
不是 `TextMenuExt.SubMenu` 那种下拉式子菜单。

**为什么弃用下拉式子菜单**（两个坑都实机踩过）：

1. `TextMenu.Option<T>.Added()` 会把 `Container.InnerContent` 改成 `TwoColumn`，
   导致暂停菜单所有原版按钮从居中变靠左（要靠 `menu.Add(subMenu)` 之后再还原 `InnerContent` 来救）。
2. 更致命的是**叠字**：子菜单把所有行挤在固定宽度里，行数一多，各行的 label 和左右箭头就互相压在一起，
   完全没法看。

**独立界面的做法**（照 EVM 的 `AbstractSubmenu`）：

1. 按钮 `Pressed` 里：记下 `level.PauseMainMenuOpen`，`parentMenu.RemoveSelf()`，把它置 `false`；
2. 新建一个普通 `TextMenu`，加 `Header` + 选项 + “返回”，`level.Add(menu)`；
3. `menu.OnESC` / `menu.OnCancel` / “返回”按钮 → `menu.Close()`，`level.Add(parentMenu)` 放回去，
   恢复 `PauseMainMenuOpen`；
4. `menu.OnPause` → 直接调 `level.Unpause()`（它会关菜单、播音效、重置 freeze timer），
   不要自己再关一次，否则音效重复。

这样用的是原版 `TextMenu` 的完整布局：有 header、超长会滚动、选择和返回行为都和原版一致。

---

## 2b. 恢复默认值（语义照 EVM）

界面末尾一个 `TextMenu.Button`，按下调 `CornerBoostState.ResetToDefaults()`：
把 `Enabled` / `RequireGrab` / `Preset` / `CustomSpeed` / `LimitCustomSpeed` 写回出厂值，
并 `Overrides.Clear()` 清掉“玩家改过”的标记——否则值虽然回到默认，控件仍会因为残留标记显示成金色，
而且地图触发器也没法重新生效。只动本 Mod 自己的设置，不碰别的 Mod。

---

## 3. 当前实现要点

| 文件 | 职责 |
| --- | --- |
| `VerticalCornerBoostModule.cs` | 模板自带的 Module，`Load/Unload` 只转发给 `CornerBoostHooks` |
| `CornerBoostHooks.cs` | 挂/摘 Hook：`On.Celeste.Player.Update`、`Everest.Events.Level.OnCreatePauseMenuButtons` |
| `CornerBoostLogic.cs` | 几何判定 + 注入 + 双重防叠加；**几何常量在这里** |
| `CornerBoostState.cs` | 生效值（设置 / 地图覆盖 / 玩家覆盖 + 颜色）、原版速度常量、菜单写值入口 |
| `CornerBoostUI.cs` | 菜单控件构建（`ColoredOnOff` / `ColoredSlider` / `ColoredButton`），Mod 选项、暂停入口、独立界面共用 |
| `CornerBoostMenu.cs` | 暂停菜单入口按钮 + 独立设置界面（照 EVM `AbstractSubmenu` 的摘菜单/放回流程） |
| `VerticalCornerBoostTrigger.cs` | 地图触发器 `VerticalCornerBoost/VerticalCornerBoostTrigger` |
| `VerticalCornerBoostModuleSettings.cs` | 设置项 + 6 个 `Create<属性名>Entry` 覆盖 Everest 默认控件 |
| `Loenn/triggers/verticalCornerBoostTrigger.lua` | Loenn 里的触发器定义 |
| `Ahorn/triggers/verticalCornerBoostTrigger.jl` | Ahorn 里的触发器定义 |
| `Dialog/English.txt`、`Dialog/Simplified Chinese.txt` | 中英双语本地化 |

### 触发器为什么叫 VerticalCornerBoostTrigger 而不是 CornerBoostTrigger

**原版 Celeste 就有名为 "Corner Boost"（CB）的技巧**。把触发器叫 `CornerBoostTrigger`，
mapper 在编辑器里会以为它和那个既有技巧有关（比如以为能改 CB 的行为）。
所以实体名、类名、两个编辑器定义的文件名**全都带 Vertical**。

- 已经**决定不做旧名别名**：`CustomEntity` 支持多 ID（`params string[] ids`），
  技术上可以同时注册 `VerticalCornerBoost/CornerBoostTrigger` 当兼容别名，
  但那样旧名会继续出现在 mapper 的搜索里，歧义仍在，所以彻底断开。
- **不要**为了让"旧地图兼容"再加回别名，除非确实有已发布的图用了旧名。

### 触发器尺寸为什么"能改"，以及别把它写死

`width` / `height` 在编辑器定义里**只是放置时的默认值**，玩家随时拖拽缩放。依据（都查过源码/反编译）：

- **C# 侧**：`VerticalCornerBoostTrigger` 的 ctor 只调 `base(data, offset)`；
  `Celeste.Trigger` 的 ctor 里是 `Collider = new Hitbox(data.Width, data.Height)`。
  **不要**在子类里再写 `new Hitbox(8, 8)` 之类，那才会真的把尺寸钉死。
  唯一的例外是 `coversScreen`：它主动把 Collider 换成整间房间，这是有意的。
- **Loenn 侧**：`src/entities.lua` 的
  `entities.canResize(room, layer, entity)` → `getHandlerValue(entity, room, "canResize", entity.width ~= nil, entity.height ~= nil)`。
  handler 没定义 `canResize` 时，默认值就是"width/height 字段是否存在"。
  placement 的 `data` 会被逐键复制进实体（`entities.lua` 里 `for k, v in pairs(placementInfo.data)`），
  所以**写了 width/height 就自动可缩放**，不需要额外的 `canResize` 标志。
  另外 `getPlacements` 同时兼容 `placements = { ... }`（单表）和 `placements = { {...} }`（列表）
  两种写法（`if #placements > 0 then return placements[1] else return placements end`），但**推荐列表写法**。
- **Ahorn 侧**：`@mapdef Trigger "...(... width::Integer=16, height::Integer=16 ...)` 给默认值，
  `Ahorn.EntityPlacement(Type, "rectangle")` 让它按矩形放置/缩放。
  Ahorn 的数值类型用 `Number`（参考 mod 里都是 `Number` / `Integer` / `String` / `Bool`，别写 `Float32`）。

判定顺序（`CornerBoostLogic.Update`，顺序本身有含义，别随意调换）：

```text
Scene 不是 Level                     -> 清标记，return
CornerBoostState.Prune(level)        -> 清掉别的房间/已销毁的地图条目
Dead                                 -> 移除 revertOnDeath 条目 + 清标记，return
不满足墙角几何                        -> 清标记，return   （离开墙角才重置，字典条目因此天然有界）
!CornerBoostState.Enabled            -> 清标记，return   （关掉开关要清，否则重开后旧标记会挡住本次墙角）
RequireGrab && !Input.GrabCheck      -> 清标记，return   （抓握状态，不是抓墙能力）
Speed.Y >= 0                         -> return           （只处理向上运动，且不标记，之后上冲仍能触发）
已标记                                -> return           （同一段墙角只处理一次）
标记 = true
|Speed.Y - target| < 0.1             -> return           （原版这一帧已经给过了，不叠加）
Speed.Y < target                     -> return           （CB/超级墙跳已更快，跟着原版 Math.Min 语义不降速）
Speed.Y = target
```

抓握判定为什么用 `Input.GrabCheck`：它是 `Settings.GrabMode` 的统一出口，
按住（默认）= `Grab.Check`、反转 = `!Grab.Check`、切换 = `grabToggle`，
所以三模式天然都对；而且 EVM 的 `True No Grabbing` 就是把它钩成 `false`，我们不需要任何兼容代码。
**不要**改成 `ClimbCheck()`——那是"能不能抓墙"，会让"抓墙被禁用但仍抓着"的情况失效。

几何判定用"判定盒整体平移"而不是点检测（`Entity.CollideCheck<T>(Vector2 at)`）：

- `ahead = Position + UnitX * Facing * WallGlueDistance`（默认 1f）→ 撞墙 = 贴墙
- `!CollideCheck<Solid>(ahead - UnitY * (Height + WallTopMargin))`（默认 2f）→ 墙顶高不过头顶 2 像素

---

## 4. 手感调参入口（开发者看这里）

**速度的唯一出口**：`CornerBoostState.Speed`（负数，直接赋给 `Speed.Y`），
`CornerBoostState.SpeedUp` 是它的正数形式（菜单/触发器都用正数 px/s）。
它们的取值顺序是：**玩家覆盖 → 地图触发器覆盖 → 设置档位**。
注入值 **和** "原版是否已生效"的比对基准都必须取 `CornerBoostState.Speed`，
**不要**再出现写死的 `-120`，也不要在别处另算一份速度。

| 档位（枚举名） | 显示名 | 向上速度 | 额外上升高度（估算） |
| --- | --- | --- | --- |
| `Vanilla`（默认） | 原版 | 120 | ≈ 8 px |
| `Strong` | 略有力气 | 160 | ≈ 14 px |
| `VeryStrong` | 比较有力气 | 200 | ≈ 22 px |
| `ExtremelyStrong` | 超级有力气 | 300 | ≈ 50 px |
| `Mmtgd` | MMTGD | 400 | ≈ 89 px |
| `Custom` | 自定义 | `abs(输入值)` | `v² / (2g)` |

- **枚举成员名是存档键**（YamlDotNet 按名字序列化）→ 改档位显示名只改 `Dialog` 里的
  `modoptions_verticalcornerboostmodule_preset_*`，**不要重命名枚举成员**，否则旧设置解析失败会被重置。

- 为什么 -120 感觉"没力气"：`Speed.Y` 是像素/秒，`Gravity = 900`，额外高度 ≈ `v² / (2g)`。
- `Vanilla` 档不写死数字：`CornerBoostLogic.ClimbHopYSpeed` 是 `static readonly`，静态初始化时反射读
  `Player.ClimbHopY`（`GetRawConstantValue()`），失败才退回 -120；`Load()` 里会把它打进 `log.txt`。
  这样游戏/Everest 升级改了常量也能自动跟上。`CornerBoostState.DefaultSpeedUp` 是它的正数镜像。
- `Custom` 档：`Settings.CustomSpeed` 是 **string**（Everest 给 string 属性生成文本输入按钮），
  玩家填正数，代码取绝对值再转成向上速度；解析失败 / ≤0 / NaN 一律退回原版值。
- `Settings.LimitCustomSpeed`（默认 true）把上限压在 1000 px/s；关掉就不设上限（**不推荐**，
  可能出现穿墙观感、摄像机乱飞或卡顿）。真正的输入兜底是 `[SettingMaxLength(6)]`（6 字符）。
- 几何常量（同在 `CornerBoostLogic.cs`）：`WallGlueDistance`（默认 1f，贴墙判定平移量）、
  `WallTopMargin`（默认 2f，允许墙顶高过玩家头顶的像素数；实机不触发时**优先调这个**，改 4~8）。

### 三色菜单（仿 Extended Variants，但不依赖它）

| 颜色 | 含义 | 判定（`CornerBoostState` 里的 `XxxColor`） |
| --- | --- | --- |
| 白 `Color.White` | 默认值 | 当前值 == 地图值，且地图值 == 默认值 |
| 蓝 `Color.DeepSkyBlue` | 地图触发器改过 | 当前值 == 地图值，但地图值 != 默认值 |
| 金 `Color.Goldenrod` | 玩家在游戏内自己改过 | 当前值 != 地图值 |

- 比较基准是"地图定义值"（`MapValues`）而不是 Settings；玩家把值改回与地图相同，就自动退回蓝色。
- 实现方式：`Color` 由 `Func<Color>` 每帧现算，控件在 `Render` 里更新 `UnselectedColor` 或是自己重画，
  **不要**在创建控件时算一次颜色缓存下来，否则改完值颜色不会变。
- `CustomSpeed` 输入框本身不标黄（它只是"速度"的另一种表示）；只有"地图给了速度、玩家又填了别的数"时才标黄。

### 地图触发器的语义（`VerticalCornerBoostTrigger`）

- 三个字段都可以是"不改"：`enabled` / `requireGrab` 用 `leave`，`speed` 用 `0`。
- `revertOnLeave`（默认 true）：离开触发器时撤销；为 false 时效果一直留到离开房间（换房/重进关会重建）。
- `revertOnDeath`（默认 true）：死亡时撤销；`Player.Update` 里检测到 `Dead` 就移除这些条目。
- `coversScreen`：把 Collider 扩到整间 `Level.Bounds`，等价于"进房间即生效"。
- `flag` / `flagInverted`：非空时，只有 `Level.Session.GetFlag(flag)` 成立（`flagInverted` 取反）这条覆盖才算数；
  **每帧现算**（`MapEntry.IsActive`），所以 flag 一变立刻生效，不需要进出触发器。
- 覆盖值**只存在内存里**（`CornerBoostState` 的条目表），不写 Settings，所以不会污染玩家存档。

### 玩家覆盖为什么放在 Session 里

`VerticalCornerBoostModuleSession.PlayerOverrides`（`HashSet<CornerBoostField>`）记录"玩家在游戏内改过哪些字段"。
如果只放在 static 字段里，重启游戏后这个状态会丢，而设置值仍然持久化，
结果就是**下次进图时地图值会静默盖掉玩家自己设的值**；放进 Session 后语义和 EVM 的
`Session.VariantsOverridenByUser` 一致，跨重启仍然保持"玩家优先"。

- `CornerBoostField` 因此必须是 `public`（Session 的属性类型不能比属性本身更难访问）。
- **枚举成员名是存档键**，改名要处理旧存档。
- `CornerBoostState.Clear()`（Unload 时调用）**只清地图条目**，不动玩家覆盖。

### 多版本策略（已决策：单 DLL + 档位，不要再加变体）

- "原版版 / 有力气版"两个 DLL 的方案**不做**。`everest.yaml` 只有 `Dependencies` /
  `OptionalDependencies`，**没有**互斥字段；多 DLL 只能在运行时扫描 `Everest.Modules` 仲裁，
  且两套 UI 与 Dialog 键会重复（同键互相覆盖），成本远大于收益。
- 如果将来非要做多 DLL：仲裁必须**懒执行**（首次 `Player.Update` 时判定），
  因为 `EverestModule` 没有 `Ready()`，`Load()` 时兄弟模块可能还没注册。
- 顺带记录：本实现的 `Math.Min` 语义让"更强的那个赢"，多个变体同时存在也不会来回抖。

---

## 5. 待办 / 状态

- [x] 编译通过（`dotnet publish -c Debug -p:CelestePrefix="<Celeste 目录>"` 实测无 error）
- [x] 暂停菜单开关双向可用（实机确认：关掉后不再触发）
- [x] 暂停菜单改为独立界面（照 EVM），弃用下拉式子菜单（**实机待确认**）
- [x] Mod 选项新增 `ShowPauseMenuSubMenu`（**实机待确认**）
- [x] 中英双语本地化（`Dialog/`，键前缀 `modoptions_verticalcornerboostmodule_`）
- [x] 速度档位 `Preset` + `CustomSpeed` + `LimitCustomSpeed`（编译通过，**实机待确认**）
- [x] `RequireGrab`（默认 true，读 `Input.GrabCheck`）+ 独立界面同步（**实机待确认**）
- [x] 独立界面 + “恢复默认值”（**实机待确认**）
- [x] `Enabled` 默认改为 `false`（**实机待确认**：旧存档里的 true 会保留）
- [x] 地图触发器 `VerticalCornerBoostTrigger` + Loenn 定义（**实机待确认**）
- [x] 三色菜单（`CornerBoostState` + `CornerBoostUI`，Mod 选项与暂停菜单共用；**实机待确认**）
- [ ] 抓墙被 Extended Variants 禁用时是否仍触发（预期：仍触发）
- [ ] EVM `True No Grabbing` 时是否被挡住（预期：挡住）
- [ ] 三种抓键模式实测
- [ ] CB / 抓墙跳 / 反墙跳蹭墙角各场景实测
- [ ] 地图触发器的 revertOnLeave / revertOnDeath / coversScreen 实测
- [ ] 手感微调（`WallTopMargin`；档位默认值不要动，保持 `Vanilla` = -120）

---

## 6. 构建备忘

- **首选：在工作区根目录跑 `build-release.bat`**。
  - 无参数 = Release 编译 + 生成 `VerticalCornerBoost\VerticalCornerBoost.zip`；
  - `--no-zip` = 只编译（Debug，不打包）；
  - 它自己从 `Celeste.txt` 读游戏目录，**不需要手敲 `CelestePrefix`**；
  - **该文件必须保持纯 ASCII + CRLF**：cmd.exe 用 OEM 代码页（中文系统是 GBK）解析批处理，
    写中文或存成 LF 会导致语法错乱（已踩过：`'m' 不是内部或外部命令` 这类乱码报错）。
- **Release 包不带 `.pdb`**：`PackageMod` 的 `FilesToPackage` 用 `Exclude` 排除调试符号
  （省体积、不暴露源码路径）。包内应只有 `everest.yaml` / `bin/*.dll` / `Dialog/` / `Maps/` / `Ahorn/` / `Loenn/`。
- `PackageMod` 的 `Condition` 是 `'$(Configuration)' == 'Release'`，Debug 不打包；
  其 `Inputs`/`Outputs` 已移除——通配符配单文件名的增量比较不可靠，改为每次 Release 都重新打包。

- 手动构建（等价的底层命令，一般不需要）：
  ```bat
  dotnet publish VerticalCornerBoost\Source\VerticalCornerBoost.csproj -c Release -p:CelestePrefix="<Celeste 目录>"
  ```
  工作区**没有** `Celeste.dll`，必须传 `CelestePrefix`（默认的 `lib-stripped` 不存在）。
  注意末尾要加 `-c Release` 才会顺带打包。
- 产物落在 `VerticalCornerBoost\bin\VerticalCornerBoost.dll`（`everest.yaml` 里写的就是 `bin/...`）。
- 装进游戏：把整个 `VerticalCornerBoost` 文件夹复制到 `<Celeste>\Mods\`。
- 反编译临时文件不要写进工作区（用 `%TEMP%`）。
- 反编译命令：`ilspycmd -t <类型全名> "<Celeste 目录>\\Celeste.dll"`；
  `ilspycmd` 装在 `C:\\Users\\Lenovo\\.dotnet\\tools\\ilspycmd.cmd`，不在 PATH 上时要写全路径。
- **本机沙箱注意**：DSH workspace-write 模式下 `dotnet` 写 `Source/obj` 会被拒
  （`UnauthorizedAccessException`），编译那一步需要完全访问；文件编辑工具本身不受影响。
- 抓不到外网时 `git clone` / `curl` 会被挡，但 `python -c "import urllib.request..."` 可用，
  需要参考开源 mod 源码时可以这样把 tarball 拉到 `%TEMP%`（EVM 源码就是这么取的）。
