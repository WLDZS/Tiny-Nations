# Tiny Nations Unit 预制体制作规范

规范版本：`1.13`

本文规定当前项目中可由 `UnitSystem` 加载、由 ELC 驱动并出现在单位调试面板中的 Unit 资产应如何制作。
它是 Unit 资产制作的唯一事实来源；项目级 Skill 只负责引导 AI 按本文执行，不复制本文内容。

当前可用基准：

- `WarriorBlue`：两段普通攻击动画与持续防御动画。
- `Skull`：单段普通攻击动画与持续防御动画。

## 1. 适用范围

当新增或修改以下任一内容时使用本规范：

- Unit Prefab；
- Unit Animator Controller 与动画状态；
- `UnitDefinition`；
- 单位属性 `UnitAttributeSetConfig`；
- 单位使用的技能 `ScriptableObject`；
- YooAsset 单位收集结果；
- Unit 调试生成流程。
- Unit 的 `Rigidbody2D`、身体碰撞与 Hurtbox 配置；
- 近战查询范围、命中窗口与伤害 `GameEffect`。

本文描述当前源码的运行时契约。当前实现运行时 TeamId、Self/Ally/Enemy 关系、基于场景 Tilemap 的静态导航、基础近战自动战斗和死亡回收；外交、死亡动画、尸体、复活与完整 RTS 命令系统尚未实现。结构整理后的运行与视觉结果仍需在 Unity 中验收。

## 2. 核心边界

1. Unit Prefab 是纯表现对象，不挂项目自有的 `MonoBehaviour`、Unit Controller 或 Unit View。
2. `UnitSystem` 负责按 `UnitDefinition` 地址加载配置、从 Prefab 池租用表现对象、创建 `UnitEntity`，并在 Despawn 时归还表现对象和释放 UnitDefinition 租约。
3. `UnitEntity` 组装通用 Comp 与 Logic；不要为 Warrior、Skull 等具体兵种新增专属 Entity、Comp、Logic 或 Controller。
4. 单位差异优先由 `UnitDefinition`、属性 SO、技能 SO、Animator Controller 和动画资源表达。
5. 一个 C# 文件只放一个类型。不要把运行时辅助类型写成另一个类的嵌套类型。
6. 如果当前通用配置无法表达新单位需求，先指出缺失的通用能力及最小扩展方案；不要直接把兵种特例写进通用系统。
7. 属性 SO 只保存共享初始配置；每个生成的单位必须持有独立的运行时属性值，禁止修改共享 SO 表示掉血、耗蓝或临时 Buff。
8. 战斗队伍是 Unit 实例的运行时数据，由 `UnitSpawnRequest.TeamId` 传入；不要把队伍写死在 Prefab 或 `UnitDefinition` 中，也不要用 Layer 或 Tag 表达敌我关系。
9. 单位存活状态由通用 `UnitLifeComp` 保存。致死伤害发布一次 `UnitDeathEvent`，停止输入、移动和技能，并由 `UnitSystem` 在当帧 LateUpdate 安全回收；不要在伤害回调中重入销毁 Entity。
10. 不使用玩家输入且 Primary 槽为 `MeleeAttackSkill` 的 Unit 会获得基础近战 AI；AI 与伤害结算必须复用同一个近战查询框，不维护独立的攻击距离。
11. Component 保存状态与局部不变量；`SkillLogic` 驱动技能更新，`UnitGameEffectLogic` 执行效果计时、伤害结算和事件发布。`UnitEntity` 直接装配依赖，仅注册需要帧调度的 Logic。

## 3. 每个 Unit 的交付物

一个可生成 Unit 至少包含：

```text
Assets/GameAsset/
├─ GameEffects/
│  └─ <EffectName>.asset
└─ Units/
   ├─ Definitions/
   │  └─ <UnitName>Definition.asset
   └─ <UnitType>/
      ├─ <UnitType>Attributes.asset
      └─ [<Variant>/]
         ├─ Animations/
         │  ├─ <UnitName>.controller
         │  └─ <UnitName><Action>.anim
         ├─ SkillConfigs/
         │  └─ <UnitName><SkillName>.asset
         ├─ <UnitName>.prefab
         └─ 动画使用的 Sprite 或 Sprite Sheet
```

