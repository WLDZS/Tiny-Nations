# BorFramework 技术评审与演进建议

> 本文面向框架维护者和业务开发人员，基于当前工作区代码进行静态评审。它描述的是能够由现有实现推导出的风险、建议的最小改造方案及验收标准。框架结构与 API 入口另见 [框架导航与 API 索引](FrameworkNavigation.md)。

## 1. 评审结论

BorFramework 当前已经形成一套可用的轻量运行时骨架：

- `GameHub` 统一管理模块注册和生命周期；
- 业务通过 `I...Module` 接口使用服务；
- 模块间依赖主要由构造函数显式传入；
- `IAssetLease<T>` 明确表达资源引用的释放责任；
- `GameSystem`、ELC、FSM 和 UI MVVM 分别覆盖业务生命周期、对象行为、状态切换与界面绑定。

当前主要问题不在模块数量，而在生命周期闭环和异步边界。进入、运行、退出、再次进入同一玩法后，一些对象无法从全局模块移除；实体在更新中修改集合可能破坏本帧执行；UI 异步加载完成时可能已经失去原导航上下文。

建议先修复这些确定的运行时风险，再继续实现 `SaveModule`、`ConfigModule` 或增加新的抽象层。

## 2. 优先级总览

| 优先级 | 问题 | 直接影响 | 建议处理阶段 |
| --- | --- | --- | --- |
| P0 | `GameSystem` 只能添加，不能移除 | 重进场景后旧系统残留，新系统注册失败，资源租约可能泄漏 | 第一阶段 |
| P0 | Entity 更新期间允许立即增删 | 跳帧、重复更新或集合枚举失效 | 第一阶段 |
| P1 | Logic 的停止与释放没有统一闭环 | 监听或运行状态可能残留 | 第一阶段 |
| P1 | UI 异步打开缺少导航上下文校验 | 页面退出后窗口仍可能打开 | 第二阶段 |
| P1 | UI 栈状态和视觉状态未完全绑定 | 暂停页面仍可显示或交互，栈顶顺序不稳定 | 第二阶段 |
| P1 | `FrameworkReadyEvent` 不代表资源已经可用 | Ready 之后同步资源访问仍可能失败 | 第三阶段 |
| P1 | Hub 注册失败无结果，允许注册 `null` | 错误延迟到生命周期调用时暴露 | 第一阶段 |
| P2 | Logic Phase 只在单个 Entity 内排序 | 跨实体行为存在本帧与上一帧数据混用风险 | 玩法验证后 |
| P2 | 多处失败只返回 `null` / `false` | 调用方难以定位失败原因 | 按调试需求渐进补充 |
| P2 | API 命名存在拼写债务 | 降低可发现性，未来修正会形成破坏性修改 | 进入稳定版本前 |

P0 表示在常见生命周期中可能造成错误状态或资源无法回收；P1 表示在异步操作、导航和错误配置下容易出现问题；P2 表示当前可以继续使用，但应明确语义或在框架稳定前清理。

## 3. GameSystem 生命周期不完整

相关代码：

- [`GameSystemModule.AddSystem`](../Scripts/BorFramework/2_Module/GameSystemModule/GameSystemModule.cs)
- [`IGameSystemModule`](../Scripts/BorFramework/2_Module/GameSystemModule/IGameSystemModule.cs)
- [`GameFlowSystem`](../Scripts/GameLogic/GameFlow/GameFlowSystem.cs)
- [`GameBoot`](../Scripts/GameLogic/GameBoot.cs)

### 3.1 当前行为

`GameSystemModule` 使用系统类型作为唯一键。`AddSystem<T>` 遇到重复类型时直接返回，但接口没有返回注册是否成功，也没有删除系统的 API。

`GameSystemModule` 随 `GameBoot` 常驻。当前 `GameFlowSystem` 在模块启动前由 `GameBoot` 加入，本身也是全程系统，因此生命周期能够闭合；但接口仍无法安全托管只属于单个场景或玩法阶段的临时 System。若业务直接调用 `AddSystem` 注册这类对象，可以形成以下流程：

