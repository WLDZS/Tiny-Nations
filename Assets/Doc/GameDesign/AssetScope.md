# 素材约束与现状清单

版本：0.1  
日期：2026-09-22  
适用范围：两人联机 Demo，以及最多四人的同阵容对战规划。

## 1. 使用本清单的方式

本游戏以已有素材确定表现边界。Demo 使用 Worker、Warrior、Archer；完整阵容增加 Lancer、Monk。当前方案是每方约 20 人、工人与军人共享人口的易上手 RTS；共享人口规则和数值统一见 [数值设计](Balance.md)。玩家以整队进退和简单命令为主，不要求逐人管理技能。没有列入主方案的素材不因此自动成为新兵种、科技或模式。

本清单区分三种证据，避免把美术库存写成已完成玩法：

| 标记 | 含义 |
|---|---|
| 素材已有 | 工作区存在贴图、切片、动画或 Controller；重点角色和效果已抽样目视检查。只能说明表现有依据。 |
| 配置 / 代码已有，待运行验收 | 存在 UnitDefinition、Prefab、技能配置或对应代码；本次没有运行 Unity，不能据此承诺游戏内行为完整可用。 |
| 策划待实现 | 本文提出的用途与规则；需要配置或程序接入，以及实际运行验收。 |

本次未改动单位、动画、场景、技能配置和代码，未调用 Unity CLI，未进行联机、战斗或运行时验收。

## 2. 玩家单位素材

五类玩家单位均有 **Blue、Red、Purple、Yellow、Black 五色**。Demo 使用蓝、红；四人模式增加紫、黄；黑色暂不占用玩家颜色。颜色表示玩家身份，不增加阵营技能差异。

| 单位 | 每色实际动作素材 | 允许的表现与主方案用途 | 当前接入状态 |
|---|---|---|---|
| [Worker](../../GameAsset/Units/Worker/) 工人 | 每色 20 张：空手 Idle / Run；Axe、Pickaxe、Hammer、Knife 各 Idle / Run / Interact；Gold、Wood、Meat 各 Idle / Run | Demo 采金、伐木、搬运、建造。金木链条直接使用对应工具和负重动作；本稿不启用维修。 | 素材已有；没有 Worker UnitDefinition / Unit Prefab，经济与建造流程待实现。 |
| [Warrior](../../GameAsset/Units/Warrior/) 战士 | 每色 Idle、Run、Guard、Attack1、Attack2，共 5 张 | Demo 自动近战前排，两段挥砍有明确动作依据。Guard 保留库存，本稿不启用。 | 蓝色已有 Definition / Prefab / 近战与 Guard 配置，待运行验收；Guard 减伤未接入伤害结算，其余颜色仍需装配。 |
| [Archer](../../GameAsset/Units/Archer/) 弓手 | 每色 Idle、Run、Shoot，共 3 张 | Demo 后排单体射击；搭配现有 Arrow。 | 素材已有；没有 Archer Definition / Unit Prefab，发射、命中与远程行为待实现。 |
| [Lancer](../../GameAsset/Units/Lancer/) 枪兵 | 每色 Idle / Run，加 Down、Downright、Right、Upright、Up 各 Attack / Defence，共 12 张；另有 Default 3 张 | 完整阵容中使用方向刺击、较长触及距离协助前排。Defence 保留库存，本稿不启用架枪机制。 | 素材已有；没有 Lancer Definition / Unit Prefab，方向选择和普通攻击规则待实现。 |
| [Monk](../../GameAsset/Units/Monk/) 修士 | 每色 Idle、Run、Heal，共 3 张 | 完整阵容的自动后排治疗，无手动治疗按钮。 | 素材已有；没有 Monk Definition / Unit Prefab，治疗和目标选择待实现。 |

Worker 对应原素材 Pawn，并非两种兵。可追溯到 [WorkerBlueIdle.png.meta](../../GameAsset/Units/Worker/Blue/WorkerBlueIdle.png.meta) 中保留的原始路径 `Pawn and Resources/Pawn/Blue Pawn/Pawn_Idle.png`。

### 2.1 已核实的表现边界

