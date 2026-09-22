# BorFramework 当前结构整理与验收边界

本文记录当前源码中的结构和生命周期契约，替代此前已过期的风险清单。详细接入方式见 [框架总结](FrameworkOverview.md)，公开 API 见 [框架导航](FrameworkNavigation.md)。

**验收状态：本次进行了源码与调用链检查，未运行编译、Play Mode、性能测试或 UI 视觉验收。下面的实现描述不代表对应运行场景已经通过。**

## 1. 当前分工

```text
GameBoot：应用组合与依赖注入
  ├─ BorFramework：资源、场景、UI、帧循环、事件与生命周期
  └─ GameLogic
       ├─ GameFlowSystem：菜单与 Demo 状态
       ├─ NavigationSystem：导航服务
       └─ UnitSystem：单位生成、查询与回收
            └─ UnitEntity：直接装配状态和 Logic
```

框架不引用具体游戏业务。GameBoot 按依赖顺序创建模块，再按 Navigation、Unit、GameFlow 顺序安装全程业务系统。存档与配置的空壳模块及启动注册已移除；当前没有相应的通用业务 API。

Entity 的线性装配仍然保留。Component 直接保存在具体实体中并通过构造函数传给 Logic；没有为装配添加 Builder、工厂或依赖注入容器。

## 2. 已落地的契约

### 2.1 Entity 与 Logic

源码：[Entity](../Scripts/BorFramework/1_Core/ELC/Entity/Entity.cs)、[Logic](../Scripts/BorFramework/1_Core/ELC/Logic/Logic.cs)、[EntityModule](../Scripts/BorFramework/2_Module/EntityModule/EntityModule.cs)。

- Entity 仅保留 Logic 注册与顺序，不再保存 Component 查询容器，也不再提供 AddComp、GetComp、GetLogic。
- AddLogic 仅能在首次启动前调用，重复运行时类型返回 false；按 Phase 排序，同 Phase 保留装配顺序。
- Logic 的公开 OnUpdate、Stop、Dispose 由基类固定控制；子类扩展 OnStart、OnTick、OnStop、OnDispose。
- Stop 对正在运行的 Logic 调用一次 OnStop，下一次合法更新可重新启动；Dispose 先停止再永久释放，重复调用无效，释放后不再更新。
- Entity.Dispose 按逆序停止和释放行为；EntityModule.Stop 同样停止所属实体，之后 Start 可恢复尚未释放的实体。
- AddEntity 成功接收实体所有权；空、重复、待移除或已释放实体返回 null。更新期间添加的实体进入待加入列表，从下一帧开始更新。
- RemoveEntity 立即标记待移除，阻止目标继续执行后续 Logic；更新期间在本批更新结束后释放，更新外立即释放。
- `RemoveEntity(entity, onRemoved)` 成功释放后才调用 onRemoved。与实体绑定的 GameObject 归还池、资源租约释放使用该回调，避免逻辑仍在执行时先收走 Unity 对象。

### 2.2 业务 System 所有权

源码：[IGameSystemModule](../Scripts/BorFramework/2_Module/GameSystemModule/IGameSystemModule.cs)、[GameSystemModule](../Scripts/BorFramework/2_Module/GameSystemModule/GameSystemModule.cs)。

- AddSystem 返回 bool；成功后由模块持有系统，失败时所有权仍在调用方。
- 空对象、相同注册类型、重复实例或已释放模块会拒绝注册。
- 按模块当前状态补调 Init 和 Start；整体生命周期仍按注册顺序启动、逆序停止和释放。
- RemoveSystem 按注册类型删除，先 Stop 再 Dispose，未注册返回 false。

这些接口提供了明确的场景级 System 退出路径；当前全程 Navigation、Unit、GameFlow 仍由 GameBoot 统一安装，不为此额外创建通用场景容器。

### 2.3 UI 导航与异步结果

源码：[IUIModule](../Scripts/BorFramework/2_Module/UIModule/IUIModule.cs)、[UIModule](../Scripts/BorFramework/2_Module/UIModule/UIModule.cs)。

- OpenAsync 对 Screen 和 Window 转入各自导航 API；其他层直接打开。
- PushScreenAsync 在加载成功后暂停并隐藏旧页面、关闭窗口、将新页面入栈。栈中已有同类型页面时返回 null。
- OpenWindowAsync 要求当前 Screen 存在，同类型 Window 已在栈中时返回 null。
- 导航版本和 View 打开版本用于淘汰过期请求；Window 还检查请求所属页面。等待期间返回、关闭、停止或发起后续页面导航，不应再由旧请求显示 UI。
- PopScreen 恢复前一页面时调用 OnResume 并重新显示 GameObject；暂停隐藏不结束打开生命周期，因此 IsOpen 不能作为可见性判断。
- 同一 View 类型保留单个缓存实例和 ViewModel；Close 解绑、关闭但保留缓存，Destroy 再释放实例、ViewModel、租约并取消注册。
- 新打开的 View 移到所属子层最后一个 sibling。UI Stop 关闭全部条目、清空栈并使等待中的打开请求失效。