```text
首次进入玩法场景
  -> 创建场景级资源租约
  -> 注册并启动 SceneGameSystem

退出玩法场景
  -> 场景入口销毁
  -> SceneGameSystem 仍由全局 GameSystemModule 持有

再次进入玩法场景
  -> 再次创建资源租约
  -> AddSystem 因同类型已存在而静默失败
  -> 新租约没有被系统接管，也没有被调用方释放
```

旧系统仍会持有上一场景实例、模块引用和资源租约。即使 Unity 已销毁场景对象，系统本身也要等整个框架退出才会 `Dispose()`。

### 3.2 建议接口

保持按类型注册的现有设计，只补充成功结果和删除能力：

```csharp
public interface IGameSystemModule : IModule
{
    bool TryAddSystem<T>(T system) where T : class, IGameSystem;
    bool RemoveSystem<T>() where T : class, IGameSystem;
    T GetSystem<T>() where T : class, IGameSystem;
}
```

建议语义：

- `TryAddSystem` 返回 `true` 后，系统的生命周期由模块接管；
- 返回 `false` 时，所有权仍属于调用方，调用方负责释放传入系统及其资源；
- `RemoveSystem<T>` 先 `Stop()`，再 `Dispose()`，最后从字典和顺序表删除；
- 删除不存在的系统返回 `false`；
- 模块运行期间新增系统，仍沿用当前“立即补调 `Init()` 和 `Start()`”的行为。

场景级业务入口销毁时应移除自己注册的 System。若以后同一类型需要多个实例，再单独设计实例 ID；当前没有必要提前引入复杂作用域容器。

### 3.3 验收标准

- 连续进入和退出同一玩法场景三次，每次都能创建并启动新的场景级 System；
- 每次退出后都不存在旧场景对象、旧系统和旧资源租约；
- 重复注册返回 `false`，调用方可以立即释放未被接管的对象；
- `RemoveSystem<T>` 多次调用不会重复执行 `Dispose()`。

## 4. Entity 更新期间修改集合不安全

相关代码：

- [`EntityModule.OnUpdate`](../Scripts/BorFramework/2_Module/EntityModule/EntityModule.cs)
- [`EntityModule.RemoveEntity`](../Scripts/BorFramework/2_Module/EntityModule/EntityModule.cs)
- [`Entity.Tick`](../Scripts/BorFramework/1_Core/ELC/Entity/Entity.cs)

### 4.1 当前风险

`EntityModule.OnUpdate` 正向遍历 `_entities`。如果 Entity A 在更新中调用 `RemoveEntity(A)`：

- A 会立即从列表删除；
- 后续元素向前移动，循环索引递增后可能跳过 Entity B；
- `RemoveEntity` 立即调用 A 的 `Dispose()`；
- A 的 `Dispose()` 清空 `_logicOrder`，而 `Entity.Tick()` 可能正在遍历该列表。

同样，更新期间新增实体会改变当前列表长度，使新实体是否在本帧执行取决于加入时机。这会让帧语义不稳定。

### 4.2 建议方案

在 `EntityModule` 内增加“更新中”状态和待处理队列：

```text
开始更新
  -> 标记 isUpdating
  -> 只遍历本帧开始时的有效实体
结束更新
  -> 取消 isUpdating
  -> 先处理待移除实体
  -> 再处理待新增实体
```

建议规则：

- 更新期间调用 `RemoveEntity` 只标记移除，不立即修改主列表；
- 已标记移除的实体不再执行本帧剩余 Logic；
- 更新期间新增的实体从下一帧开始 Tick；
- 拒绝添加 `null` 和重复实体；
- 同一实体重复移除只执行一次 `Dispose()`；
- `EntityModule.Dispose()` 同时处理主列表和待处理队列。

这个改动只需要列表、集合和一个 `_isUpdating` 标记，不需要引入命令总线或通用调度系统。

