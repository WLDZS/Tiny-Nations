# Tiny Nations 项目简报

> 状态快照：2026-09-21  
> 当前阶段：核心玩法垂直切片 / 技术原型  
> 项目分支：`main`

## 一句话概述

Tiny Nations（小小国家）是一款基于 Unity 6 的 2D 小规模 RTS 原型。当前开发重点不是扩张完整 RTS 系统，而是先验证一条稳定、可复用的单位战斗链路：进入演示场景、生成单位、寻路接敌、执行近战技能、结算伤害与死亡，并安全回收运行时对象。

## 项目概况

| 项目项 | 当前情况 |
| --- | --- |
| 引擎 | Unity `6000.6.0f1` |
| 渲染与内容形态 | URP 17.7、2D 像素风资源 |
| 核心架构 | BorFramework + GameLogic，采用 Module、GameSystem、ELC、FSM 分层 |
| 资源系统 | YooAsset，业务通过资源地址和租约访问资源 |
| 异步方案 | UniTask |
| 输入方案 | Unity Input System |
| UI | uGUI + 项目内 MVVM 与导航栈 |
| 当前内容规模 | 7 个 UnitDefinition、7 个单位 Prefab、4 个正式 GameAsset 场景 |
| 代码规模 | 63 个 GameLogic C# 文件、56 个 BorFramework C# 文件 |

## 当前已形成的核心能力

### 1. 框架与流程

- `GameBoot` 作为应用组合根，负责模块注册、业务 System 组装和 Unity 帧回调转发。
- `GameFlowSystem` 通过状态机串联主菜单与 Demo 场景。
- 框架已提供日志、事件、输入、实体、资源、Prefab 池、场景、UI、存档占位、配置占位和业务系统生命周期。
- 框架层保持玩法无关，单位、导航和游戏流程均位于 `GameLogic`。

### 2. 数据驱动单位

- 已配置 Minotaur、PaddleFish、Panda、PigRider、Skull、Spider、WarriorBlue 共 7 个单位。
- 单位差异主要由 `UnitDefinition`、属性配置、技能配置、Animator Controller 和动画资源表达。
- 通用 `UnitEntity` 组合输入、移动、导航、技能、效果、队伍、生命和表现逻辑，避免为每个兵种复制一套代码。
- 开发版调试面板可按 UnitDefinition 发现并生成单位，便于快速验证内容配置。

### 3. 战斗运行时

- 已具备玩家移动、近战攻击、防御、属性与 GameEffect 结算链路。
- 运行时 `TeamId` 支持 Self、Ally、Enemy 关系判断，近战技能可按目标关系过滤。
- 伤害事件、受击闪白、生命状态和死亡事件已接入。
- 死亡单位在当帧 `LateUpdate` 安全移除，避免更新集合期间重入销毁。
- 单位 GameObject 由 Prefab 池复用；每次生成重新建立业务运行时状态，回收时释放相应资源租约。

### 4. 导航与近战 AI

- 导航系统基于 Ground / Collision Tilemap 建立可行走网格，并使用八方向 A* 搜索路径。
- 路径计算包含单位半径净空、斜向穿角限制、路径段碰撞检查、转折点压缩和目标点接近处理。
- 单位导航逻辑已加入起点连接、经过路径点判定和卡住后重新寻路。
- 当前近战 AI 可定期寻找最近敌人、保持目标、追击、面向目标，并在攻击范围内停止移动和触发主技能。

> 导航和近战 AI 位于当前未提交工作区中，应视为“正在开发”，不能仅凭源码存在判定为运行验收完成。

## 运行结构

```mermaid
flowchart LR
    Boot[GameBoot] --> Hub[GameHub / Modules]
    Hub --> Flow[GameFlowSystem]
    Flow --> Menu[MainMenu]
    Flow --> Demo[Demo]
    Demo --> UnitSystem[UnitSystem]
    Demo --> Navigation[NavigationSystem]
    UnitSystem --> Definition[UnitDefinition / 属性 / 技能]
    UnitSystem --> Pool[PrefabPool]
    UnitSystem --> Entity[UnitEntity]
    Entity --> Command[输入或 AI 命令]
    Command --> Path[导航与移动]
    Path --> Combat[技能与 GameEffect]
    Combat --> Death[伤害 / 死亡 / 回收]
```

## 进度判断

