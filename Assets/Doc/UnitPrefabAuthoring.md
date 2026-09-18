# Tiny Nations Unit 预制体制作规范

规范版本：`1.5`

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
- Unit 的 `Rigidbody2D` 与 `Collider2D` 物理碰撞配置。

本文描述的是当前已经落地的运行时契约，不提前设计伤害、受击、阵营、寻路或 RTS 控制逻辑。

## 2. 核心边界

1. Unit Prefab 是纯表现对象，不挂项目自有的 `MonoBehaviour`、Unit Controller 或 Unit View。
2. `UnitSystem` 负责按 `UnitDefinition` 地址加载配置和 Prefab、创建 `UnitEntity`、持有资源租约并销毁单位。
3. `UnitEntity` 组装通用 Comp 与 Logic；不要为 Warrior、Skull 等具体兵种新增专属 Entity、Comp、Logic 或 Controller。
4. 单位差异优先由 `UnitDefinition`、属性 SO、技能 SO、Animator Controller 和动画资源表达。
5. 一个 C# 文件只放一个类型。不要把运行时辅助类型写成另一个类的嵌套类型。
6. 如果当前通用配置无法表达新单位需求，先指出缺失的通用能力及最小扩展方案；不要直接把兵种特例写进通用系统。
7. 属性 SO 只保存共享初始配置；每个生成的单位必须持有独立的运行时属性值，禁止修改共享 SO 表示掉血、耗蓝或临时 Buff。

## 3. 每个 Unit 的交付物

一个可生成 Unit 至少包含：