### 4.3 验收标准

- Entity 在自身 Logic 中删除自己，不抛出集合修改错误；
- 自删除不会导致下一个 Entity 跳过本帧；
- 同一帧新增的 Entity 从下一帧开始更新；
- 重复添加或重复删除不会产生重复 Tick 或重复 Dispose。

## 5. Logic 缺少停止与释放配对

相关代码：

- [`Logic.OnUpdate`](../Scripts/BorFramework/1_Core/ELC/Logic/Logic.cs)
- [`Logic.Dispose`](../Scripts/BorFramework/1_Core/ELC/Logic/Logic.cs)
- [`Entity.Dispose`](../Scripts/BorFramework/1_Core/ELC/Entity/Entity.cs)

### 5.1 当前行为

Logic 首次更新时调用 `OnStart()`。运行中的 Logic 只有在进入阻塞状态后才自动调用 `OnStop()`；Entity 释放时直接调用 Logic 的 `Dispose()`。

这使下面这种业务实现容易留下订阅：

```csharp
public override void OnStart()
{
    eventModule.Subscribe<SomeEvent>(OnEvent);
}

public override void OnStop()
{
    eventModule.Unsubscribe<SomeEvent>(OnEvent);
}
```

如果 Entity 在 Logic 正常运行时被删除，框架不会保证先调用 `OnStop()`。

### 5.2 建议方案

由 `Logic` 基类统一维护运行状态，并提供框架内部使用的停止入口：

- 未运行时停止不执行任何回调；
- 正在运行时停止只调用一次 `OnStop()`；
- `Dispose()` 前确保已经停止；
- 阻塞仍保持计数语义；
- 解除最后一层阻塞后，下一次更新重新调用 `OnStart()`。

`EntityModule.Stop()` 是否停止所有 Logic 需要明确。如果 Stop 表示整个框架暂停，则应停止；如果仅表示暂时取消帧订阅并预计继续运行，则可以保留 Logic 状态，但必须在文档中固定这一语义。

### 5.3 Component 所有权也应明确

`Entity.AddComp<T>` 当前只写入字典，没有设置 `comp.Entity`。`Entity.Dispose()` 清空 Logic，但没有清空 `_comps`。

建议采用以下简单规则：

- 添加 Component 成功时由 Entity 设置 `comp.Entity = this`；
- 相同类型重复添加返回 `false`；
- Entity 释放时把所有 Component 的 `Entity` 置空并清空集合；
- `CompMono` 所在 GameObject 是否由 Entity 销毁，应由具体 Entity 负责，框架只处理引用关系；
- `Entity.Go` 建议提供只读公开属性，避免每个子类重复暴露另一份 `GameObject` 属性。

## 6. UI 异步请求与导航状态存在竞争

相关代码：

- [`UIModule.OpenAsync`](../Scripts/BorFramework/2_Module/UIModule/UIModule.cs)
- [`UIModule.PushScreenAsync`](../Scripts/BorFramework/2_Module/UIModule/UIModule.cs)
- [`UIModule.OpenWindowAsync`](../Scripts/BorFramework/2_Module/UIModule/UIModule.cs)
- [`UIViewModelBase`](../Scripts/BorFramework/2_Module/UIModule/Base/UIViewModelBase.cs)

### 6.1 窗口可能在所属页面退出后打开

`OpenWindowAsync` 只在开始加载前检查 Screen 栈。资源加载期间如果用户返回并关闭当前 Screen，加载完成后仍可能显示这个 Window 并压入窗口栈。

建议为导航上下文增加简单版本号：

1. 开始导航请求时记录当前 Screen 类型和导航版本；
2. `await` 返回后再次检查 Screen、版本和请求状态；
3. 上下文已经变化时取消显示；
4. 已加载的 View 可以保留缓存，也可以按策略销毁，但不能加入失效的导航栈。

### 6.2 重复打开栈中已有的 Screen 语义不明确