- [WorkerBlueInteractAxe](../../GameAsset/Units/Worker/Blue/WorkerBlueInteractAxe.png) 是持斧交互；采集行为有美术依据。工人不因持有斧头就自动获得完整战斗兵职责。
- [WarriorBlueAttack1](../../GameAsset/Units/Warrior/Blue/WarriorBlueAttack1.png) 是挥剑动作；攻击范围、目标数、每段伤害由设计决定，不能从剑光形状推导范围伤害。
- [WarriorBlueGuard](../../GameAsset/Units/Warrior/Blue/WarriorBlueGuard.png) 是举盾姿态，配套动画为循环。这里只记录已有表现库存，本稿不开放 Guard、主动盾防或隐含的盾防减伤。
- [ArcherBlueShoot](../../GameAsset/Units/Archer/Blue/ArcherBlueShoot.png) 明确包含拉弓与释放。没有据此设计爆炸箭、散射箭或新魔法箭。
- [LancerBlueRightDefence](../../GameAsset/Units/Lancer/Blue/LancerBlueRightDefence.png) 是横枪架势，不是持盾。该动作保留库存，本稿无架枪、反冲锋、击退或缴械机制，也不引入骑兵来填充克制关系。
- [MonkBlueHeal](../../GameAsset/Units/Monk/Blue/MonkBlueHeal.png) 是治疗施法。没有独立普通攻击、复活或召唤动作，主方案不增加这些能力。

## 3. 建筑素材

下列七类建筑均有蓝、红、紫、黄、黑五色。核实的是建筑外观素材；不能由一张完整建筑图推定建造进度、损毁、攻击动画、生产队列或网络同步已完成。

| 建筑 | 素材证据 | 设计用途与边界 |
|---|---|---|
| Castle | [CastleBlue.png](../../GameAsset/Buildings/Castle/Blue/CastleBlue.png) | 主基地外观依据，核心用途按主设计确定。 |
| Barracks | [BarracksBlue.png](../../GameAsset/Buildings/Barracks/Blue/BarracksBlue.png) | 战士与后续枪兵的训练建筑外观依据。 |
| Archery | [ArcheryBlue.png](../../GameAsset/Buildings/Archery/Blue/ArcheryBlue.png) | 弓手训练建筑外观依据。 |
| House1 | [House1Blue.png](../../GameAsset/Buildings/House1/Blue/House1Blue.png) | 住房外观依据。 |
| House2 | [House2Blue.png](../../GameAsset/Buildings/House2/Blue/House2Blue.png) | 复用第二种房屋外观作为仓库，名称与资源交付用途由 UI 明示；不增加住房升级线。 |
| Monastery | [MonasteryBlue.png](../../GameAsset/Buildings/Monastery/Blue/MonasteryBlue.png) | 完整阵容中修士的训练建筑外观依据。 |
| Tower | [TowerBlue.png](../../GameAsset/Buildings/Tower/Blue/TowerBlue.png) | 防御建筑可用外观；具体开放阶段、数量与攻击规则按主设计和数值表执行。 |

建筑建造、受击、摧毁及生产状态优先使用已有外观配合进度条、颜色和通用效果表达，不把未发现的阶段性动画列为交付前提。House3、Cannon、PirateTower、GnomeHut、FishHut、Cave 等其他建筑外观保留库存，不自动扩大 Demo 的建筑树。

## 4. 金木资源与工人动作链

Demo 资源以金、木为限。对应采集、负重和资源外观均已有，但资源数量、采集计时、容量、交付和耗尽规则属于待实现玩法。

