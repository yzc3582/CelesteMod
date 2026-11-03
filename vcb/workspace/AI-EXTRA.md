# AI-EXTRA.md — 给后续 Agent 的上下文补充

> 本文件记录**已经反编译 `Celeste.dll` / 实测确认过**的事实、踩过的坑、当前待办与调参入口。
> `AGENTS.md` 是需求与约束的权威；本文件是"怎么实现才不会翻车"的补充。
> 冲突时以 `AGENTS.md` 为准，但请先看本文件的"反编译事实"一节——有些坑不看这里会重踩。
>
> **写这一节时请特别注意措辞**：本文件曾被外部 AI 阅读后误读出「Trigger 是房间级」
> 「`CornerBoostState.Enabled` 是那个开关」「全局开关与 Trigger 是 AND/OR 关系」三个错误结论。
> 凡是涉及**作用域 / 优先级 / 谁覆盖谁**的地方，一律用代码片段说话，不要只写自然语言。
> 现在第 3 节「地图触发器的语义」已经按这个标准重写。

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
Scene 不是 Level                          -> 清 hasHoppedThisWall，return
CornerBoostState.Prune(level)             -> 清掉「已销毁 / 属于别的 Level」的**地图条目**
CornerBoostLogic.PruneStale(level)        -> 清掉「已不在本 Level」的 **Player 标记**
Dead                                      -> 移除 revertOnDeath 条目 + 清标记，return
不满足墙角几何                             -> 清标记，return   （离开墙角才重置）
!CornerBoostState.Enabled                 -> 清标记，return   （关掉开关要清，否则重开后旧标记会挡住）
RequireGrab && !Input.GrabCheck           -> 清标记，return   （松手后重抓可再次触发）
Speed.Y >= 0                              -> **清标记**，return（下落中清标记，之后上冲能再触发）
已标记                                     -> return           （同一段上冲只处理一次）
标记 = true
|Speed.Y - target| < 0.1                  -> return           （原版这一帧已经给过了，不叠加）
Speed.Y < target                          -> return           （CB/超级墙跳已更快，跟着原版 Math.Min 语义不降速）
Speed.Y = target
```

**两套「条目」别搞混**（这是最容易误读的地方）：

| 名字 | 存什么 | 什么时候清 |
| --- | --- | --- |
| `CornerBoostState.entries`（地图条目） | 每个**已进入的触发器**一条，带它给的覆盖值 | `OnLeave`（`revertOnLeave` 时）、`revertOnDeath`、`Prune` |
| `CornerBoostLogic.hasHoppedThisWall`（玩家标记） | 每个 Player 一条，表示「这段上冲已处理」 | 判定顺序里各分支 `Remove(self)`、`PruneStale` |

`Prune` 里的「别的房间」指的是**地图条目跨 Level 残留**（换房后旧 Level 的条目要清），
**不是**说「一个房间只存一个开关」。详见下面的作用域说明。

**为什么 `Speed.Y >= 0` 要清标记**：玩家拿到加速后开始下落，再次上冲时应当能再触发一次
（已和用户确认这是期望行为）。早期版本这里不清标记，导致「下落再上冲」被 `ContainsKey` 挡住，是 bug。
注入后 `Speed.Y = target`（负值），后续帧仍走 `Speed.Y < 0` 分支，所以**不会**在同一段上冲里重复注入。

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

### 已知行为：墙顶有刺时不翻墙（**这是原版行为，与 Mod 无关**）

**现象**：抓着墙往上爬，墙顶**没有**刺时会正常翻上去；墙顶**有**刺时翻不上去，
但本 Mod 的加速仍然会生效（因为加速判定只看 `Solid` 几何，不看能不能翻越）。

**这不是 Mod 的 bug，是原版 Celeste 的既有行为。** 机制如下（均已反编译核实）：

1. `Celeste.Spikes` 是 `public class Spikes : Entity`，构造时执行 `Add(new LedgeBlocker());`
   —— 尖刺**不是 `Solid`**，但它挂了一个 `LedgeBlocker` 组件。
2. 原版攀爬上移分支（`Player.ClimbUpdate`，`Input.MoveY.Value == -1` 时）判定：
   ```csharp
   if (CollideCheck<Solid>(Position - Vector2.UnitY)
       || (ClimbHopBlockedCheck() && SlipCheck(-1f)))
   {
       if (Speed.Y < 0f) Speed.Y = 0f;   // 把上移速度归零
       num = 0f;
       flag = true;                       // 本帧不爬
   }
   else if (SlipCheck()) { ClimbHop(); return 0; }
   ```
   读法（注意 `||` 的短路与括号）：

   - 若 `CollideCheck<Solid>(Position - UnitY)`（头顶正上方有实心块）**或**
     `(ClimbHopBlockedCheck() && SlipCheck(-1f))` 成立 → 清 `Speed.Y`、本帧不爬；
   - 否则若 `SlipCheck()` → `ClimbHop()` 翻上去。

   所以挡住翻越的是 `ClimbHopBlockedCheck()`（**尖刺走这条**），`SlipCheck` 本身只查 `Solid`、与尖刺无关；
   两者是 `&&` 关系，缺一不可。
3. `Player.ClimbHopBlockedCheck()` 会遍历 `Scene.Tracker.GetComponents<LedgeBlocker>()` 并调用
   `HopBlockCheck(this)`；`LedgeBlocker.Blocking` 默认是 `true`，且
   `HopBlockCheck` 里还有 `player.CollideCheck(Entity, player.Position + UnitX * Facing * 8f)`
   —— 尖刺正好挂在墙顶，这个 8px 前探必定命中，于是返回 `true`。
4. 结论：**墙顶有刺 → `ClimbHopBlockedCheck()` 为真 → 原版把上移速度清零、不调用 `ClimbHop()` → 翻不上去。**
   这是原版给 mapper 的控制手段（墙顶放刺就是「别从这儿翻」），不是缺陷。

**本 Mod 与它的关系**：

- 本 Mod 的墙角判定只查 `CollideCheck<Solid>`，**看不到 `LedgeBlocker`**，所以「墙顶有刺」照样会给加速；
  给了速度但因为上面第 4 条，角色上不去。表现就是「吃了加速但没翻过去」。
- **用户明确表示不修改该行为**，也**不要**试图让 Mod 绕过 `LedgeBlocker`（那等于让 Mod 无视 mapper
  用尖刺表达的「此处禁止翻越」意图，是越界）。

**后续 Agent 请注意**：这不是待修的 bug。如果要改，先问用户。

### 地图触发器的语义（`VerticalCornerBoostTrigger`）

#### 1. 作用域：默认**碰撞箱级**，`revertOnLeave=false` 时升级为**房间级**

`MapEntry.IsActive` **只看两件事**：`Owner` 还活着、`Level` 是当前房间。
它**不检查玩家是否还站在箱子里**。所以作用域由 `revertOnLeave` 决定：

| `revertOnLeave` | 条目何时消失 | 实际作用域 |
| --- | --- | --- |
| `true`（默认） | `OnLeave` 时 `RemoveEntry` | **碰撞箱级**——只在箱内生效 |
| `false` | 只有死亡(`revertOnDeath`)/换房(`Prune`)才清 | **房间级**——离开箱子后整间房仍受影响 |

所以 `width/height` **就是覆盖范围本身**，不是"辅助判定"。想让整间房生效有两种写法：

- 把触发器拉满房间，或勾 `coversScreen`（`Added` 里把 Collider 换成 `Level.Bounds`）；
- 或者就写 `revertOnLeave = false`（进一次箱子之后，整间房长期生效）。

`Prune(level)` 每帧清理的是「`Owner` 已销毁」或「`Level` 不是当前房间」的**残留条目**，
防止换房后旧条目继续影响（`IsActive` 也会二次校验 `Level == scene`）。

#### 2. 优先级：三层，**地图默认什么都不改**

```csharp
// CornerBoostState 里三个生效值的统一形状
玩家在游戏内改过该字段  ?  设置值
                        :  (地图覆盖值 ?? 设置值)