- 有颜色或皮肤变体时使用 `Units/<UnitType>/<Variant>`，例如 `Units/Warrior/Blue`。
- 没有变体时直接使用 `Units/<UnitType>`，例如 `Units/Skull`。
- 同一 Unit 的 Prefab、动画、Animator 和技能配置保持相邻。
- 属性配置按平衡原型共享；同兵种的颜色或皮肤变体可以引用同一份 `<UnitType>Attributes.asset`。
- 不同兵种即使当前数值相同，只要未来需要独立平衡，就分别维护属性配置。
- `UnitDefinition` 统一放在 `Assets/GameAsset/Units/Definitions`，便于 YooAsset 独立收集。

## 4. Prefab 契约

Prefab 使用以下表现层级：

```text
<UnitName>                # 逻辑根节点，位置在脚底落地点
├─ Visual                 # 美术节点，向上偏移到正确视觉位置
│  ├─ SpriteRenderer
│  └─ Animator
└─ Hurtbox                # 需要参与受击判定时手动添加
   └─ Collider2D
```

根对象至少包含 `Transform` 与 `SortingGroup`；`Visual` 至少包含 `Transform`、`SpriteRenderer` 与 `Animator`。

制作要求：

1. Prefab 文件名、根对象名和 `_prefabAddress` 使用同一个稳定的 `<UnitName>`，例如 `Skull`。
2. `Animator.runtimeAnimatorController` 必须指向该 Unit 的 Controller。
3. 根对象的位置是单位脚底落地点，`SortingGroup` 使用 `World` Sorting Layer、`Sorting Order = 0`；不要用固定高 Order 让单位永久遮住场景物件。
4. `Visual` 只负责美术偏移和动画，`SpriteRenderer` 应显示 Idle 的有效初始帧，并使用 `World` Sorting Layer、`Sorting Order = 0`。
   - 中心 Pivot 的素材以 Idle 首帧为基准，将“画面中心到脚底接地点”的像素距离除以 PPU，作为 `Visual.localPosition.y`。
   - 偏移量保持在像素网格上，不通过修改逻辑根节点来修正美术位置。
5. 当前基准 Prefab 不挂项目自有脚本。只有当前需求确实需要时，才添加 Collider、Rigidbody 等 Unity 内置组件。
6. 不在每个 Prefab 上挂 Unit Controller；输入、移动、动画和技能驱动由 `UnitEntity` 的通用 Logic 负责。

需要真实物理碰撞的 Unit 额外遵守：

1. `Rigidbody2D` 与身体 `Collider2D` 成对挂在逻辑根节点，不能只配置其中一个。
2. `Rigidbody2D` 使用 `Dynamic`、`Gravity Scale = 0`、冻结 Z 轴旋转；像素角色建议开启插值。
3. 身体 `Collider2D` 不勾选 `Is Trigger`，只表达稳定的单位占地，不跟随武器或攻击动画轮廓变化。
4. 物理组件存在时，`UnitMovementLogic` 通过刚体速度移动；没有物理组件的旧 Unit 暂时保留 Transform 移动。
5. 近战可攻击范围由技能 SO 的查询框表达，与身体碰撞体是不同概念；不要通过放大身体碰撞体表达攻击范围。

需要参与伤害判定的 Unit 额外手动添加 Hurtbox：

1. Hurtbox 是逻辑根节点的子对象，Layer 使用 `UnitHurtbox`。
2. Hurtbox 只挂一个符合单位受击轮廓的 `Collider2D`，并勾选 `Is Trigger`。
3. Hurtbox 不再添加 `Rigidbody2D`；它通过根节点的 `Rigidbody2D` 归属到当前 `UnitEntity`。
4. 身体碰撞体负责与地图障碍的物理碰撞。当前 `UnitSystem` 在生成时忽略单位身体之间的碰撞，单位可以相互穿过；Hurtbox 继续参与技能查询，不以身体阻挡表达攻击范围。
5. 当前近战查询只命中技能 SO 中 `_hitLayerMask` 包含的 Layer，因此没有 Hurtbox 的单位不会受到近战伤害。

当前由 `WarriorBlue` 作为真实单位碰撞试点。

`UnitSystem` 会在实例中查找 `Animator` 和 `SpriteRenderer`。缺少任一组件时生成失败，并输出错误日志。
根节点的 `Rigidbody2D` 与 `Collider2D` 只配置其中一个时，生成同样失败。

## 5. Animator 与动画契约

