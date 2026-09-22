# Tiny-Nations 与 SwipGunner 架构学习总结

> 记录日期：2026-09-21  
> 评审范围：`Tiny-Nations` 当前工作区与本机 `E:\Unity project\SwipGunner` 项目源码。  
> 评审方式：只读静态分析，没有运行 Unity、进入 Play Mode 或执行编译。本文用于学习和归档，不是迁移方案。

## 1. 学习目标与结论

这次分析的目的不是决定是否将 Tiny-Nations 迁移到 SwipGunner，而是研究另一套 Unity 游戏工程如何组织依赖、生命周期和业务功能，再判断哪些设计原则值得吸收。

两套项目解决的问题高度重叠，但设计哲学不同：

- Tiny-Nations 使用显式组合根、轻量模块和手动构造函数注入，强调依赖透明、框架与业务分离以及资源所有权。
- SwipGunner 使用 Zenject、多个 Context 和 Installer 管理大型依赖图，强调作用域、功能纵向切片和完整业务闭环。

SwipGunner 的工程完成度更高，已经落地存档、钱包、UI、关卡、状态机、对象池、音频、广告和教程等功能；Tiny-Nations 的框架边界更清楚、机制更少，也更适合当前 RTS 项目方向。

最值得吸收的不是 Zenject 本身，而是以下原则：

1. 用作用域明确对象由谁创建、持有和销毁。
2. 按功能组织完整业务切片，而不是只按技术类型分目录。
3. 让订阅、资源句柄、帧回调和运行时对象形成生命周期闭环。
4. 通过构造函数声明依赖，把具体实现的选择留在组合根。
5. 框架提供通用机制，业务决定具体策略和内容。

## 2. 两套架构的基本形态

### 2.1 Tiny-Nations

Tiny-Nations 的主要运行链路是：

```text
GameBoot
  -> GameHub
    -> Module
      -> GameSystem
        -> Entity
          -> Comp + Logic
```

主要职责如下：

- [`GameBoot`](../Scripts/GameLogic/GameBoot.cs) 是应用组合根，创建模块和业务 System，并启动框架生命周期。
- [`GameHub`](../Scripts/BorFramework/1_Core/Hub/GameHub.cs) 按接口类型保存全局模块。
- `IModule` 统一框架服务的初始化、启动、停止和释放。
- `IGameSystem` 承载一项持续运行的业务功能。
- `Entity + Comp + Logic` 组织单位等运行时对象的数据与行为。
- `StateMachine` 表达互斥状态或业务流程。
- `IAssetLease<T>` 表达资源引用的所有权和释放责任。

这种结构的核心特点是依赖显式。应用入口可以直接看到对象如何创建、依赖如何连接：

```csharp
var resourceModule = new ResourceModule();
var prefabPoolModule = new PrefabPoolModule(resourceModule);
var sceneModule = new SceneModule(resourceModule);
var uiModule = new UIModule(resourceModule);
```

### 2.2 SwipGunner

SwipGunner 的主要运行链路是：

```text
ProjectContext
  -> GlobalSceneContext
  -> GameplaySceneContext / UISceneContext
    -> GameObjectContext
      -> Installer
        -> Service / Presenter / StateMachine / Model
```

主要职责如下：

- `ProjectContext` 安装整个游戏进程共享的服务。
- 不同 `SceneContext` 安装对应场景需要的功能。
- 玩家和敌人 Prefab 可以通过 `GameObjectContext` 获得独立依赖作用域。
- Installer 声明接口到实现、实例生命周期和场景组件的绑定关系。
- `IInitializable`、`ITickable` 和 `IDisposable` 由 Zenject 生命周期统一驱动。
- SignalBus 负责跨功能广播。
- Feature 目录通常包含一项业务所需的配置、服务、表现、状态和组装代码。

相关入口：