```text
Assets/GameAsset/Units/
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
└─ Visual                 # 美术节点，向上偏移到正确视觉位置
   ├─ SpriteRenderer
   └─ Animator
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
5. 技能触发距离与身体碰撞体是不同概念，不通过放大身体碰撞体表达攻击范围。

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

生成单位时，属性配置会复制为该 `UnitEntity` 独有的 `UnitAttributeComp`。每个运行时属性都包含：

- `BaseValue`：不包含临时影响的基础值；
- `CurrentValue`：当前参与玩法计算的值；
- Stat 或 Resource 行为类型；
- Resource 使用的最大属性引用。

无临时影响时 `CurrentValue` 等于 `BaseValue`。移动逻辑读取 `MoveSpeed.CurrentValue`；资源修改通过 `TryChangeResource` 执行并限制在 `0` 到最大属性 `CurrentValue` 之间。

属性系统当前只实现初始化、查询、Base/Current 设置、Current 重置和 Resource 增减，不包含完整 Modifier、Buff 或 GameplayEffect 栈。

## 8. 技能 SO 配置

### 8.1 当前 Slot 含义

`SkillConfig._slot` 是技能的触发槽位，不是动画序号。当前已有：

- `Primary`：普通攻击；
- `Secondary`：防御。

同一个 Unit 的 `_skills` 中不要注册重复 Slot，否则后注册的技能无法进入 `SkillComp`。Slot 目前仍属于 `SkillConfig` 的运行时契约；如果以后要让同一技能配置复用于不同按键槽位，应单独调整装配模型，不在制作单个 Unit 时临时绕过。

### 8.2 近战攻击

使用 `MeleeAttackSkillConfig`，配置：

- `_slot`；
- `_triggerRange`；
- `_cooldownSeconds`；
- `_animationStages`。

`_animationStages` 是有序数组：

- 单段攻击配置一个元素，例如 Skull 的 `Skull_Attack`；
- 多段表现按播放顺序配置多个元素，例如 WarriorBlue 的 `Warrior_Attack1_Blue`、`Warrior_Attack2_Blue`；
- `_durationSeconds` 表示该阶段在切换到下一阶段前持续的时间，应按动画 Clip 的实际长度填写；
- 当前数组只描述连续动画表现，不代表伤害段数，也不在这里实现伤害结算。

数组不能为空，也不要填写空状态名或非正持续时间；无效阶段会被运行时忽略，全部无效时技能无法触发。

### 8.3 防御

使用 `GuardSkillConfig`，配置：

- `_slot`；
- `_damageReductionRatio`；
- `_cooldownSeconds`；
- `_minimumDurationSeconds`；
- `_animationStateName`。

当前防御按住时保持激活。释放发生在 `_minimumDurationSeconds` 之前时，防御会保持到最短持续时间届满后再停止；
实际停止防御时开始计算 `_cooldownSeconds`，冷却结束前不能再次进入防御。系统级取消不受最短持续时间限制。
伤害系统尚未落地，`_damageReductionRatio` 只是已经保留的通用配置，不代表当前已有完整伤害结算。

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
6. 创建技能 SO；普通攻击按真实表现填写一段或多段 `_animationStages`。
7. 创建脚底为逻辑根、带 `SortingGroup` 和 `Visual` 子节点的表现 Prefab，并正确绑定 Sprite 和 Animator Controller；需要真实物理碰撞时，在根节点成对配置 Dynamic `Rigidbody2D` 与非 Trigger 身体 `Collider2D`。
8. 创建 `Definitions/<UnitName>Definition.asset`，填写 Prefab address、属性、移动动画与技能列表。
9. 检查 GUID 引用、YooAsset address 唯一性和 Collector 覆盖范围。
10. 进入开发运行环境，用“YooAsset 单位生成”面板刷新列表并生成该 Unit。

如果只是新增现有配置已能表达的 Unit，不应修改通用 C# 代码。

## 11. 验收清单

- [ ] 项目当前编译无新增错误。
- [ ] 调试面板能从 `UnitDefinition` 标签中发现新 Unit。
- [ ] 点击生成后 Prefab 成功实例化，没有缺少 `Animator` 或 `SpriteRenderer` 的日志。
- [ ] 属性 SO 包含 MaxHealth、MoveSpeed 和正确绑定 MaxHealth 的 Health。
- [ ] 使用 Mana 的单位同时包含 Mana 与 MaxMana；不使用 Mana 的单位同时省略两者。
- [ ] 每个运行时 Unit 拥有独立的属性值，没有修改共享属性 SO。
- [ ] 调试面板生成成功后能显示 Health 和 MoveSpeed 的 Base / Current 信息。
- [ ] Unit 生成后默认播放 Idle。
- [ ] 移动时播放 Move，停止时回到 Idle。
- [ ] Primary 按顺序播放全部攻击动画阶段；单段和多段配置均不依赖兵种专属代码。
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

- `Assets/Scripts/GameLogic/Units/System/UnitSystem.cs`
- `Assets/Scripts/GameLogic/Units/Config/UnitDefinition.cs`
- `Assets/Scripts/GameLogic/Units/Config/UnitAttributeSetConfig.cs`
- `Assets/Scripts/GameLogic/Units/Comp/UnitAttributeComp.cs`
- `Assets/Scripts/GameLogic/Units/Entity/UnitEntity.cs`
- `Assets/Scripts/GameLogic/Units/Skill/SkillConfig.cs`
- `Assets/Scripts/GameLogic/Units/Skill/MeleeAttackSkillConfig.cs`
- `Assets/Scripts/GameLogic/Units/Skill/GuardSkillConfig.cs`

## 13. 规范与 Skill 的同步迭代

项目 Skill 位于 `.agents/skills/create-unit-prefab/SKILL.md`，用于把本文转化为 AI 可重复执行的工作流。

每次修改本文时执行以下检查：

1. 如果修改了制作步骤、职责边界、目录、命名、配置字段、YooAsset 规则或验收方式，同一次修改中必须同步更新 Skill。
2. 同时递增本文的“规范版本”和 Skill 中的“适配规范版本”，并确保两者一致。
3. Skill 只写执行顺序、触发范围和必须检查的内容；具体字段和值仍以本文为准，避免两套正文产生冲突。
4. 只修正错别字、链接或排版且不改变执行含义时，可以不升级版本，但仍需确认 Skill 不受影响。
5. 新增 Unit 时不递增规范版本；只有制作契约本身发生变化时才升级。
6. 修改完成后运行 `python -X utf8 .agents/skills/create-unit-prefab/scripts/validate_sync.py`，确认两个版本一致。