1. Controller 必须包含 `UnitDefinition` 中填写的 Idle 与 Move 状态。
2. Controller 必须包含每个技能配置引用的全部动画状态。
3. 状态名区分大小写，必须与 SO 中填写的字符串完全一致。
4. Idle、Move 和需要持续保持的 Guard 动画循环播放；一次性 Attack 动画不循环。
5. 当前 `UnitAnimationLogic` 使用 `Animator.Play` 直接切换状态，不依赖参数和 Transition。不要为了满足当前流程额外建立参数或状态跳转。
6. 技能结束后，表现层会根据移动输入回到 Idle 或 Move。

动画状态名表达动作语义即可。新增资源优先使用稳定、一致的命名，不为已有可用资源做无收益的批量改名。

## 6. UnitDefinition 配置

为每个 Unit 创建一个 `UnitDefinition`：

| 字段 | 要求 |
| --- | --- |
| `_prefabAddress` | Prefab 的 YooAsset address；当前 `AddressByFileName` 下等于不带扩展名的 Prefab 文件名 |
| `_attributeSet` | 有效的 `UnitAttributeSetConfig`；必须包含 Health、MaxHealth、MoveSpeed |
| `_idleAnimationStateName` | Controller 中真实存在的 Idle 状态名 |
| `_moveAnimationStateName` | Controller 中真实存在的 Move 状态名 |
| `_skills` | 该单位实际注册的技能配置列表 |

`UnitDefinition` 的资产名使用 `<UnitName>Definition`。调试面板会读取带 `UnitDefinition` 标签的地址，显示时去掉 `Definition` 后缀。

## 7. 属性 SO 配置

每个 `UnitDefinition` 引用一个通用类型的 `UnitAttributeSetConfig`。不要创建 `WarriorAttributeSet.cs`、`MageAttributeSet.cs` 等兵种专属属性类。

配置分成两类：

- Stat：基础能力或资源上限，例如 `MaxHealth`、`MoveSpeed`、`MaxMana`；
- Resource：会消耗与恢复的资源，例如 `Health`、`Mana`，并绑定一个 Stat 作为上限。

所有单位必须配置：

```text
Stats
├─ MaxHealth
└─ MoveSpeed

Resources
└─ Health → MaxHealth
```

使用魔力的单位额外配置：

```text
Stats
└─ MaxMana

Resources
└─ Mana → MaxMana
```

`Mana` 与 `MaxMana` 必须同时存在或同时省略。没有魔力的单位不配置这两个属性；不要用 `Mana = 0` 表示没有魔力。

`MaxHealth` 与钳制后的初始 `Health` 必须大于 0；零生命配置会在生成前被拒绝并记录原因。其他数值必须为有限非负值，`MoveSpeed` 和初始 `Mana` 可以为 0。

生成单位时，属性配置会复制为该 `UnitEntity` 独有的 `UnitAttributeComp`。每个运行时属性都包含：

- `BaseValue`：不包含临时影响的基础值；
- `CurrentValue`：当前参与玩法计算的值；
- Stat 或 Resource 行为类型；
- Resource 使用的最大属性引用。

无临时影响时 `CurrentValue` 等于 `BaseValue`。移动逻辑读取 `MoveSpeed.CurrentValue`；资源修改通过 `TryChangeResource` 执行并限制在 `0` 到最大属性 `CurrentValue` 之间。

属性系统当前只实现初始化、Base/Current 查询和 Resource 增减；BaseValue 初始化后只读，不提供尚无消费者的通用设置与重置接口。
资源增减只修改 `CurrentValue`，不会改写 `BaseValue`。当前伤害 GameEffect 通过该入口扣除 Health。

## 8. 技能 SO 配置

### 8.1 当前 Slot 含义

`SkillConfig._slot` 是技能的触发槽位，不是动画序号。当前已有：

- `Primary`：普通攻击；
- `Secondary`：防御。

同一个 Unit 的 `_skills` 不允许空项或重复 Slot。`UnitSystem` 在租用 Prefab 前检查每个技能的 `TryValidate`；失败会记录 Definition、技能或列表索引，并拒绝此次生成。Slot 仍属于 `SkillConfig` 的运行时契约。

### 8.2 近战攻击

使用 `MeleeAttackSkillConfig`，配置：

- `_slot`；
- `_cooldownSeconds`；
- `_querySize`；
- `_queryOffset`；
- `_hitLayerMask`；
- `_targetRelations`；
- `_hitWindows`；
- `_gameEffects`；
- `_animationStages`。