- [ProjectContextInstaller](../../../SwipGunner/Assets/KrolStudio/Content/Features/GameBootstrapModule/Scripts/Installers/ProjectContextInstaller.cs)
- [GlobalSceneContextInstaller](../../../SwipGunner/Assets/KrolStudio/Content/Features/GameBootstrapModule/Scripts/Installers/GlobalSceneContextInstaller.cs)
- [GameplaySceneContextInstaller](../../../SwipGunner/Assets/KrolStudio/Content/Features/GameBootstrapModule/Scripts/Installers/GameplaySceneContextInstaller.cs)
- [UISceneContextInstaller](../../../SwipGunner/Assets/KrolStudio/Content/Features/GameBootstrapModule/Scripts/Installers/UISceneContextInstaller.cs)

## 3. 作用域：SwipGunner 最值得学习的设计

SwipGunner 用 Context 表达不同生命周期：

| 作用域 | 生命周期 | 典型内容 |
| --- | --- | --- |
| Project | 整个游戏进程 | 场景加载、日志、全局 Signal、游戏流程 |
| Global Scene | 跨关卡业务 | 存档、钱包、暂停、震动、相机 |
| Gameplay Scene | 单次玩法 | 关卡、敌人生成、Gameplay 对象池 |
| UI Scene | UI 场景 | UI 管理器、Presenter、教程 |
| GameObject | 单个实体 | 玩家、敌人、触手的状态机和表现依赖 |

作用域真正解决的是：

- 谁创建对象；
- 谁持有对象；
- 对象应该存活多久；
- 谁负责停止和销毁；
- 离开场景后是否还允许访问该对象。

Tiny-Nations 已经有清楚的全局作用域：

```text
GameBoot -> GameHub -> 全局 Module
```

但场景级和玩法级作用域还没有完整表达。当前 `GameSystemModule` 只能添加 System，不能移除，说明临时业务对象的所有权和退出流程仍不完整。

可以学习作用域思想，但不需要引入 DI 容器。轻量表达可以是：

```text
Application Scope
  |- Framework Modules
  `- Global Systems

Gameplay Scope
  |- UnitSystem
  |- NavigationSystem
  |- Gameplay UI
  `- Scene-owned leases and subscriptions
```

重点是进入和退出时所有权能够闭环，而不是增加新的 Scope 类名。

## 4. Installer：局部组合根

Installer 的本质是局部组合根。它把某项功能的创建和连接从总启动类中拆出去。

例如 SwipGunner 的 `UIModuleInstaller` 集中组装：

- UIManager；
- UIScreenNavigationService；
- 页面 Presenter；
- HUD；
- RocketLauncher UI；
- Reward 和 Rocket 配置。

这种方式的优点是总启动入口不会包含所有业务细节。缺点是 Installer 数量过多后，依赖链会分散在场景、Prefab、ScriptableObject 和注入方法中。

Tiny-Nations 可以吸收“按功能拆分组装代码”的思想，而不必复制 Installer 系统。随着 `GameBoot` 增长，可以先拆成清楚的普通方法：

```csharp
InstallFrameworkModules();
InstallGameplaySystems();
InstallUnitSystems();
InstallDebugTools();
```

所有调用仍由 `GameBoot` 决定，可以继续保持运行拓扑集中可见。

## 5. 功能纵向切片

SwipGunner 的 Feature 通常包含功能所需的完整代码。以 Enemy 为例，大致包含：

```text
EnemyModule/
|- Config
|- Behaviours
|- Damageable
|- Effects
|- Installer
|- MonoEntity
|- Spawn
`- Movement / Attack / Death
```

这是一种纵向切片：

```text
Enemy = 数据 + 行为 + 表现 + 配置 + 组装
```

它的主要价值是经常一起修改的代码位于同一个业务域，开发一个功能时不必在全局 `Configs`、`Services`、`States`、`Views` 等目录之间反复跳转。

Tiny-Nations 的 `Units` 已经形成类似的较大业务域：

```text
Units/
|- Configs
|- Entities
|- Skills
|- Effects
|- Events
`- Systems
```

