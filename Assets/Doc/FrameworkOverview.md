# BorFramework 框架总结与开发接入

本文面向参与《小小国家》（Tiny Nations）开发的技术同事，介绍当前框架的组织方式、运行流程和业务接入约定。首次接触项目时建议先阅读本文，需要定位具体方法时使用 [框架导航与 API 索引](FrameworkNavigation.md)。新增代码同时遵守 [C# 与框架开发规范](CodingStandards.md) 和 [Unity 目录规范](DirectoryStandards.md)。

本文按当前源码整理；本次整理未运行编译或 Play Mode，运行与视觉结果仍需人工验收。

## 1. 框架定位

BorFramework 是本项目基于 Unity 构建的轻量游戏框架，负责统一启动、模块访问、帧更新、资源加载、场景切换、UI 和业务对象生命周期。

框架以 `GameHub` 作为服务入口，以 `IModule` 作为基础服务生命周期协议。业务代码放在 `GameLogic`，通过模块接口使用框架能力，并通过 `IGameSystem`、`Entity`、`Logic`、`StateMachine` 组织玩法。

主要技术组成如下：

| 技术 / 结构 | 项目中的作用 |
| --- | --- |
| Unity | 场景、GameObject、组件和引擎帧循环 |
| YooAsset | 资源包初始化、资源及场景加载 |
| UniTask | 资源、场景和 UI 的异步流程 |
| Unity Input System | 输入 Action 配置与读取 |
| uGUI | Canvas UI、层级和交互 |
| UI MVVM | 将 View 显示与 ViewModel 状态分开 |
| ELC | 使用 Entity、Logic、Component 组织对象行为与数据 |
| FSM | 管理对象或业务流程的离散状态 |

## 2. 目录与架构分层

```text
Assets/
├─ Scripts/
│  ├─ BorFramework/
│  │  ├─ 1_Core/       Hub、ELC、FSM、Singleton
│  │  ├─ 2_Module/     各框架模块的接口和实现
│  │  └─ 3_Boot/       Boot 场景编辑器辅助
│  └─ GameLogic/
│     ├─ GameBoot.cs   应用组装、自动启动与帧转发
│     ├─ GameFlow/     正式业务流程与状态切换
│     └─ MainMenu/     主菜单 UI
├─ GameAsset/          项目资源与场景
├─ Resources/Input/   默认 InputActionAsset 及生成代码
└─ Doc/               框架说明与 API 导航
```

运行时关系可按下面的方向理解：

```text
GameBoot
  └─ GameHub：注册、获取、初始化、启停模块
       ├─ 基础服务：Log / Mono / Event / Input
       ├─ 内容服务：Resource / PrefabPool / Scene / UI
       └─ 业务托管：GameSystem / Entity

GameLogic
  ├─ IGameSystem：组织一项业务功能
  ├─ Entity + Logic + Comp：组织对象行为和数据
  ├─ StateMachine：组织状态切换
  └─ View + ViewModel：组织界面显示和状态
```

`1_Core` 提供基础类型，`2_Module` 提供可通过接口调用的服务；应用层的 `GameBoot` 负责选择模块实现并组装具体业务。新增玩法通常放入 `GameLogic/<Feature>`；新增框架服务才需要进入 `BorFramework/2_Module`。完整的放置判断与资源目录结构以 [Unity 目录规范](DirectoryStandards.md) 为准。

## 3. 启动与运行流程

[`GameBoot`](../Scripts/GameLogic/GameBoot.cs) 在首个场景加载前自动创建，并通过 `DontDestroyOnLoad` 常驻。它是具体游戏的应用组合根，完成以下工作：

1. 初始化 `GameHub`。
2. 创建模块并按接口类型注册。
3. 安装全程运行的 GameFlowSystem；单位 System 由 Demo 状态在场景加载后安装。
4. 按注册顺序调用各模块的 `Init()` 和 `Start()`，其中业务 System 随 `GameSystemModule` 一起启动。
5. 在自身 `Start()` 中发布 `FrameworkReadyEvent`。
6. 将 Unity 的三类帧回调转发到 `IMonoModule`。
7. 销毁时按注册逆序停止、释放模块。

当前注册顺序为：

```text
Log → Mono → Event → Input → Entity → Resource → PrefabPool
    → Scene → UI → GameSystem
```

主要依赖通过构造函数显式传入：`EntityModule` 依赖 `IMonoModule`，`PrefabPoolModule`、`SceneModule` 和
`UIModule` 依赖 `IResourceModule`。

`BorFramework` 程序集不引用任何具体业务系统。`GameBoot` 位于应用层，可以同时引用框架模块实现和具体业务类型，因此直接创建 `GameFlowSystem`，不需要静态注册表或反射扫描。

资源包初始化是异步过程。`FrameworkReadyEvent` 表示模块启动流程已执行，不等同于资源状态已经是 `Ready`。`LoadAssetAsync` 和场景加载会等待资源初始化结束；同步加载要求资源已经就绪。

启动时，`GameBoot` 在模块生命周期开始前加入 [`GameFlowSystem`](../Scripts/GameLogic/GameFlow/GameFlowSystem.cs)，由它进入主菜单状态并加载首个业务场景。

## 4. 模块访问与生命周期

业务使用注册时的接口类型获取模块：

```csharp
using BorFramework;

var resourceModule = GameHub.Ins.GetModule<IResourceModule>();
var eventModule = GameHub.Ins.GetModule<IEventModule>();
var uiModule = GameHub.Ins.GetModule<IUIModule>();
```

`GameHub` 按泛型类型保存模块，因此当前应使用 `GetModule<IResourceModule>()`，而不是 `GetModule<ResourceModule>()`。未注册的类型返回 `null`。业务入口获取依赖后，通过构造函数传给 System 或 Logic。

所有框架模块实现 [`IModule`](../Scripts/BorFramework/1_Core/Hub/IModule.cs)：

| 生命周期 | 作用 |
| --- | --- |
| `Init()` | 初始化模块数据或启动准备工作 |
| `Start()` | 开始运行，例如订阅帧事件、启用输入 |
| `Stop()` | 停止运行，例如取消帧事件、禁用输入 |
| `Dispose()` | 清理模块持有的对象、监听和资源 |

生命周期由 `GameBoot` 与 `GameHub` 协调，普通业务代码通常使用模块业务 API。

## 5. 各模块职责

| 模块接口 | 职责 | 常用入口 |
| --- | --- | --- |
| `ILogModule` | 输出日志 | `Log`、`Waring`、`Error` |
| `IMonoModule` | 提供 Unity 帧事件 | `OnFixUpdate`、`OnUpdate`、`OnLateUpdate` |
| `IEventModule` | 进程内、按事件类型分发通知 | `Subscribe`、`Unsubscribe`、`Publish` |
| `IInputModule` | 按 Action 名读取输入 | `ReadVector2`、`ReadFloat`、`IsPressed`、`WasPressedThisFrame` |
| `IEntityModule` | 保存 Entity 并驱动其 Logic | `AddEntity`、`RemoveEntity` |
| `IResourceModule` | 加载 Unity 资源并返回租约 | `LoadAssetAsync<T>`、`LoadAsset<T>`、`State` |
| `IPrefabPoolModule` | 按资源地址复用 Prefab 实例 | `RentAsync`、`Return` |
| `ISceneModule` | 按资源地址管理场景 | `LoadSceneAsync`、`UnloadSceneAsync`、`SetActiveScene` |
| `IUIModule` | UI 注册、实例缓存、绑定与导航 | `Register`、`OpenAsync`、`PushScreenAsync`、`Back` |
| `IGameSystemModule` | 托管业务系统生命周期 | `AddSystem`、`GetSystem`、`RemoveSystem` |

表中名称与当前源码一致，包括 `Waring` 和 `OnFixUpdate`。完整接口链接和方法说明见 [API 索引](FrameworkNavigation.md)。

## 6. 业务组织：System、Entity、Logic 与 FSM

### 6.1 GameSystem：组织业务流程

[`IGameSystem`](../Scripts/BorFramework/2_Module/GameSystemModule/IGameSystem.cs) 使用 `Init / Start / Stop / Dispose` 管理一项业务功能。System 可以持有多个模块引用，组织对象创建、输入判断、事件发布和资源释放。

[`GameFlowSystem`](../Scripts/GameLogic/GameFlow/GameFlowSystem.cs) 展示了这种用法：持有场景、UI 和帧模块，驱动业务状态机，并在停止与释放时解除帧回调。

```csharp
var candidate = new MyGameSystem(/* 构造参数 */);
if (!systemModule.AddSystem(candidate))
{
    candidate.Dispose();
    return;
}

var system = systemModule.GetSystem<MyGameSystem>();
// 业务结束时停止、释放并移除：
systemModule.RemoveSystem<MyGameSystem>();
```

这是接入形式示意，`MyGameSystem` 由业务实现。系统按注册的泛型类型唯一，重复类型和重复实例都会被拒绝。`AddSystem` 成功返回 `true` 并接收所有权，失败返回 `false`，候选系统仍由调用方负责；已初始化或启动时，会补调对应生命周期。`RemoveSystem<T>` 成功时先停止再释放，未注册时返回 `false`。

需要随游戏全程启动的系统由应用层 `GameBoot` 在 `InitModules()` 前创建并加入 `GameSystemModule`。

场景内临时创建的系统仍可直接使用 `AddSystem`，但必须同时规划退出场景时的移除和释放。

### 6.2 Entity：组合对象的能力

[`Entity`](../Scripts/BorFramework/1_Core/ELC/Entity/Entity.cs) 是纯 C# 对象，可关联 Unity GameObject，并组合多个 Component 和 Logic。

| 类型 | 职责 | 业务实现方式 |
| --- | --- | --- |
| `Entity` | 聚合对象的数据与行为 | 子类直接持有 Component，首次启动前用 `AddLogic` 注册行为 |
| `Comp` | 纯 C# Component 基类 | 保存对象数据或提供能力 |
| `CompMono` | MonoBehaviour Component 基类 | 用于需要 Unity 组件载体的能力 |
| `Logic` | 可按帧执行的行为 | 实现 `OnTick(float dt)` |

Entity 通过 `IEntityModule.AddEntity()` 转移所有权并加入更新；空、重复、待移除或已释放实体返回 `null`。Entity 不保存 Component 查询容器，子类直接传递所需引用；`AddComp`、`GetComp`、`GetLogic` 已移除。

更新中新增实体从下一帧开始执行。`RemoveEntity(entity, onRemoved)` 返回是否接收移除请求：立即标记实体停止后续行为，本批更新结束后再释放；更新外则立即释放。成功释放后调用 `onRemoved`，宿主 GameObject 归还池与资源租约释放应放在该回调，避免实体仍在 Tick 时先回收其表现对象。`EntityModule.Stop()` 会停止全部实体，之后 Start 可恢复；Dispose 则永久释放。

### 6.3 Logic：行为执行与阻塞

[`Logic`](../Scripts/BorFramework/1_Core/ELC/Logic/Logic.cs) 使用不可覆写的 `OnUpdate / Stop / Dispose` 统一生命周期。子类实现受保护的 `OnStart / OnTick / OnStop / OnDispose`：首次更新启动；Stop 只停止已运行行为；Dispose 先 Stop 再 OnDispose，重复调用无效，释放后不能恢复。不要在子类重新实现公开 Dispose 绕过停止顺序。

`Block()` 和 `UnBlock()` 使用计数式阻塞。阻塞后的下一次更新停止行为，解除全部阻塞后下一次更新重新启动。`AddLogic` 只允许首次启动前装配，同类型拒绝重复注册；同一 Phase 保留装配顺序。

`Phase` 用于单个 Entity 内的执行排序：

```text
Input → Command → Movement → Targeting
      → Combat → StatusEffect → Cleanup → Presentation
```

默认 Phase 为 `Command`。这套顺序作用于每个 Entity 内部，EntityModule 仍逐个更新 Entity。

### 6.4 FSM：离散状态切换

[`StateMachine`](../Scripts/BorFramework/1_Core/FSM/StateMachine.cs) 适合表达待机、移动、攻击等状态，或一组互斥的流程阶段。业务继承 `State`，实现 `OnEnter / OnUpdate / OnExit`，再用 `AddState()` 和 `ChangeState<T>()` 管理状态。

FSM 不自动接入帧循环，由所属 System 或 Logic 调用 `Update(dt)`。`Stop()` 退出当前状态，`Clear()` 同时移除已注册状态。

## 7. 资源与场景

### 7.1 资源地址和资源租约

资源模块使用 YooAsset address 定位资源，当前默认包为 `DefaultPackage`。收集配置见 [`BundleCollectorSetting.asset`](../BundleCollectorSetting.asset)。编辑器使用模拟模式，非编辑器代码使用离线模式。

加载返回 [`IAssetLease<T>`](../Scripts/BorFramework/2_Module/ResourceModule/IAssetLease.cs)，其中 `Asset` 是 Unity 资源对象，`IsValid` 表示租约是否有效，`Dispose()` 释放该引用。

以下是加载片段，应放入异步方法中，并由持有者保存租约：

```csharp
var lease = await resourceModule.LoadAssetAsync<GameObject>("MainMenuView");
if (lease == null)
    return;

var instance = UnityEngine.Object.Instantiate(lease.Asset);
// 持有 instance 和 lease，在业务结束时清理实例并释放租约。
```

资源模块负责加载，业务负责实例化普通 Prefab。租约应覆盖资源实际使用期；释放实例与释放资源引用是两个操作。UI 模块会自行持有其 Prefab 租约。

### 7.2 Prefab 实例池

[`IPrefabPoolModule`](../Scripts/BorFramework/2_Module/PrefabPoolModule/IPrefabPoolModule.cs) 按 Prefab address
维护独立池桶。每个池桶只持有一个 Prefab 资源租约，租出的 GameObject 在业务完成本次状态初始化前保持未激活。

```csharp
GameObject instance = await prefabPoolModule.RentAsync(
    "WarriorBlue",
    position,
    rotation,
    null);
if (instance == null)
    return;

// 初始化本次业务状态后再显示。
instance.SetActive(true);

// 不再使用时归还池桶，而不是销毁实例。
prefabPoolModule.Return(instance);
```

池桶使用框架内部的最大空闲数量，按实际峰值增长，不会在创建时预生成实例。框架整体 Dispose 时统一清理全部实例和资源租约。

空闲实例位于常驻池根节点下；租出和归还只切换父节点，不直接调用 Unity 的场景迁移接口。无父节点的实例保留在池模块的常驻场景中。
因此场景级业务仍应在卸载场景前归还自己的全部活跃实例。

池模块只复用 Prefab GameObject，不处理血量、技能、队伍等业务状态。业务 System 每次租用时仍应创建自己的运行时对象，
在 `SetActive(true)` 前完成物理速度、材质属性等可用于未激活对象的状态复位；Animator 必须在激活后立即复位，
不能对未激活对象调用 `Animator.Play` 或 `Animator.Update`。

### 7.3 场景加载

[`ISceneModule`](../Scripts/BorFramework/2_Module/SceneModule/ISceneModule.cs) 支持 `Single` 替换场景和 `Additive` 叠加场景，返回 `bool` 表示操作结果：

```csharp
bool loaded = await sceneModule.LoadSceneAsync("MainMenu");
```

`TryGetScene` 查询本模块记录的场景，`SetActiveScene` 设置活动场景。模块通过 `IsBusy` 控制同一时间的加载或卸载操作；卸载针对本模块加载的场景，且保留最后一个已加载场景。

## 8. UI：View、ViewModel 与导航

UI 使用 uGUI，模块启动时自动创建常驻 `UIRoot`、Canvas 和层级。主层从低到高为 `World / Screen / Window / Popup / GlobalOverlay`，每层包含 `Layer1 / Layer2 / Layer3`。

### 8.1 View 与 ViewModel 分工

- [`UIView<TViewModel>`](../Scripts/BorFramework/2_Module/UIModule/Base/UIView.cs) 持有 Unity UI 控件，通过 `OnBind()` 建立显示绑定。
- [`UIViewModelBase`](../Scripts/BorFramework/2_Module/UIModule/Base/UIViewModelBase.cs) 提供 `OnOpen / OnPause / OnResume / OnClose / Dispose`，承载页面状态和业务交互。
- [`BindableProperty<T>`](../Scripts/BorFramework/2_Module/UIModule/Binding/BindableProperty.cs) 在值变化时通知监听者，订阅时立即提供当前值。

View 中调用 `Bind(property, listener)` 建立的绑定，会在 View 解绑时自动取消。ViewModel 订阅的业务事件则由 ViewModel 自己在关闭或释放时取消。

### 8.2 UI 注册与打开

以当前主菜单为例，在业务状态中注册 Prefab 地址、层级和 ViewModel 工厂：

```csharp
uiModule.Register<MainMenuView, MainMenuViewModel>(
    "MainMenuView",
    EUILayer.Screen,
    () => new MainMenuViewModel(EnterDemo));

await uiModule.PushScreenAsync<MainMenuView>();
```

该片段对应项目现有的主菜单流程。Prefab 根对象需要挂载对应 View 组件。每个 View 类型对应一份注册记录和缓存实例。

| 操作 | 当前语义 |
| --- | --- |
| `OpenAsync<T>()` | Screen、Window 分别转入对应导航 API；其他层直接打开 |
| `PushScreenAsync<T>()` | 加载成功后关闭窗口、暂停并隐藏前一 Screen，再压入新页面；同类型已在栈中则返回 `null` |
| `OpenWindowAsync<T>()` | 在当前 Screen 上打开 Window 并压栈；无页面或同类型窗口已在栈中则返回 `null` |
| `Back()` | 优先关闭栈顶 Window，否则弹出 Screen |
| `PopScreen()` | 关闭当前窗口、弹出 Screen，对前一 Screen 调用 `OnResume()` 并重新显示 |
| `Close<T>()` | 关闭并解绑，保留实例、ViewModel 和资源租约 |
| `Destroy<T>()` | 销毁实例、释放 ViewModel 和租约，并移除注册 |

导航请求记录导航版本，每种 View 还记录打开请求版本；Window 同时记录所属 Screen。等待期间发生后续导航、关闭或停止时，过期请求返回 `null`，不再显示旧页面。隐藏的下层 Screen 保留绑定和打开生命周期，因此 `IsOpen<T>()` 仍为 `true`，不能用它代替可见性判断。新打开的 View 会置于所属子层最后一个 sibling。

世界血条、名字等可重复创建的元素可继承 `WorldUIElementBase`，通过 `Bind(target, offset)` 记录目标，不参与页面导航栈。

## 9. 事件、输入和帧更新

事件使用实现 `IEvent` 的结构体作为载体。发送方通过 `Publish` 通知，接收方通过同一事件类型订阅；事件在发布调用中同步分发，不保存历史通知。

```csharp
eventModule.Subscribe<HealthChangedEvent>(OnHealthChanged);
eventModule.Publish(new HealthChangedEvent(health));
// 接收方结束监听时：
eventModule.Unsubscribe<HealthChangedEvent>(OnHealthChanged);
```

输入通过 Action 名读取，例如 `ReadVector2("Move")`、`WasPressedThisFrame("Attack")`。默认配置位于 [`DefultInputSystem.inputactions`](../Resources/Input/DefultInputSystem.inputactions)，配套 `.cs` 是工具生成代码。

纯 C# 业务可订阅 `IMonoModule.OnUpdate` 等事件获取 `dt`。System 需要在 `Stop()` 中取消自己建立的帧订阅；Entity 的帧驱动由 `EntityModule` 完成。

## 10. 当前业务流程如何串起整个框架

当前正式流程从框架启动进入主菜单，再由用户选择 Demo：

```text
GameBoot 初始化模块并安装 GameFlowSystem
  → GameFlowSystem 进入 MainMenuState
  → MainMenuState 加载 MainMenu 场景
  → 注册并打开 MainMenuView
  → MainMenuViewModel 接收场景入口回调
  → 点击 Demo 按钮后切换状态
  → 状态加载场景并安装 UnitSceneSystems
  → Demo 生成玩家单位；调试面板可生成其他单位
```

MainMenuState 使用进入版本使旧异步结果失效，场景已加载时可直接重新注册、打开菜单；退出时销毁菜单注册，保证 GameFlowSystem Stop 后再次 Start 能重新进入。只有菜单场景与 View 均已就绪时才接受 EnterDemo。Demo 场景加载或玩家生成失败会记录错误并返回主菜单。这里描述控制流，尚未用本次运行结果验收。

建议按以下顺序阅读源码：

1. [`GameBoot`](../Scripts/GameLogic/GameBoot.cs)：框架模块与场景系统工厂如何组装。
2. [`GameFlowSystem`](../Scripts/GameLogic/GameFlow/GameFlowSystem.cs)：System 如何持有模块并驱动状态机。
3. [`MainMenuState`](../Scripts/GameLogic/GameFlow/States/MainMenuState.cs)：状态如何加载场景、注册并打开 UI。
4. [`MainMenuViewModel`](../Scripts/GameLogic/MainMenu/UI/MainMenuViewModel.cs)：ViewModel 如何承接业务回调。
5. [`MainMenuView`](../Scripts/GameLogic/MainMenu/UI/MainMenuView.cs)：View 如何绑定按钮交互。

## 11. 同事接入新功能的工作方式

开发一个新功能时，先在 `GameLogic/<Feature>` 建立业务目录，确定入口、对象、UI 和资源地址。需要持续运行的业务由 `IGameSystem` 组织，对象行为由 Entity 和 Logic 承载，状态切换使用 FSM。

业务入口获取模块并传递给后续对象。涉及资源的对象保存自己的租约；涉及订阅的对象记录对应监听，在生命周期结束时取消。UI 先注册，再根据用途选择普通打开或导航入栈。

| 开发需求 | 主要落点 |
| --- | --- |
| 新玩法流程 | `GameLogic/<Feature>` 中的入口与 `IGameSystem` |
| 角色、敌人等对象行为 | Entity 子类与 Logic 子类 |
| 对象数据 / Unity 组件能力 | `Comp` 或 `CompMono` 子类 |
| 页面、窗口、HUD | View、ViewModel、Prefab 与 UI 注册代码 |
| 跨对象通知 | 实现 `IEvent` 的事件结构体 |
| 输入配置 | `.inputactions` 资产与业务 Action 读取 |
| 新基础服务 | `2_Module` 下的接口和实现，在 `GameBoot` 注册 |

项目代码沿用已有命名和目录结构，失败流程使用空值检查、`bool`、状态枚举、提前返回和必要日志。具体约定见仓库根目录 [AGENTS.md](../../AGENTS.md)。修改 Input System 配置时编辑源资产并重新生成代码；第三方依赖源码与版本按项目约定维护。

本文描述当前实现。日常开发查询具体参数、泛型约束和源码位置时，继续使用 [框架导航与 API 索引](FrameworkNavigation.md)。