`_animationStages` 是有序数组：

- 单段攻击配置一个元素，例如 Skull 的 `Skull_Attack`；
- 多段表现按播放顺序配置多个元素，例如 WarriorBlue 的 `Warrior_Attack1_Blue`、`Warrior_Attack2_Blue`；
- `_durationSeconds` 表示该阶段在切换到下一阶段前持续的时间，应按动画 Clip 的实际长度填写；
- 当前数组只描述连续动画表现，不代表伤害段数。

数组不能为空，状态名不能为空，持续时间必须是有限正值；无效阶段会在生成前被拒绝。连续阶段可以引用同一个动画状态，运行时通过技能播放版本在新阶段重新播放该状态。制作时仍需检查 Animator 中确实存在对应状态，字符串非空检查不能替代引用验收。

命中判定使用 `Physics2D.OverlapBox`：

- `_querySize` 是世界单位下的方形或矩形宽高，两个分量都必须大于 0；
- `_queryOffset` 是以单位朝右为基准、相对逻辑根节点的偏移；朝左时运行时自动镜像 X 分量，Y 分量不变；
- `_hitLayerMask` 通常只选择 `UnitHurtbox`；
- `_targetRelations` 声明允许命中的关系，可组合 `Self`、`Ally`、`Enemy`；普通攻击只配置 `Enemy`；
- `_hitWindows` 可配置多个“开始时间 + 持续时间”，时间从本次技能开始时计算；
- 有效窗口内每个 Tick 都会查询，因此目标在窗口开始后进入范围仍可被命中；
- 同一个目标在同一个窗口内只命中一次，不同窗口可再次命中。WarriorBlue 当前配置两个窗口，Skull 配置一个窗口；
- `_gameEffects` 是命中后施加给目标的 Effect 列表，可同时配置瞬时伤害与 DoT。

命中窗口与效果列表至少各有一项，所有项必须有效；LayerMask 与目标关系不能空。窗口开始时间必须位于技能总时长内，窗口结束超出总时长时按总时长截断。冷却、查询框和时间等配置不接受 NaN 或 Infinity。

同一个查询框也用于基础近战 AI 的起手判断：目标 Unit 的 Hurtbox 进入当前朝向下的 `_queryOffset + _querySize` 区域后，AI 停止移动并尝试释放 Primary；目标不在查询框内时通过导航追击。技能冷却只决定能否起手，不会让已经进入查询框的 AI 继续向目标挤压。因此不存在独立的 `_triggerRange`，调整查询框会同时改变实际命中范围和 AI 停步范围。当前不可达目标仍保持锁定；自动换敌或放弃目标属于另行确定的行为规则。

选中 `MeleeAttackSkillConfig` 时，Inspector 提供攻击范围预览，可指定预览基准、切换左右朝向，并在 Scene 视图中直接调整 Offset 与 Size。蓝色实心矩形表示编辑态查询范围。
运行攻击时，Scene 视图会实时绘制实心查询矩形。黄色表示攻击正在播放但当前不在命中窗口，红色表示当前处于命中窗口。

### 8.3 防御

使用 `GuardSkillConfig`，配置：

- `_slot`；
- `_damageReductionRatio`；
- `_cooldownSeconds`；
- `_minimumDurationSeconds`；
- `_animationStateName`。

当前防御按住时保持激活。释放发生在 `_minimumDurationSeconds` 之前时，防御会保持到最短持续时间届满后再停止；
实际停止防御时开始计算 `_cooldownSeconds`，冷却结束前不能再次进入防御。系统级取消不受最短持续时间限制。
当前伤害 GameEffect 已能扣除 Health，但防御减伤尚未接入伤害结算；`_damageReductionRatio` 仍只是保留的通用配置。

### 8.4 伤害 GameEffect

使用 `GameEffectConfig` 配置伤害：

- `_durationPolicy = Instant`：命中时立即结算一次 `_damagePerApplication`；
- `_durationPolicy = Duration`：持续 `_durationSeconds`，每隔 `_periodSeconds` 结算一次 `_damagePerApplication`；
- Duration Effect 的第一次伤害在第一个 Period 到达时结算，不在施加瞬间额外结算；
- 如果一次命中同时需要初始伤害和 DoT，在近战技能的 `_gameEffects` 中同时配置一个 Instant Effect 与一个 Duration Effect；
- 同一个 Duration Effect 被重复施加时，首版作为相互独立的运行时实例叠加；
- 实际伤害会限制 Health 不低于 0，并发布 `UnitDamageEvent`，事件同时包含请求伤害与实际伤害。
- Health 首次降到 0 时会把 `UnitLifeComp` 标记为死亡，并发布一次 `UnitDeathEvent`；Instant 与 Duration 伤害使用同一规则。