| 链条 | 现有素材 | 对应表现 |
|---|---|---|
| 伐木 | [Tree1.png](../../GameAsset/World/Props/Trees/Tree1.png)、[Tree1Chopped.anim](../../GameAsset/World/Props/Trees/Tree1Animation/Tree1Chopped.anim)、[Stump1.png](../../GameAsset/World/Props/Trees/Stump1.png) | 树木可用、砍伐过渡、耗尽后树桩。 |
| 木材搬运 | [WorkerBlueInteractAxe.png](../../GameAsset/Units/Worker/Blue/WorkerBlueInteractAxe.png)、[WorkerBlueRunWood.png](../../GameAsset/Units/Worker/Blue/WorkerBlueRunWood.png)、[WoodResource.png](../../GameAsset/World/Resources/Wood/WoodResource.png) | 持斧采集、携木返程、木材图标或物件。 |
| 采金 | [GoldStone1.png](../../GameAsset/World/Resources/Gold/Deposits/GoldStone1.png)、[GoldStone1Highlight.png](../../GameAsset/World/Resources/Gold/Deposits/GoldStone1Highlight.png)、[WorkerBlueInteractPickaxe.png](../../GameAsset/Units/Worker/Blue/WorkerBlueInteractPickaxe.png) | 金矿节点、交互反馈、持镐采集。金矿有多种外形，不因此增加多种矿物。 |
| 金币搬运 | [WorkerBlueRunGold.png](../../GameAsset/Units/Worker/Blue/WorkerBlueRunGold.png)、[GoldResource.png](../../GameAsset/World/Resources/Gold/Resource/GoldResource.png) | 负重返程和黄金资源表现。 |
| 建造 | [WorkerBlueInteractHammer.png](../../GameAsset/Units/Worker/Blue/WorkerBlueInteractHammer.png) | 工人敲击施工的表现依据；建造位置、占用和进度仍需实现。锤子动作不代表存在维修，本稿 Demo 和完整阵容均不启用维修。 |

Worker 的 Knife / Meat 动作、[MeatResource.png](../../GameAsset/World/Resources/Meat/MeatResource.png)、Pig、Sheep 素材保留，不在 Demo 中追加食物资源和狩猎循环。

## 5. 投射物、受击、治疗与死亡表现

| 内容 | 已有素材或配置 | 使用约束 |
|---|---|---|
| 弓箭 | [Arrow.png](../../GameAsset/Combat/Projectiles/Arrow/Arrow.png) | 可支持弓手和设计中允许的箭矢攻击；发射点、弹道、命中和销毁需要程序实现。 |
| 举盾库存 | [WarriorBlueGuard.asset](../../GameAsset/Units/Warrior/Blue/SkillConfigs/WarriorBlueGuard.asset) 与 Guard 动画 | 有状态、动画和减伤字段，但减伤未接入伤害结算。本稿不启用 Guard，不需要为它配置正式平衡数值。 |
| 治疗 | [MonkBlueHeal.png](../../GameAsset/Units/Monk/Blue/MonkBlueHeal.png)、[VfxHealEffect.png](../../GameAsset/Combat/VFX/Healing/VfxHealEffect.png) | 修士施法与独立绿环、十字粒子均已有，可组合为单体治疗。配套通用治疗动画默认循环，实际播放时长应由治疗流程控制。 |
| 消散烟尘 | [VfxDust01.png](../../GameAsset/Combat/VFX/Dust/VfxDust01.png)、[VfxDust02.png](../../GameAsset/Combat/VFX/Dust/VfxDust02.png) | 可复用为单位死亡后的烟尘；这是拟接入用途，不是已完成死亡效果。 |
| 受击闪色 | [UnitDamageFlashLogic.cs](../../Scripts/GameLogic/Units/Entities/Logic/UnitDamageFlashLogic.cs)、[UnitSpriteFlash.shader](../../GameAsset/Units/Common/Shaders/UnitSpriteFlash.shader) | 代码和材质基础已有，实际视觉仍需运行验收；不要求每种角色新增受击序列帧。 |

五类玩家单位未发现独立 Dead / Death 动作。拟采用“死亡立即停止战斗和碰撞，角色移除，原位置短暂播放现有烟尘”的统一处理。烟尘独立于单位的逻辑存活，不允许尸体继续挡路或承受治疗。本次检查发现 [UnitSystem.cs](../../Scripts/GameLogic/Units/Systems/UnitSystem.cs) 已处理死亡事件并排队回收单位；不能将这个回收流程描述为已接入死亡烟尘。

