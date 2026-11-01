# VerticalCornerBoost / 纵向抓角加速

Celeste (Everest) 代码 Mod。当玩家在**任意来源的向上运动中**、处于"贴着墙且墙顶就在头顶附近"
的墙角状态时，向 `Player.Speed.Y` 注入原版 `ClimbHop` 的纵向速度。不修改 `Speed.X`，
不依赖 `StateMachine` / `ClimbCheck()`。

## 安装

1. `dotnet publish -c Debug -p:CelestePrefix="<Celeste 安装目录>"`（在 `Source/` 下执行）
2. 把整个 `VerticalCornerBoost` 文件夹复制到 `<Celeste>\Mods\`

## 开关

- Mod 选项：`Enable Vertical Corner Boost`（启用机制）、`Show submenu in pause menu`（暂停菜单是否显示子菜单）
- 暂停菜单：`Vertical Corner Boost` 子菜单内的 `Enabled` 开关，与 Mod 选项共用同一个 `Settings.Enabled`

## 手感调节（开发者）

速度数值只有一处，改完重新 `publish` 即可：

```csharp
// Source/CornerBoostLogic.cs
public const float ClimbHopYSpeed = -120f;   // 原版 ClimbHop 的值
```

`Speed.Y` 是像素/秒，`Gravity = 900`，额外上升高度约为 `v² / (2g)`：

| 取值 | 额外上升高度（估算） |
| --- | --- |
| -120（原版） | ≈ 8 px |
| -140 | ≈ 11 px |
| -160 | ≈ 14 px |
| -200 | ≈ 22 px |

同文件另有两个几何常量：`WallGlueDistance`（贴墙判定平移量，默认 1f）、
`WallTopMargin`（允许墙顶高过头顶的像素数，默认 2f；**实机不触发时优先调它**）。

注意：改成非 -120 就不再是"和原版翻越等值"的还原版了。

## 本地化

`Dialog/English.txt`、`Dialog/Simplified Chinese.txt`。Mod 选项的键名规则是
`modoptions_` + 设置类名小写去 `settings` 后缀 + `_` + 属性名小写（`EverestModule.CreateModMenuSection` 的规则），
即前缀 `modoptions_verticalcornerboostmodule_`；暂停菜单自建项用 `VCB_MENU_TITLE` / `VCB_MENU_TOGGLE`。

更详细的反编译事实、踩坑记录与待办见仓库根目录的 `AI-EXTRA.md`。