`Assets/GameAsset/GameEffects/BasicMeleeDamage.asset` 是当前基础近战瞬时伤害示例。防御减伤、死亡动画、尸体保留、掉落、复活与更复杂的 Modifier/Tag/Stack 规则仍未接入。

### 8.5 战斗队伍与目标关系

1. `UnitSpawnRequest.TeamId` 是运行时队伍来源；同一种 UnitDefinition 和 Prefab 可以按不同 TeamId 生成。
2. `UnitEntity` 使用通用 `UnitTeamComp` 保存 TeamId，不创建兵种专属阵营组件。
3. `UnitSystem` 统一解析关系：同一实体为 `Self`，TeamId 相同为 `Ally`，TeamId 不同为 `Enemy`。
4. 技能通过 `_targetRelations` 决定允许影响的关系；关系过滤发生在查询到 UnitEntity 之后、应用 GameEffect 之前。
5. `GameEffect` 不判断敌我关系，同一个伤害或治疗 Effect 可以被不同目标规则的技能复用。
6. Layer 继续只表达 `UnitHurtbox` 等物理查询角色，不建立 PlayerHurtbox、EnemyHurtbox 等阵营 Layer。
7. 当前 Demo 玩家使用 TeamId 1，单位调试面板默认使用 TeamId 2，并允许输入其他整数 TeamId。

### 8.6 基础死亡流程

1. 每个 `UnitEntity` 都持有独立的 `UnitLifeComp`，初始为存活。
2. `UnitGameEffectLogic` 统一扣除 Health。致死时先标记生命状态，再依次发布 `UnitDamageEvent` 和一次 `UnitDeathEvent`，避免事件回调把零生命单位继续当作存活目标。
3. 死亡单位立即拒绝新的 GameEffect，输入、移动和技能 Logic 停止继续执行。
4. `UnitSystem` 收到死亡事件后只加入待销毁队列，不在 Entity Tick 或事件回调中立即释放对象。
5. `UnitSystem` 在当帧 LateUpdate 统一 `Despawn` 死亡单位。`IEntityModule.RemoveEntity` 先停止并释放 Logic，完成回调再归还 Prefab 和释放 UnitDefinition 租约。更新期间主动移除同样遵守该顺序，Prefab 资源租约由池桶持有。
6. 当前没有死亡动画和死亡停留时间。表现仍通过伤害/死亡事件组织，调试日志由 `UnitCombatLog` 订阅处理，不放进效果结算或单位回收流程。

## 9. YooAsset 收集约束

当前 `Assets/BundleCollectorSetting.asset` 已配置：

| 收集路径 | 过滤 | 打包 | 标签 | 地址规则 |
| --- | --- | --- | --- | --- |
| `Assets/GameAsset/Units` | Prefab | `PackSeparately` | `UnitPrefab` | `AddressByFileName` |
| `Assets/GameAsset/Units/Definitions` | 全部资产 | `PackSeparately` | `UnitDefinition` | `AddressByFileName` |

因此：

1. 正确放入上述目录后，不为每个新 Unit 单独增加 Collector。
2. 所有被收集资产的文件名必须在对应 address 范围内唯一。
3. `_prefabAddress` 填文件名生成的 address，不填磁盘路径或 `.prefab` 扩展名。
4. 属性 SO 和技能 SO 由 `UnitDefinition` 作为依赖带入，不需要独立的运行时 address。
5. 不修改 YooAsset 源码来适配 Unit。

## 10. 标准制作流程

