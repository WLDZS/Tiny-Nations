# 场景系统启动约定

本项目借鉴 SwipGunner 的 ProjectContext / SceneContext 作用域，但继续使用现有的显式构造和 `GameSystemModule`，不引入依赖注入容器。

## 当前生命周期

| 所有者 | 存活范围 | 内容 |
| --- | --- | --- |
| `GameBoot` | 游戏进程 | 资源、场景、UI、输入、实体、事件等框架模块，以及 `GameFlowSystem`。 |
| `MainMenuState` | 主菜单 | MainMenu 场景与菜单 View；不注册导航和单位 System。 |
| `DemoState` | Demo 场景 | 加载场景后创建 `UnitSceneSystems`，依次注册导航和单位 System，再生成玩家。 |

`UnitSceneSystems` 是 Demo 的局部组合根。它从已加载的 Demo 场景读取 `World/Grid/Ground` 与 `World/Grid/Collision`，按 Ground 范围建立导航格子；格子有 Ground Tile 且没有 Collision Tile 时可行走。Collision 内的半格与斜边 Tile 目前按整格禁行。建图失败时不注册单位系统，并返回主菜单。

地图构建完成后，先以 `INavigationSystem` 注册导航系统，再以 `IUnitSystem` 注册单位系统。模块已启动时会立即调用各系统的 `Init` 和 `Start`。离开场景时先移除单位系统，再移除导航系统；调试面板仅在单位系统存在时创建。开发版的单位生成面板可切换导航网格覆盖层：绿为可行走，红为 Collision 禁行，灰为 Ground 范围内无地面 Tile。本阶段只提供地图数据，不包含路径搜索或单位自动移动。

## 新增另一类测试场景

以 RTS 建造测试为例：创建独立场景和流程状态，让该状态在场景加载完成后安装它实际需要的 `RTSBuildingSystem`，退出时移除。不要把新业务 System 默认加入 `GameBoot`，也不要为每个场景预建空 Installer。

验收时检查：主菜单无导航和单位 System；进入 Demo 后导航格子可查询，玩家仍可用方向输入移动；返回菜单后两个 System 均被移除，再次进入仍可正常启动。当前文档只记录源码契约，Unity 编译、Play Mode 和视觉验收由项目开发者执行。