此外存在 Acorn、CannonBall、Bomb 投射物，以及 Explosion、Fire、WaterSplash 通用效果。这些作为库存保留，不据此新增玩家攻城车、投弹手、法师或海军。

## 6. UI 与音频边界

现有 UI 外观可支持轻量 RTS 面板，但并不证明面板逻辑已经完成：

- 纸张与条幅：[Papers](../../GameAsset/UI/Decorations/Papers/)、[Ribbons](../../GameAsset/UI/Decorations/Ribbons/)、[Banners](../../GameAsset/UI/Decorations/Banners/)。
- 按钮：[Buttons](../../GameAsset/UI/Buttons/)，包含大小、圆形 / 方形、蓝红按钮。
- 进度条 / 生命条底图与填充：[Bars](../../GameAsset/UI/Bars/)。
- 头像：[Humans](../../GameAsset/UI/Avatars/Humans/)、[Creatures](../../GameAsset/UI/Avatars/Creatures/)。头像文件名多数只有编号，接入时须目视匹配，不根据编号猜兵种。
- 光标与基础图标：[Cursors](../../GameAsset/UI/Cursors/)、[Icons](../../GameAsset/UI/Icons/)。图标含义须核图，未匹配的命令可先用文字按钮表达。

在 `Assets/GameAsset` 中按常见音频扩展名检索，未发现 `.wav`、`.mp3`、`.ogg`、`.aiff`、`.aif`、`.flac` 文件。本设计不假设已有攻击、死亡、资源、建筑或语音音效；Demo 的关键信息必须可通过画面和文字辨认。该结论只针对本目录与这些文件类型，不能推定整个项目绝无音频。

## 7. 七个现有单位配置与原始美术的区别

[Definitions 目录](../../GameAsset/Units/Definitions/) 当前有七个 UnitDefinition，并存在对应 Prefab、Attributes 和技能配置。它们的装配完整度不一致，不能直接当作七种可用战斗单位，更不能当作最终七兵种阵容。

| Definition | 当前技能配置 | 对本方案的意义 |
|---|---|---|
| [WarriorBlueDefinition](../../GameAsset/Units/Definitions/WarriorBlueDefinition.asset) | 近战 + Guard | 玩家战士的现有装配起点。 |
| [MinotaurDefinition](../../GameAsset/Units/Definitions/MinotaurDefinition.asset) | 近战 + Guard | 保留测试或未来中立候选，不纳入 Demo 玩家兵种。 |
| [PandaDefinition](../../GameAsset/Units/Definitions/PandaDefinition.asset) | 近战 + Guard | 同上。 |
| [SkullDefinition](../../GameAsset/Units/Definitions/SkullDefinition.asset) | 近战 + Guard | 同上。 |
| [PaddleFishDefinition](../../GameAsset/Units/Definitions/PaddleFishDefinition.asset) | 近战 | 保留；不由此增加水战或运输。 |
| [PigRiderDefinition](../../GameAsset/Units/Definitions/PigRiderDefinition.asset) | 近战 | 保留；不由此增加玩家骑兵。 |
| [SpiderDefinition](../../GameAsset/Units/Definitions/SpiderDefinition.asset) | 近战 | 保留测试或未来中立候选。 |

当前仅 [WarriorBlue.prefab](../../GameAsset/Units/Warrior/Blue/WarriorBlue.prefab) 与 [Spider.prefab](../../GameAsset/Units/Spider/Spider.prefab) 同时配置了根节点 Rigidbody2D、身体 CircleCollider2D，以及带触发 CapsuleCollider2D 的 Hurtbox。Minotaur、Panda、Skull、PaddleFish、PigRider 五个 Prefab 缺少这一身体 / 受击配置，战斗装配不完整；不能因为它们具有技能配置就写成已经能够被命中或参与完整战斗。

七种当前 Attributes 均为 **初始 / 最大生命 100、移速 3**。全部近战技能引用同一个 [BasicMeleeDamage.asset](../../GameAsset/GameEffects/BasicMeleeDamage.asset)，当前每次应用伤害为 10；四个 Guard 配置的减伤比例字段均为 50%。这些是当前测试配置，不是正式平衡，正式使用的单位参数见 [Balance.md](Balance.md)。