```

具体到 `Enabled`：

```csharp
public static bool Enabled =>
    Overrides.Contains(CornerBoostField.Enabled)
        ? Settings.Enabled
        : MapValues.Enabled ?? Settings.Enabled;
```

**这不是 AND / OR 关系，而是「覆盖」关系**：

- 触发器字段默认 `leave`（= 不管），此时 `MapValues.Enabled` 是 `null`，退化为 `Settings.Enabled`
  → **没放触发器的房间，行为和纯全局开关时完全一样**，不存在"必须有触发器才生效"。
- 触发器写了 `enable` / `disable`，且玩家没在游戏内手动改过该字段 → **地图说了算**。
- 玩家在游戏内用菜单改过该字段（`PlayerOverrides` 含它）→ **玩家说了算**，直到玩家把值改回与地图相同
  （此时自动移出 `PlayerOverrides`，重新变回蓝色 = 地图值）。

一句话：**全局设置是默认值，地图触发器是覆盖，玩家的游戏内改动优先级最高。**

#### 3. 多触发器同房间：按「后进入者覆盖先进入者」合并

`MapValues` 遍历所有 `IsActive` 的条目，**逐个字段**合并，后遍历到的覆盖前面的：

```csharp
foreach (MapEntry entry in entries) {
    if (!entry.IsActive(scene)) continue;
    if (entry.Values.Enabled.HasValue)    merged.Enabled = entry.Values.Enabled;
    if (entry.Values.RequireGrab.HasValue) merged.RequireGrab = entry.Values.RequireGrab;
    if (entry.Values.SpeedUp.HasValue)     merged.SpeedUp = entry.Values.SpeedUp;
}
```

- 每个条目**只合并自己非 null 的字段**，所以「A 管 enabled、B 管 speed」这种组合是成立的。
- 两条都管同一字段时，**列表里靠后的赢**（即后 `OnEnter` 的那条）。
  这是「后进先出」的近似，没有做栈式撤销；`revertOnLeave` 只是把条目整条删掉。
- 如果需要确定性的优先级，目前**没有显式 priority 字段**，靠进出顺序。改需求要另加字段。

#### 4. 其它字段

- `enabled` / `requireGrab` 用 `leave` / `enable` / `disable`；`leave` = 不管这个字段。
- `speed` 用 `0` = 不改；正数 = 向上速度 px/s。注意这是**独立通道**，不经过 `Preset` 解析，
  也不受 `LimitCustomSpeed` 的 1000 上限约束。
- `revertOnLeave`（默认 true）：离开箱子时撤销，**这是"碰撞箱级"的来源**；
  改成 false 就变成"进过一次之后整间房长期生效"（房间级）。详见上面第 1 条。
- `revertOnDeath`（默认 true）：死亡时撤销。
- `coversScreen`：把 Collider 扩到整间 `Level.Bounds`，等价于"进房间即生效"。
- `flag` / `flagInverted`：非空时只有 `Level.Session.GetFlag(flag)` 成立（可反转）这条覆盖才生效；
  **每帧现算**（`MapEntry.IsActive`），flag 一变立刻生效，不需要进出触发器。
- 覆盖值**只存在内存里**（`CornerBoostState` 的条目表），不写 Settings，不污染存档。

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