1. 读取本规范以及当前 `UnitDefinition`、属性配置、技能配置和 `UnitSystem`，确认运行时契约没有变化。
2. 选择最接近的新旧 Unit 作为基准：多段近战参考 WarriorBlue，单段近战参考 Skull。
3. 检查源 Sprite、Animation Clip、循环设置和真实时长；不要凭名称猜测。
4. 创建或整理 Animator Controller，确保所有配置使用的状态真实存在。
5. 创建或复用属性 SO，至少配置 MaxHealth、MoveSpeed，以及上限为 MaxHealth 的 Health。
6. 创建技能 SO；普通攻击按真实表现填写一段或多段 `_animationStages`，并配置方形查询范围、目标关系、命中窗口和命中后 GameEffect。普通攻击默认只命中 `Enemy`。
7. 创建脚底为逻辑根、带 `SortingGroup` 和 `Visual` 子节点的表现 Prefab，并正确绑定 Sprite 和 Animator Controller；需要真实物理碰撞时，在根节点成对配置 Dynamic `Rigidbody2D` 与非 Trigger 身体 `Collider2D`；需要受击时手动添加 `UnitHurtbox` Layer 的 Trigger 子碰撞体。
8. 创建 `Definitions/<UnitName>Definition.asset`，填写 Prefab address、属性、移动动画与技能列表。
9. 检查 GUID 引用、YooAsset address 唯一性和 Collector 覆盖范围。
10. 进入开发运行环境，用“YooAsset 单位生成”面板选择 TeamId、刷新列表并生成该 Unit；需要受伤的单位还应验证 Health 归零后只死亡一次并在帧末安全移除。

如果只是新增现有配置已能表达的 Unit，不应修改通用 C# 代码。

## 11. 验收清单

- [ ] 项目当前编译无新增错误。
- [ ] 调试面板能从 `UnitDefinition` 标签中发现新 Unit。
- [ ] 点击生成后 Prefab 成功租出，没有缺少 `Animator` 或 `SpriteRenderer` 的日志。
- [ ] 属性 SO 包含 MaxHealth、MoveSpeed 和正确绑定 MaxHealth 的 Health。
- [ ] 初始 MaxHealth / Health 为有限正数；故意设置为 0 时生成明确失败。
- [ ] 使用 Mana 的单位同时包含 Mana 与 MaxMana；不使用 Mana 的单位同时省略两者。
- [ ] 每个运行时 Unit 拥有独立的属性值，没有修改共享属性 SO。
- [ ] 调试面板生成成功后能显示 Health 和 MoveSpeed 的 Base / Current 信息。
- [ ] Unit 生成后默认播放 Idle。
- [ ] 移动时播放 Move，停止时回到 Idle。
- [ ] Primary 按顺序播放全部攻击动画阶段；单段和多段配置均不依赖兵种专属代码。
- [ ] 连续相同动画阶段会重新播放；无效技能配置在生成前被拒绝并指出原因。
- [ ] Primary 的查询范围、偏移、LayerMask、目标关系和全部命中窗口来自技能 SO。
- [ ] 非玩家控制的近战 Unit 只在目标 Hurtbox 进入同一个技能查询框后停步并尝试攻击，冷却期间不会继续挤向目标。
- [ ] QueryOffset.x 会随单位左右朝向镜像，实际判定与预览一致。
- [ ] Scene 视图在编辑态显示蓝色实心查询矩形，运行时窗口外为黄色、窗口内为红色。
- [ ] Hurtbox 是 `UnitHurtbox` Layer 的 Trigger 子碰撞体，不额外挂刚体。
- [ ] 同一目标在同一命中窗口只结算一次，不同窗口能够再次结算。
- [ ] Instant GameEffect 立即扣除 Health；Duration GameEffect 按 Period 持续扣除，并且不会修改 Health.BaseValue。
- [ ] Unit 的 TeamId 来自生成请求；普通攻击能命中不同 TeamId 的 Enemy，不能命中相同 TeamId 的 Ally。
- [ ] 致死伤害先发布最后一次 UnitDamageEvent，再发布一次 UnitDeathEvent；死亡单位停止输入、移动、技能与新 Effect。
- [ ] 死亡单位在 LateUpdate 安全 Despawn，没有 Entity 遍历重入异常、重复死亡或资源租约泄漏。
- [ ] 主动回收、首次 Tick 前回收和更新中回收均先结束技能/效果，再归还实例；重复回收无副作用。
- [ ] 同一 Unit 再次生成时复用已归还的 Prefab 实例，并正确恢复 Idle、默认朝向、材质属性和零物理速度。
- [ ] Secondary 能进入并退出 Guard 表现。
- [ ] 提前释放 Guard 时会保持到配置的最短持续时间，正常释放后不会无限保持。
- [ ] Guard 结束后进入配置的冷却时间，冷却结束前不能再次触发。
- [ ] Attack 不循环，Idle、Move 和持续 Guard 的循环设置正确。
- [ ] 没有新增 `<UnitName>Logic`、`<UnitName>Comp`、Unit 专属 Controller `MonoBehaviour`。
- [ ] 每个新增 C# 类型独占一个脚本文件。
- [ ] Prefab、Definition、技能 SO 及其引用没有 Missing 或丢失 GUID。
- [ ] YooAsset address 没有重名。
- [ ] 根节点位于脚底落地点，带 `SortingGroup`，且根与 `Visual` 都使用 `World` / Order 0 的排序设置。
- [ ] 需要真实物理碰撞的 Unit 在根节点成对配置 Dynamic `Rigidbody2D` 与非 Trigger 身体 `Collider2D`，移动时没有继续直接修改 Transform。