### 2.4 主菜单与 Demo

源码：[GameFlowSystem](../Scripts/GameLogic/GameFlow/GameFlowSystem.cs)、[MainMenuState](../Scripts/GameLogic/GameFlow/States/MainMenuState.cs)、[DemoState](../Scripts/GameLogic/GameFlow/States/DemoState.cs)。

MainMenuState 通过进入版本过滤旧异步结果；退出时销毁菜单注册，重新进入时可利用已加载场景重新创建菜单。GameFlowSystem Stop 后再次 Start 会重新进入主菜单。

菜单场景与 View 均准备完成后，EnterDemo 才接受请求。其 bool 返回值表示状态切换请求被接受，不表示 Demo 场景和玩家已经创建完成。Demo 加载或复用场景后直接生成玩家；场景加载或玩家生成失败时记录错误并返回主菜单。过期生成请求若拿到单位会将其回收，退出 Demo 时回收所持玩家单位。NavigationSystem 通过场景加载与卸载事件管理 Tilemap 导航地图。

## 3. 保留的范围和限制

| 边界 | 当前含义 |
| --- | --- |
| FrameworkReadyEvent | 表示模块启动流程已执行，不保证 YooAsset、首个场景或菜单已就绪。 |
| ELogicPhase | 只在一个 Entity 内排序；仍逐个 Entity 更新，未实现跨实体全局阶段调度。 |
| UI 类型唯一 | 每种 View 类型只有一份缓存与一个导航位置，重复入栈被拒绝；没有通用多实例 UI ID。 |
| Prefab 池 | 只复用 GameObject；单位属性、技能等运行时状态按生成重建，由业务管理。 |
| 场景与池实例 | 无父节点的租出实例保留在池的常驻场景中；业务退出时必须主动归还。 |
| 失败恢复 | 当前有日志、失败返回值和 Demo 回菜单路径，没有通用资源修复或失败提示页面。 |
| 公开旧名称 | Waring、OnFixUpdate、DefultInputSystem 仍按已有名字使用；此次未修改生成的 Input System 代码。 |

后续是否需要全局阶段、存档、更多 UI 实例或性能优化，应由实际玩法和运行证据决定。上述边界不自动构成必须扩展的新系统。

## 4. 待人工验收清单

以下项目均待用户在当前 Unity 工程中验证，不把源码静态检查标为通过。

### 生命周期与更新安全

- [ ] 编译无新增错误。
- [ ] Entity 在自己的 Tick 中请求移除后不再执行后续 Logic；释放和回收回调各执行一次。
- [ ] Entity A 删除尚未更新的 B 时，B 当帧不再执行；更新中添加的实体从下一帧开始。
- [ ] RemoveEntity 在更新外同步完成，更新内延迟完成；宿主对象只在完成回调内回收。
- [ ] EntityModule Stop/Start 后行为恢复且没有重复订阅；Dispose 后不会再次启动或更新。
- [ ] Logic 阻塞、解除阻塞、Stop、Dispose 的调用顺序与次数符合契约。
- [ ] System 重复类型/实例注册失败不丢失候选对象所有权；RemoveSystem 只停止并释放已注册对象。

### UI 导航

- [ ] A 页面打开 B 后 A 隐藏且不接收点击；返回后 A 显示并恢复交互。
- [ ] 重复打开栈内同类型 Screen 或 Window 返回 null，不产生重复栈记录。
- [ ] Screen/Window 加载中执行 Back、Close、Destroy、Stop 或切换页面，完成后的旧请求不重新弹出 UI。
- [ ] 从 A 发起的 Window 不会在已切换到 B 后显示；加载失败保留原页面。
- [ ] Close 与 Destroy 分别正确处理绑定、ViewModel 和资源租约。

### 业务流程与资源

- [ ] 正常启动菜单可用，只有菜单就绪后接受进入 Demo 请求。
- [ ] GameFlowSystem Stop/Start 后菜单重新出现，按钮仍可进入 Demo。
- [ ] Demo 场景加载或玩家生成失败后回到菜单，错误日志可定位失败地址。
- [ ] 状态退出后的异步结果不继续创建界面或遗留单位。
- [ ] 单位回收、再次生成后的 Animator、朝向、材质与物理速度不继承上次状态；运行时数据重新创建。
- [ ] 框架退出时没有遗留监听、实体、池实例和资源租约。

验收若发现偏差，优先修正对应所有权或状态边界，再决定是否需要新的抽象。
