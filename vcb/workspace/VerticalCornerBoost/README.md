# VerticalCornerBoost / 纵向抓角加速

Celeste (Everest) 代码 Mod。当玩家在**任意来源的向上运动中**、处于"贴着墙且墙顶就在头顶附近"
的墙角状态时，向 `Player.Speed.Y` 注入原版 `ClimbHop` 的纵向速度。不修改 `Speed.X`，
不依赖 `StateMachine` / `ClimbCheck()`。

## 安装

1. `dotnet publish -c Debug -p:CelestePrefix="<Celeste 安装目录>"`（在 `Source/` 下执行）
2. 把整个 `VerticalCornerBoost` 文件夹复制到 `<Celeste>\Mods\`

## 开关

- Mod 选项：`Enable Vertical Corner Boost`（启用机制）、`Show submenu in pause menu`（暂停菜单是否显示子菜单）、
  `Speed preset`（速度档位）、`Custom speed`（自定义速度，填正数）、`Limit custom speed`（是否限制上限，默认开）
- 暂停菜单：`Vertical Corner Boost` 子菜单内的 `Enabled` 开关，与 Mod 选项共用同一个 `Settings.Enabled`

## 手感调节

内置档位（括号内是向上速度）：

- 原版（120，与原版 ClimbHop 等值，运行时反射读取）
- 略有力气（160）/ 比较有力气（200）
- 超级有力气（300）/ MMTGD（400）
- 自定义

- 选 `Custom` 时在 `Custom speed` 里填**正数**（例如 160），程序会自动转成向上速度（-160）；填错或 ≤0 退回原版值。
- `Limit custom speed` 默认开启，把上限压在 1000 px/s；关掉则不设上限（不推荐，可能出现穿墙观感或卡顿）。
  输入框本身最多 6 个字符，所以不会出现天文数字。
- `Vanilla` 档的 120 是**运行时**从游戏里反射读 `Player.ClimbHopY` 拿到的（读不到才用 120 兜底），
  启动时会把读到的值写进 `log.txt`。这样 Celeste/Everest 将来改了常量，本 Mod 会自动跟上。

`Speed.Y` 是像素/秒，`Gravity = 900`，额外上升高度约为 `v² / (2g)`：

| 档位 | 向上速度 | 额外上升高度（估算） |
| --- | --- | --- |
| 原版 | 120 | ≈ 8 px |
| 略有力气 | 160 | ≈ 14 px |
| 比较有力气 | 200 | ≈ 22 px |
| 超级有力气 | 300 | ≈ 50 px |
| MMTGD | 400 | ≈ 89 px |

同文件另有两个几何常量：`WallGlueDistance`（贴墙判定平移量，默认 1f）、
`WallTopMargin`（允许墙顶高过头顶的像素数，默认 2f；**实机不触发时优先调它**）。

注意：改成非 -120 就不再是"和原版翻越等值"的还原版了。

## 本地化

`Dialog/English.txt`、`Dialog/Simplified Chinese.txt`。Mod 选项的键名规则是
`modoptions_` + 设置类名小写去 `settings` 后缀 + `_` + 属性名小写（`EverestModule.CreateModMenuSection` 的规则），
即前缀 `modoptions_verticalcornerboostmodule_`；暂停菜单自建项用 `VCB_MENU_TITLE` / `VCB_MENU_TOGGLE`。

更详细的反编译事实、踩坑记录与待办见仓库根目录的 `AI-EXTRA.md`。