这个方向适合 RTS。后续应继续按业务责任组织，但不需要让每个兵种重复拥有一套 Installer、Service 和 StateMachine。

可复用原则是：

> 经常一起修改的代码应尽量放在同一个业务域中。

## 6. 依赖注入：学习原则，不必照搬工具

SwipGunner 使用构造函数或 `[Inject]` 声明依赖。例如游戏流程状态需要场景服务、SignalBus 和 LoadingCurtain，容器负责寻找并传入实现。

Tiny-Nations 的 `UnitSystem` 和 `GameFlowSystem` 已经使用相同设计原则，只是依赖由 `GameBoot` 手动传入：

```csharp
new UnitSystem(
    resourceModule,
    prefabPoolModule,
    entityModule,
    inputModule,
    eventModule,
    monoModule,
    navigationSystem);
```

两种方式的本质区别是：

- SwipGunner 由容器完成对象图组装；
- Tiny-Nations 由应用组合根手动完成对象图组装。

真正值得保留的原则：

1. 类在构造时声明运行所需的依赖。
2. 类内部不要随意从全局入口寻找依赖。
3. 业务尽量依赖稳定接口。
4. 具体实现由最外层组合根选择。

Zenject 的优势是多作用域和大规模对象图的批量替换；代价是依赖来源更分散。当前 Tiny-Nations 继续使用手动组装更容易理解和维护。

## 7. 状态机

SwipGunner 将状态机用于游戏流程、玩家、敌人和触手。状态通过 Factory 延迟创建，可以拥有自己的注入依赖，并支持异步 `Enter` 和 `Exit`。

相关代码：

- [GameFlowStateMachine](../../../SwipGunner/Assets/KrolStudio/Content/Features/GameFlowStateMachineModule/Scripts/GameFlowStateMachine.cs)
- [StateMachine](../../../SwipGunner/Assets/KrolStudio/Core/StateMachineModule/Core/StateMachine.cs)

优点：

- 状态创建与状态机本身分离；
- 状态可以拥有不同服务依赖；
- 支持场景加载等异步流程；
- 状态实例可以延迟创建和缓存。

当前实现也暴露了异步状态机的典型风险：

- 切换时先修改 `currentState`，再执行 `Enter`；
- `Enter` 失败后没有恢复旧状态；
- `Dispose()` 调用异步 `Exit()` 时没有等待完成。

Tiny-Nations 当前同步 FSM 更简单，适合单位普通状态和简单业务流程。应学习的不是立即改成异步 FSM，而是明确以下契约：

- 进入失败后处于哪个状态；
- 退出是否完成后才允许进入下一状态；
- 重复进入当前状态如何处理；
- 状态由谁创建和释放；
- 状态依赖如何提供。

只有实际场景流程需要异步时，才应在对应业务层增加异步状态语义。

## 8. SignalBus 与事件系统

SwipGunner 使用 SignalBus 表达跨功能通知，例如：

- `GameplayStartedSignal`；
- `GameplayFinishedSignal`；
- `ShowScreenSignal`；
- `WalletChangedSignal`；
- `SettingsLoadedSignal`。

全局 Signal 集中声明在 [GlobalSignalsInstaller](../../../SwipGunner/Assets/KrolStudio/Content/Features/GlobalSignalsModule/Scripts/GlobalSignalsInstaller.cs)。

优点：

- 发布者不需要知道订阅者；
- 多个系统可以监听同一事实；
- Signal 类型形成统一消息契约。

代价：

- 控制流不再直观；
- Signal 过多时难以追踪发送者和接收者；
- Request 类型 Signal 容易把普通方法调用变成全局消息；
- Optional Subscriber 可能让漏接事件静默发生。

Tiny-Nations 的 `EventModule` 已经覆盖了相同基础能力。适合继续遵守以下边界：

- “伤害已经发生”“单位已经死亡”适合事件；
- “查找目标”“读取存档”“请求路径”更适合接口方法；
- 必须得到结果的操作不使用广播；
- 局部对象能够直接调用时，不升级为全局事件。