`PushScreenAsync<T>` 在目标类型已经存在于 `_screenStack` 时直接返回。若它位于栈中但不是栈顶，该页面可能仍处于 Pause 状态，调用者却拿到了一个非空 View。

应明确选择一种行为：

- 当前项目更适合“同一 Screen 类型在栈中唯一”；
- 再次导航到已有 Screen 时，将其上方页面关闭并恢复它；
- 如果业务需要同类型多实例，再改用实例 ID，不应让当前 API 隐式支持。

### 6.3 栈状态应同步到 View 状态

当前压入新 Screen 时只调用旧 ViewModel 的 `OnPause()`，没有控制旧 View 的可见性、Raycast 或 sibling 顺序。

建议由 UI 模块定义统一行为：

- 全屏 Screen 被覆盖时隐藏或禁用交互；
- 恢复时恢复可见性或交互，再调用 `OnResume()`；
- Window 入栈时移动到对应子层的最后一个 sibling；
- Screen 和 Window 的栈顶必须与视觉最上层一致；
- `Popup` 与 `GlobalOverlay` 通过 `OpenAsync` 管理，不混入 Screen / Window 栈。

### 6.4 加载失败后的状态需要复位

Prefab 缺少指定 View 组件或 ViewModel 创建失败时，应该保证：

- 请求状态复位；
- 实例和租约被释放；
- 后续修复资源后可以重新打开；
- 日志包含 View 类型、address 和失败阶段。

## 7. 框架启动状态表达不准确

相关代码：

- [`GameBoot.Start`](../Scripts/GameLogic/GameBoot.cs)
- [`ResourceModule.Init`](../Scripts/BorFramework/2_Module/ResourceModule/ResourceModule.cs)
- [`FrameworkReadyEvent`](../Scripts/BorFramework/2_Module/EventModule/FrameworkReadyEvent.cs)

`GameBoot` 在所有模块执行同步 `Start()` 后发布 `FrameworkReadyEvent`。此时 `ResourceModule` 的 YooAsset 初始化任务可能仍在运行，因此框架 Ready 并不代表资源 Ready。

建议把状态拆成两个清楚的阶段：

| 阶段 | 含义 | 可执行操作 |
| --- | --- | --- |
| ModulesStarted | 模块已经注册、Init、Start，帧循环可用 | 订阅事件、读取模块状态 |
| StartupReady | 启动必需的资源包和基础数据准备成功 | 进入首个业务场景、同步资源读取 |

最小实现方式是让启动协调代码等待 `ResourceModule` 到达 `Ready` 或 `Failed`，成功后再加载首个场景。`IModule` 暂时可以保持同步接口，不必把所有模块统一改为异步生命周期。

还应定义初始化失败后的用户路径：记录错误、停留在 Boot 场景、显示重试或退出入口。仅将状态设为 `Failed` 对实际产品流程还不够。

## 8. GameHub 的注册契约应收紧

相关代码：[`GameHub`](../Scripts/BorFramework/1_Core/Hub/GameHub.cs)

当前 `RegisterModule<T>` 存在几个模糊点：

- 没有检查 `module == null`，但生命周期遍历默认所有元素非空；
- 重复注册静默失败；
- Hub 启动后继续注册的模块不会自动补调 `Init()` 和 `Start()`；
- `GetModule<T>` 必须使用注册时完全相同的泛型类型，调用方无法判断是未注册还是类型使用错误。

建议改为 `bool TryRegisterModule<T>(T module)`，并固定以下契约：

- `null`、重复类型和 Hub 未初始化时返回 `false` 并记录明确日志；
- 框架模块只允许在 `StartModules()` 前注册；
- 启动后不再开放动态模块注册；
- 业务运行期的动态对象由 `GameSystemModule`、`EntityModule`、`UIModule` 管理。

这样能保持模块拓扑稳定，也避免同时维护两套“运行中注册后补生命周期”的逻辑。

## 9. ELC Phase 的真实语义

相关代码：

