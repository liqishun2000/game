# 项目会话上下文

> AI 跨会话记忆文件。设计细节见 `.cursor/design/`；里程碑见 `design/07-roadmap.md`。

**最后更新**：2026-06-03

## 项目概要

三国背景回合制策略战棋 MAUI 游戏。核心逻辑在 `MauiApp.Game`（纯 C#），UI 用 SkiaSharp 自绘大地图与战斗方格，内容 JSON 驱动。

| 工程 | 说明 |
|------|------|
| `MauiApp` | MAUI 壳：页面、渲染、存档、内容加载 |
| `MauiApp.Game` | 规则引擎：大地图、战斗、AI、数值 |
| `MauiApp.Game.Tests` | 核心库单测 |

## 当前状态

- **里程碑**：M0–M14 已完成（M10–M14 为像素商业化体验，见 `plans/第一关商业化体验规划.md`）
- **可玩闭环**：开局 → 经营 → 出征 → 战斗 → 占领 → 俘获/招降 → 胜负；存档与自定义地图加载
- **表现层**：像素主菜单（程序化山河背景+Zpix）、像素大地图（建筑/旗帜/图标 HUD/呼吸高亮/反馈飘字）、
  战斗演出（移动滑动、攻击冲刺、受击闪白+震屏+伤害飘字、反击、阵亡淡出、行动序条、结算横幅）、
  音频（`Plugin.Maui.Audio` + 程序化占位 SFX/BGM）、第一关关卡目标面板 + 分步新手引导
- **测试**：`MauiApp.Game` 33 单测全过；Windows 目标 0 警告 0 错误（引擎零侵入）

## 当前焦点

- M10–M14 已落地；下一步建议：替换占位资源为真实像素素材（Kenney/真实 BGM）、行军演出、节能动画微调

## 待办 / 后续

- 替换占位音频/贴图为真实像素素材；武将像素头像
- 大地图行军滑动演出（出征→进战斗过渡）
- 引导可加"高亮聚光"遮罩（当前为分步文字）
- 地图编辑器（后置）

## 已知决策

- 规则与数值与 UI 解耦：`MauiApp.Game` 无 MAUI 依赖
- 内容路径：`Resources/Raw/data/`、`maps/`；自定义地图由 `ContentProvider` 扫描应用数据目录
- Cursor 文档统一放项目 `.cursor/`，不用 C 盘用户目录

## 开发环境（本机）

- **.NET SDK 10.0.300**：两套并存。`C:\Program Files\dotnet`（机器 PATH，无 workload）；
  `C:\Users\a\.dotnet`（用户级，**已装 maui/android/ios/maccatalyst/windows + wasm-tools**）。
  → 构建 MAUI 壳工程须用 `C:\Users\a\.dotnet\dotnet.exe`（Rider 把 SDK 路径指到这里）。
  纯库/测试两套都行。
- **Python 3.14.2**：由 Python Install Manager 管理，用 `py` / `py -m pip` 调用（`python` 会跳商店）。
- 验证（2026-06-03）：`MauiApp.Game.Tests` 33 测试全过；MAUI Windows 目标构建 0 警告 0 错误。

## 近期变更

- 2026-06-03：完成 M10–M14 像素商业化体验（基础设施/主菜单/大地图/战斗演出/引导）+ 程序化占位音频；
  新增 `Rendering/`（AnimationClock、Tween、AssetCache、PixelAtlas、PixelFont、MenuBackgroundRenderer、
  HudRenderer、BattleAnimator、BattleVfx、FloatingText）、`Services/`（AudioService、SettingsStore、
  AudioKeys、ServiceHelper）、`Tutorial/LevelObjectives`、`tools/gen_audio.py`、Zpix 字体
- 2026-06-03：确定第一关「像素风商业化体验」方向，新增 `.cursor/plans/第一关商业化体验规划.md`
- 2026-06-02：新增 `.cursor/PROJECT.md` 与会话读取/更新规则
- 2026-06-02：新增 `.cursor/rules/project-cursor-location.mdc`（项目内 Cursor 目录规范）

## 文档索引

| 路径 | 用途 |
|------|------|
| `.cursor/PROJECT.md` | 本会话上下文（优先读） |
| `.cursor/design/` | 设计与架构 |
| `.cursor/plans/` | 规划文档 |
| `.cursor/rules/` | Cursor 规则 |