Guard 的 50% **目前只是配置值，不能认定为有效减伤**：[GuardSkill.cs](../../Scripts/GameLogic/Units/Skills/Runtime/GuardSkill.cs) 保存该比例并管理激活、结束与动画状态，而 [UnitGameEffectLogic.cs](../../Scripts/GameLogic/Units/Entities/Logic/UnitGameEffectLogic.cs) 的伤害流程直接扣除 `DamagePerApplication`，没有读取 Guard 比例。这一代码缺口经静态检查确认；本稿不启用 Guard，也不将修复它列为 Demo 前置工作。

现有攻击阶段和命中窗并不相同：蓝战士有两个动画阶段和两个命中窗，其他测试角色多为单阶段。部分异族命中框还使用相同的大范围初始值，因此不能用“伤害配置相同”推出攻击强度相同，也不能把当前配置的伤害值当成每秒伤害。

`UnitDefinition` 当前记录 Prefab 地址、属性集、待机 / 移动动画名和技能列表，见 [UnitDefinition.cs](../../Scripts/GameLogic/Units/Configs/UnitDefinition.cs)。生产、资源、价格、战斗单位上限、目标选择、对局胜负等玩法不能从 Definition 文件存在推定为已实现。

## 8. 未纳入主方案的角色

库存保留不代表待办承诺。以下分类只说明现有素材允许的表现，以及为什么不在当前小规模玩家阵容中启用。

| 分类 | 实际角色 / 动作依据 | 暂不纳入原因 |
|---|---|---|
| 其他地面近战与中立候选 | Bear、Gnome、Lizard、Minotaur、Panda、PigRider、Skull、Snake、SpearGoblin、Spider、Thief、TorchGoblin、Turtle 均有待机、移动、攻击；部分有 Guard / Hit 等动作。 | 现有五类玩家角色已经覆盖经济、前排、远程、控制距离、治疗职责。加入更多会增加平衡与识别负担，且多为单套外观。 |
| 其他远程候选 | Gnoll 有 Throw / Bone；HarpoonFish 有 Throw / Harpoon；BombFish、SlingshotGnome 有 Shoot。 | 会扩大投射物、伤害与阵容验证范围；不据此预设爆炸、穿透或控制效果。 |
| 飞行角色 | GiantBat、Bumblebee 有飞行动作和攻击。 | 会引入空地判定、飞行导航与新的对抗规则，排除 Demo。 |
| 船与鱼人组合 | Boat 有 Idle；SeahorseBoat 有 Idle 与三种鱼人搭载外观；PaddleFish 有 Row。 | 不能把搭载外观等同于完整运输系统；水域路径、上船和下船均不在当前范围。 |
| 复杂动作角色 | Shaman 有攻击、投射物、爆炸及变形施法素材；Troll 有 Windup / Attack / Recovery / Dead 与分件棒槌。 | 会增加技能表现、装配与规则复杂度，不作为小规模 Demo 的必要内容。 |
| 生活动物 | Pig、Sheep 有待机、移动或吃草动作。 | 可以作为环境库存；不增加战斗兵、食物资源和狩猎系统。 |

以上角色目录均位于 [Units](../../GameAsset/Units/)。未发现的行为、变身结果、召唤物或死亡动作不得凭角色名补全。

## 9. 后续接入要求

1. 先使用蓝红两色落实 Worker、Warrior、Archer 的完整对战闭环，再按主设计安排 Lancer、Monk 和额外玩家颜色。
2. 原始动画、通用特效、七个测试 Definition 与正式平衡表分开理解；接入时逐项验证，不用“有文件”替代运行验收。
3. 不为填充阵容而设计需要新兵种本体、新形态或新动作的玩法。优先用已列素材、简单 UI 和现有颜色明确表达状态。
4. 真正制作或修改 Unit Prefab、Animator、UnitDefinition 或技能配置前，阅读 [Unit 制作规范](../UnitPrefabAuthoring.md)，并使用项目 `create-unit-prefab` Skill。本文仅确定策划边界，不替代制作规范。
