# 项目会话上下文

> AI 跨会话记忆文件。设计细节见 `.cursor/design/`；里程碑见 `design/07-roadmap.md`。

**最后更新**：2026-06-02

## 项目概要

三国背景回合制策略战棋 MAUI 游戏。核心逻辑在 `MauiApp.Game`（纯 C#），UI 用 SkiaSharp 自绘大地图与战斗方格，内容 JSON 驱动。

| 工程 | 说明 |
|------|------|
| `MauiApp` | MAUI 壳：页面、渲染、存档、内容加载 |
| `MauiApp.Game` | 规则引擎：大地图、战斗、AI、数值 |
| `MauiApp.Game.Tests` | 核心库单测 |

## 当前状态

- **里程碑**：M0–M9 已完成（见 `design/07-roadmap.md`）
- **可玩闭环**：开局 → 经营 → 出征 → 战斗 → 占领 → 俘获/招降 → 胜负；支持存档与自定义地图加载
- **测试**：`MauiApp.Game` 单测通过；Windows 目标 0 警告 0 错误

## 当前焦点

- 建立 Cursor 项目规范：文档与规则放在 `.cursor/`，含本会话上下文机制

## 待办 / 后续（来自 roadmap）

- 战斗飘字/动画、音效
- UI 美化与操作手感
- 地图编辑器

## 已知决策

- 规则与数值与 UI 解耦：`MauiApp.Game` 无 MAUI 依赖
- 内容路径：`Resources/Raw/data/`、`maps/`；自定义地图由 `ContentProvider` 扫描应用数据目录
- Cursor 文档统一放项目 `.cursor/`，不用 C 盘用户目录

## 近期变更

- 2026-06-02：新增 `.cursor/PROJECT.md` 与会话读取/更新规则
- 2026-06-02：新增 `.cursor/rules/project-cursor-location.mdc`（项目内 Cursor 目录规范）

## 文档索引

| 路径 | 用途 |
|------|------|
| `.cursor/PROJECT.md` | 本会话上下文（优先读） |
| `.cursor/design/` | 设计与架构 |
| `.cursor/plans/` | 规划文档 |
| `.cursor/rules/` | Cursor 规则 |