| 模块 | 状态 | 说明 |
| --- | --- | --- |
| 应用启动与模块生命周期 | 已落地 | 已形成统一组合根和启停释放流程 |
| 主菜单 → Demo 流程 | 已落地 | 状态机负责场景与 UI 切换 |
| 单位数据与资源规范 | 已落地 | 7 个单位已按统一规范配置 |
| 单位生成与对象池 | 已落地 | 资源加载、运行时创建与回收链路完整 |
| 基础近战、阵营、伤害与死亡 | 已实现，待完整运行验收 | 静态链路存在，仍需在当前版本验证事件顺序与回收行为 |
| 受击闪白 | 已实现，待视觉验收 | 需要在 Unity Game 视图确认最终效果 |
| Tilemap 寻路 | 开发中 | 当前工作区包含未提交改动 |
| 近战自动索敌与追击 | 开发中 | 当前工作区包含未提交改动，行为边界仍需通过实测确认 |
| 完整 RTS 操作层 | 尚未开始 | 尚未形成框选、编队、Attack-Move、生产与经济闭环 |
| 联机与热更新 | 规划项 | 当前不应先于单机战斗垂直切片推进 |

## 当前风险与约束

1. **运行验收不足**：部分能力已通过源码或构建检查，但死亡、受击表现、障碍拐角和 AI 接敌仍需要当前 Unity Play Mode 结果确认。
2. **工作区尚未收口**：导航、近战 AI、单位系统和技能代码存在未提交修改，应先完成一轮聚焦验收，再继续扩展功能。
3. **行为合同仍需固定**：自动索敌范围、目标保持、追击距离、脱战返回和玩家命令优先级会直接影响 AI 结构，应在增加复杂 AI 前明确。
4. **框架文档可能滞后于业务代码**：框架总览仍以早期 GameLogic 目录为例，后续可在当前功能稳定后同步导航与单位系统说明。
5. **范围膨胀风险**：联网、完整阵营、经济、生产和战略 AI 都依赖稳定的单位战斗基础，不适合与当前垂直切片并行展开。

## 建议的下一阶段

### P0：收口当前战斗闭环

- 验证单位绕过障碍拐角时不再卡住，并记录失败场景。
- 验证 AI 的“发现目标 → 追击 → 停在攻击距离 → 连续攻击 → 目标死亡后重新索敌”。
- 验证致死伤害只触发一次死亡、当帧末回收，且再次生成同一 Prefab 无残留状态。
- 验证受击闪白不会污染共享材质，并在池化复用后恢复正确颜色。

### P1：固定最小 AI 行为合同

- 明确自动索敌、目标保持、追击上限和脱战返回规则。
- 明确玩家移动、攻击命令与自动 AI 的覆盖优先级。
- 在现有 Logic / Component 上完成最小实现，不提前引入行为树、GOAP、威胁表或编队系统。

### P2：扩展为可玩的 RTS 操作切片

- 增加单位选择与目标指令。
- 增加 Attack-Move 或等价的移动接敌命令。
- 用少量不同单位验证数据驱动技能和属性差异。
- 形成一段可重复演示的短战斗流程，再评估生产、经济、关卡和联网需求。

## 近期验收标准

当前阶段可以用以下结果判断“垂直切片完成”：

- 玩家可从主菜单进入 Demo，玩家单位稳定生成。
- 敌对单位能够寻路接近目标，不穿越障碍，也不会长期卡在拐角。
- 双方在正确距离内攻击，伤害、受击反馈和死亡只按预期触发。
- 死亡单位停止输入、移动与技能，并安全回收到 Prefab 池。
- 重复生成、战斗、死亡和回收后，无明显状态残留或持续报错。

## 关键资料入口

- 项目入口：`README.md`
- 框架概览：`Assets/Doc/FrameworkOverview.md`
- 框架 API 导航：`Assets/Doc/FrameworkNavigation.md`
- C# 与生命周期规范：`Assets/Doc/CodingStandards.md`
- Unity 目录规范：`Assets/Doc/DirectoryStandards.md`
- Unit 制作与运行时契约：`Assets/Doc/UnitPrefabAuthoring.md`
- 当前应用组合根：`Assets/Scripts/GameLogic/GameBoot.cs`
- 单位系统：`Assets/Scripts/GameLogic/Units/Systems/UnitSystem.cs`
- 导航系统：`Assets/Scripts/GameLogic/Navigation/NavigationSystem.cs`

---

本简报依据 2026-09-21 当前工作区的源码、资源、项目配置和 Git 状态整理。此次仅做静态检查，没有运行 Unity、进入 Play Mode 或执行编译，因此所有运行与视觉结论均保留相应的待验证标记。