简化判断：

> 事件描述已经发生的事实，方法调用表达希望某个对象完成的命令或查询。

## 9. UI 组织

SwipGunner 使用 Presenter + View：

```text
View
  <- Presenter
    <- Service / Model / Signal
```

Tiny-Nations 使用 View + ViewModel：

```text
View
  <-> ViewModel
    -> 业务回调
```

两种方式目标相同：避免 View 同时承担显示、业务状态和流程控制。

SwipGunner 值得学习的部分：

- HUD 中相对独立的区域拥有自己的 Presenter；
- 页面导航独立为服务；
- View 不直接取得大量全局服务；
- Presenter 配对处理订阅和取消订阅。

Tiny-Nations 不需要为了形式完整同时建立 View、ViewModel、Presenter 和 Service。可以根据复杂度选择：

- 只有显示状态和按钮命令：ViewModel；
- 需要组合多个业务服务和事件：单独的协调对象或 Presenter；
- 简单 UI 不增加无实际职责的层级。

## 10. 持久化

SwipGunner 将持久化拆为：

```text
Data Model
  <-> Persistor
    <-> PlayerPrefs / JSON

Service
  <-> 游戏业务

Signal
  <-> 变化通知
```

钱包、玩家数据、设置和进度分别拥有自己的 Persistor。这个设计把以下职责分开：

- 数据结构；
- 数据保存与加载；
- 业务访问和修改；
- 数据变化通知。

Tiny-Nations 的 `SaveModule` 目前仍是占位实现。更合适的学习方式是在出现第一个正式存档需求后，从一个具体数据闭环开始，再确定：

- 谁可以修改数据；
- 在什么时间保存；
- 加载失败如何处理；
- 存档版本如何升级；
- 哪些变化需要通知其他系统。

不应在没有实际数据结构前照搬通用 Persistor 泛型和完整 Signal 体系。

## 11. 资源加载

SwipGunner 使用 Facade 屏蔽 Resources 与 Addressables：

```text
AssetLoaderFacade
|- ResourcesLoader
`- AddressablesLoader
```

它展示了一个合理原则：业务依赖资源接口，而不是直接依赖具体资源系统。

但当前实现存在以下问题：

- Installer 中多处同步等待 Addressables；
- 加载失败状态检查不足；
- 资源句柄释放依赖全局字符串分组；
- 释放 API 存在，但当前业务代码没有形成清楚的调用闭环。

Tiny-Nations 的 `ResourceModule + IAssetLease<T>` 对所有权表达更明确：

```text
LoadAsset
  -> 返回 Lease
    -> 持有者使用
      -> 持有者 Dispose
```

这一部分应继续保留 Tiny-Nations 的设计，只吸收“业务依赖抽象资源接口”的原则。

## 12. 对象池

SwipGunner 的对象池由 Factory 创建对象，再按具体业务类型建立池：

```text
ObjectFactory<T>
  -> PoolObject<T>
    -> InteractorPoolContainer