## 12. 当前基准文件

多段近战 Unit：

- `Assets/GameAsset/Units/Warrior/Blue/WarriorBlue.prefab`
- `Assets/GameAsset/Units/Warrior/WarriorAttributes.asset`
- `Assets/GameAsset/Units/Definitions/WarriorBlueDefinition.asset`
- `Assets/GameAsset/Units/Warrior/Blue/SkillConfigs/WarriorBlueMeleeAttack.asset`
- `Assets/GameAsset/Units/Warrior/Blue/SkillConfigs/WarriorBlueGuard.asset`

单段近战 Unit：

- `Assets/GameAsset/Units/Skull/Skull.prefab`
- `Assets/GameAsset/Units/Skull/SkullAttributes.asset`
- `Assets/GameAsset/Units/Definitions/SkullDefinition.asset`
- `Assets/GameAsset/Units/Skull/SkillConfigs/SkullMeleeAttack.asset`
- `Assets/GameAsset/Units/Skull/SkillConfigs/SkullGuard.asset`

运行时契约入口：

- `Assets/Scripts/GameLogic/Units/Systems/UnitSystem.cs`
- `Assets/Scripts/GameLogic/Units/Configs/UnitDefinition.cs`
- `Assets/Scripts/GameLogic/Units/Configs/Attributes/UnitAttributeSetConfig.cs`
- `Assets/Scripts/GameLogic/Units/Entities/Components/Attributes/UnitAttributeComp.cs`
- `Assets/Scripts/GameLogic/Units/Entities/Components/UnitLifeComp.cs`
- `Assets/Scripts/GameLogic/Units/Entities/Components/UnitSkillComp.cs`
- `Assets/Scripts/GameLogic/Units/Entities/Components/GameEffectComp.cs`
- `Assets/Scripts/GameLogic/Units/Entities/Logic/UnitGameEffectLogic.cs`
- `Assets/Scripts/GameLogic/Units/Entities/UnitEntity.cs`
- `Assets/Scripts/GameLogic/Units/Skills/Configs/SkillConfig.cs`
- `Assets/Scripts/GameLogic/Units/Skills/Configs/MeleeAttackSkillConfig.cs`
- `Assets/Scripts/GameLogic/Units/Skills/Configs/GuardSkillConfig.cs`
- `Assets/Scripts/GameLogic/Units/Skills/Configs/SkillHitWindow.cs`
- `Assets/Scripts/GameLogic/Units/Effects/GameEffectConfig.cs`
- `Assets/Scripts/GameLogic/Units/Events/UnitDamageEvent.cs`
- `Assets/Scripts/GameLogic/Units/Events/UnitDeathEvent.cs`

## 13. 规范与 Skill 的同步迭代

项目 Skill 位于 `.agents/skills/create-unit-prefab/SKILL.md`，用于把本文转化为 AI 可重复执行的工作流。

每次修改本文时执行以下检查：

1. 如果修改了制作步骤、职责边界、目录、命名、配置字段、YooAsset 规则或验收方式，同一次修改中必须同步更新 Skill。
2. 同时递增本文的“规范版本”和 Skill 中的“适配规范版本”，并确保两者一致。
3. Skill 只写执行顺序、触发范围和必须检查的内容；具体字段和值仍以本文为准，避免两套正文产生冲突。
4. 只修正错别字、链接或排版且不改变执行含义时，可以不升级版本，但仍需确认 Skill 不受影响。
5. 新增 Unit 时不递增规范版本；只有制作契约本身发生变化时才升级。
6. 修改完成后运行 `python -X utf8 .agents/skills/create-unit-prefab/scripts/validate_sync.py`，确认两个版本一致。