- [`ELogicPhase`](../Scripts/BorFramework/1_Core/ELC/Logic/ELogicPhase.cs)
- [`Entity.AddLogic`](../Scripts/BorFramework/1_Core/ELC/Entity/Entity.cs)
- [`EntityModule.OnUpdate`](../Scripts/BorFramework/2_Module/EntityModule/EntityModule.cs)

Logic 会在单个 Entity 内按 Phase 排序，但 EntityModule 的外层循环仍然逐个更新完整 Entity：

```text
Entity A: Input -> Movement -> Combat
Entity B: Input -> Movement -> Combat
```

它并不保证以下全局阶段：

```text
所有 Entity Input
-> 所有 Entity Movement
-> 所有 Entity Combat
```

这不一定是缺陷，但必须作为框架契约写清楚。跨实体战斗结算若依赖“所有对象移动完成后的最终位置”，更适合由独立 `CombatSystem` 收集和统一结算。

在实际玩法证明需要全局 Phase 前，不建议把 EntityModule 改造成复杂的多阶段调度器。

## 10. 资源与对象所有权约定

框架已经通过 `IAssetLease<T>` 开始表达所有权，建议把这套规则扩展成明确约定：

| 对象 | 创建者 | 生命周期持有者 | 释放入口 |
| --- | --- | --- | --- |
| 框架 Module | `GameBoot` | `GameHub` | `GameHub.DisposeModules()` |
| `IGameSystem` | 业务启动代码 | 注册成功后由 `GameSystemModule` | `RemoveSystem<T>()` 或模块 Dispose |
| Entity | 业务 System | 注册成功后由 `EntityModule` | `RemoveEntity()` 或模块 Dispose |
| 普通资源租约 | 调用加载的业务对象 | 接收 `IAssetLease<T>` 的对象 | `IAssetLease<T>.Dispose()` |
| UI Prefab 租约 | `UIModule` | `UIEntry` | `Destroy<TView>()` 或模块 Dispose |
| Scene Handle | `SceneModule` | `SceneModule` | `UnloadSceneAsync()` 或资源系统整体退出 |
| 实例化 GameObject | System / Entity / UI 模块 | 创建它的一方 | 对应业务 Dispose / UI Destroy |

所有 API 都应满足一个原则：所有权转移成功必须可以判断；转移失败时，原持有者仍负责释放。

## 11. 错误处理与可观测性

当前框架遵循 `null`、`bool`、状态枚举和日志处理失败，方向与项目约定一致。建议在不增加异常流程的前提下逐步改进：

- `Register`、`AddSystem`、`AddEntity` 等可能失败的写操作返回 `bool`；
- 高频查询失败可以只返回 `false`，配置错误和生命周期错误应记录日志；
- 需要区分多个失败原因时使用小型状态枚举，例如 `NotReady / Busy / InvalidAddress / LoadFailed`；
- 日志至少包含模块名、操作、资源 address 或目标类型；
- 避免在每帧失败路径重复输出相同警告；
- 为 `ResourceModule.State == Failed` 提供可读取的最后错误信息，方便 Boot UI 展示。

不建议现在引入通用 Result 框架。只有当多个模块确实需要相同错误结构时，再提取共用类型。

## 12. API 命名与目录债务

以下名称已经进入公开接口，建议在框架稳定版本前处理：

| 当前名称 | 建议名称 | 说明 |
| --- | --- | --- |
| `ILogModule.Waring` | `Warning` | 英文拼写错误 |
| `IMonoModule.OnFixUpdate` | `OnFixedUpdate` | 与 Unity `FixedUpdate` 对齐 |
| `DefultInputSystem` | `DefaultInputSystem` | 资源名与生成类型拼写错误；应从 `.inputactions` 资产重命名并重新生成代码 |
| `GameHub.Ins` | 可暂时保留 | 虽然 `Instance` 更常见，但修改收益较小且影响所有调用点 |

Input System 生成的 `.cs` 文件不能手动改名或编辑，应修改源 `.inputactions` 资产后由 Unity 重新生成。