```

通用机制本身清楚，但具体敌人、触手、弹丸和伤害数字的池创建被放在 `Core` 下的 [GameplayPoolsInitializer](../../../SwipGunner/Assets/KrolStudio/Core/ObjectPool/Scripts/GameplayPoolsInitializer.cs)，导致通用层直接知道业务类型。

Tiny-Nations 当前边界更合理：

- BorFramework 提供通用 Prefab 实例池；
- UnitSystem 决定租用什么 Prefab；
- GameObject 可以复用；
- UnitEntity 和单位业务状态按每次租用重建。

可复用原则：

> 框架提供“如何池化”，业务决定“池化什么”。

## 13. 编译边界与测试

Tiny-Nations 的 BorFramework 有独立 asmdef，当前没有反向引用 GameLogic。这是真实的编译边界，而不仅是目录命名。

SwipGunner 的项目自有代码没有按 Core 和 Feature 建立 asmdef，主要编译进 `Assembly-CSharp`。因此：

- `Core` 可以直接引用具体业务类型；
- 目录无法阻止错误依赖；
- 修改任意业务代码可能触发更大范围编译；
- 模块独立测试和复用更困难。

两个项目当前都没有发现项目自有的自动化测试。接口数量、DI 容器和目录结构都不能替代当前行为验证。

## 14. 不建议直接学习的做法

以下做法不适合直接带入 Tiny-Nations：

### 14.1 完整引入 Zenject

当前项目规模下会增加 Context、Installer、Prefab 配置和调试成本。

### 14.2 每个单位一个 DI 子容器

SwipGunner 敌人数量有限。RTS 大量单位不适合默认使用 GameObjectContext、独立状态机对象图和各自的 MonoBehaviour `Update()`。

### 14.3 把所有交互改为 Signal

这会隐藏调用关系，尤其不适合查询和必须获得结果的命令。

### 14.4 为每个类创建接口

接口应代表真实替换边界、模块契约或测试边界，而不是为了形式上的解耦。

### 14.5 在 Installer 中同步加载资源

依赖组装和异步资源准备应当分开，避免启动或场景初始化过程中阻塞主线程。

### 14.6 只有目录分层，没有程序集边界

`Core` 和 `Features` 的名字不能阻止反向依赖。需要编译器约束时应使用 asmdef。

### 14.7 将具体业务初始化放入 Core

敌人池、触手池、具体 Prefab 地址和玩法配置应属于业务层。

## 15. 两套架构的职责映射

| SwipGunner | Tiny-Nations | 本质职责 |
| --- | --- | --- |
| ProjectContext | GameBoot + GameHub | 全局组合根 |
| SceneContext | 当前尚未完整表达 | 场景级作用域 |
| Installer | GameBoot 中的组装代码 | 创建和连接依赖 |
| `IInitializable` | `IModule.Init` / `IGameSystem.Init` | 初始化 |
| `IDisposable` | `Dispose` | 释放 |
| SignalBus | EventModule | 跨对象通知 |
| GameFlowStateMachine | GameFlowSystem + StateMachine | 游戏流程 |
| MonoEntity | UnitEntity | 游戏对象运行时抽象 |
| Presenter | ViewModel / 业务协调对象 | UI 表现逻辑 |
| Addressables Loader | ResourceModule | 资源加载 |
| PoolObject | PrefabPoolModule | 实例复用 |
| Persistor | 尚未实现的 SaveModule 业务 | 数据持久化 |

## 16. 对 Tiny-Nations 的学习方向

适合继续保留：

- 手动组合根；
- 构造函数注入；
- BorFramework 与 GameLogic 的程序集边界；
- `IAssetLease<T>` 资源所有权；
- Entity + Logic 的轻量单位模型；
- 简单、同步、契约明确的 FSM。

适合逐步吸收：

- 明确全局、场景和玩法生命周期；
- 按功能拆分 `GameBoot` 中的组装代码；
- 让订阅、帧回调和资源租约形成统一清理闭环；
- 根据 UI 复杂度分离 View、状态和业务协调；
- 从第一个真实存档结构开始建立持久化分层。

应避免：

- 过多 Installer 和容器配置；
- 每个单位建立 DI 容器；
- 全局 Signal 滥用；
- Core 混入业务；
- 没有真实替换需求的接口和抽象；
- 在实际问题出现前预建通用系统。

## 17. 最终总结

SwipGunner 的核心学习价值是：

> 展示一个功能较完整的游戏，如何通过作用域、局部组合根、依赖注入和纵向业务模块组织起来。

Tiny-Nations 当前框架的核心价值是：

> 使用较少机制维持清楚的框架与业务边界、显式依赖和资源所有权。

两者最合适的结合方式不是把 Tiny-Nations 改造成 SwipGunner，而是吸收 SwipGunner 的作用域、生命周期和完整业务切片思想，再继续使用 Tiny-Nations 当前更简单、透明、适合 RTS 的实现方式。
