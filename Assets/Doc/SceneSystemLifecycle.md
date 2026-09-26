# 场景系统启动约定

本项目借鉴 SwipGunner 的 ProjectContext / SceneContext 作用域，但继续使用现有的显式构造和 `GameSystemModule`，不引入依赖注入容器。

## 当前生命周期

| 所有者 | 存活范围 | 内容 |
| --- | --- | --- |
| `GameBoot` | 游戏进程 | 资源、场景、UI、输入、实体、事件等框架模块，以及 `GameFlowSystem`。 |
| `MainMenuState` | 主菜单 | MainMenu 场景与菜单 View；不注册导航和单位 System。 |
| `DemoState` | Demo 场景 | 加载场景后创建 `NavigationSceneSystems`，注册导航和单位 System，再生成玩家。 |
| `NavigationTestState` | NavigationTest 场景 | 加载场景后创建同一组场景系统；测试面板运行寻路查询和真实单位用例，按 Esc 返回菜单。 |

`NavigationSceneSystems` 是导航玩法的局部组合根：先注册 `NavigationSystem`，再注册依赖它的 `UnitSystem`。`GameSystemModule` 已启动时会立即调用新系统的 `Init` 和 `Start`。离开场景时先移除单位系统，再移除导航系统；单位回收、事件退订和地图解绑随各自的 `Stop` / `Dispose` 完成。调试面板仅在这组系统存在时创建。

`NavigationTest.unity` 是独立的固定测试地图，位于 `Assets/GameAsset/Scene`，由现有 YooAsset Scenes Collector 自动收集，地址为 `NavigationTest`。它只保留导航所需的 `World/Grid/Ground`、`Collision` 和相机；`NavigationTestPanel` 在该场景中运行固定与自由寻路查询，也通过 `NavigationUnitTestRunner` 生成具体单位、观察真实移动。地图布局、预期结果和操作方法见 [NavigationTestGuide.md](NavigationTestGuide.md)。

## 新增另一类测试场景

以 RTS 建造测试为例：创建独立场景和流程状态，让该状态在场景加载完成后安装它实际需要的 `RTSBuildingSystem`，退出时移除。若它还需要导航，再显式注册导航依赖。不要把新业务 System 默认加入 `GameBoot`，也不要为每个场景预建空 Installer。

验收时检查：主菜单无导航和单位 System；进入导航测试场景后地图网格与单位调试面板可用；按 Esc 返回菜单后单位被回收、导航地图解绑；再次进入仍可正常启动。当前文档只记录源码契约，Unity 编译、Play Mode 和视觉验收由项目开发者执行。