此外，[`IHub`](../Scripts/BorFramework/1_Core/Hub/IHub.cs) 当前位于全局命名空间，而实现和其余接口位于 `BorFramework`。建议将其放回 `BorFramework` 命名空间，避免 API 组织不一致。

## 13. 建议实施路线

### 第一阶段：修复生命周期闭环

目标是让同一玩法可以安全地反复进入和退出。

1. 为 `GameSystemModule` 增加可判断的注册结果和删除 API；
2. 让场景级业务入口在退出时移除自己的 System；
3. 为 Entity 增加延迟增删；
4. 统一 Logic 的停止与释放顺序；
5. 完善 Component 的 Entity 绑定和清理；
6. 收紧 `GameHub` 的注册契约。

### 第二阶段：固定 UI 导航语义

目标是让异步加载、快速返回和重复导航得到确定结果。

1. 增加导航请求版本或所属 Screen 校验；
2. 明确同类型 Screen 的唯一性和回退行为；
3. 同步 Screen Pause 与 View 可见、交互状态；
4. 统一 Window 层级顺序；
5. 验证加载失败、加载中关闭、加载中销毁。

### 第三阶段：整理启动流程

目标是让“可以进入游戏”的状态只有一个明确含义。

1. 区分 ModulesStarted 与 StartupReady；
2. 等待资源初始化成功后再加载首个业务场景；
3. 为初始化失败提供重试或退出路径；
4. 补充必要的启动状态日志。

### 第四阶段：按真实需求扩展

- 有首个实际存档结构后实现 `SaveModule`；
- 有表格或 ScriptableObject 配置需求后实现 `ConfigModule`；
- 玩法证明需要跨 Entity 全局阶段时再升级 ELC 调度；
- 性能分析证明资源重复加载有成本时再增加缓存策略；
- 项目进入 API 稳定期前统一公开名称。

## 14. 回归验证清单

这些场景覆盖框架当前最关键的边界：

### 启动与资源

- 正常启动后只创建一个 `GameBoot` 和一个 `UIRoot`；
- YooAsset 初始化成功后才进入首个业务场景；
- 资源初始化失败时停留在可处理状态，不继续加载场景；
- 框架销毁期间仍在等待的异步请求能够安全结束。

### System 与 Entity

- 场景进入、退出、再次进入后不会保留旧 System；
- System 注册失败时，调用方可以释放尚未转移的资源；
- Entity 在自身更新中删除自己不会破坏遍历；
- Entity A 删除 Entity B 时，B 不再执行不应执行的 Logic；
- Logic 的 `OnStart / OnStop / Dispose` 顺序稳定且不重复。

### UI

- 连续快速打开同一个 UI 只产生一个实例和一个资源租约；
- UI 加载中调用 Close，加载完成后不会显示；
- Screen 退出后，其加载中的 Window 不会再打开；
- 重复导航到栈内已有 Screen 的行为符合约定；
- Back 始终先关闭栈顶 Window，再返回上一个 Screen；
- Destroy 后 ViewModel、实例和资源租约都被释放。

### 事件与输入

- ViewModel 多次打开和关闭后不会重复订阅事件；
- System Stop 后不再收到帧更新；
- 不存在的 Input Action 返回安全默认值，并且不会每帧刷屏；
- 框架 Dispose 后 Event 和 Mono 监听全部清空。

## 15. 暂不建议引入的设计

现阶段以下设计会增加维护成本，但还没有对应的实际需求证明其价值：

- 自动扫描和反射注册所有模块；
- 完整依赖注入容器；
- 为所有操作统一创建复杂 Result 泛型；
- 通用对象池、资源缓存和引用计数层叠封装；
- 同类型 UI、System 或 Entity 的通用多实例 ID 系统；
- 全局多阶段 ECS 调度器；
- 为占位模块预先设计通用序列化协议。

当前框架最合适的演进方式是保持接口小、生命周期明确、失败结果可判断，并由真实业务需求推动下一步扩展。
