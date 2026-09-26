# World 资源放置规范

## 核心原则

- Tilemap 只承载没有独立状态的地图内容。
- 需要交互、耐久、掉落、再生或存档的对象必须使用 Prefab。
- 原始图片、动画和 Animator Controller 保留在现有 `Props`、`Resources` 目录中；项目实际放进场景的成品放在 `Prefabs`。

## 场景层级

```text
World
├─ Grid
│  ├─ Water                 # 水面 Tilemap
│  ├─ Ground                # 地面 RuleTile
│  ├─ CliffObstacle_01      # 完整悬崖段，根节点位于墙脚
│  │  └─ Tiles              # 上下层视觉保持在同一个 Tilemap
│  ├─ Decoration            # 无交互的草纹、花、碎石等 Tilemap
│  └─ Collision             # 不可见的地形阻挡 Tilemap
└─ WorldObjects             # 普通 GameObject，不放在 Grid 下面
   ├─ DecorationObjects     # 需要 Y 排序但不能交互的场景物件
   ├─ Harvestables
   │  ├─ Trees
   │  ├─ Ores
   │  └─ Plants
   └─ Obstacles             # 不可采集但会阻挡单位的物件
```

## 资源分类

| 内容 | 放置形式 | 位置 |
| --- | --- | --- |
| 水、草地 | Tile / RuleTile | 对应 Tilemap |
| 复合悬崖 | 带 `SortingGroup` 的完整分段 Tilemap | `Grid/CliffObstacle_XX/Tiles` |
| 草纹、花瓣、细小石子 | Tile | `Decoration` |
| 不可交互但需要前后遮挡的灌木、岩石 | Prefab | `WorldObjects/DecorationObjects` |
| 可割除草丛 | Prefab | `WorldObjects/Harvestables/Plants` |
| 可砍伐树木 | Prefab | `WorldObjects/Harvestables/Trees` |
| 可开采金矿 | Prefab | `WorldObjects/Harvestables/Ores` |
| 墙、巨石等永久阻挡物 | Prefab | `WorldObjects/Obstacles` |

## 障碍物与碰撞

- `Grid/Collision` 定义地形的物理碰撞。水域、地图边界和悬崖墙脚使用 `CollisionTiles/Tiles` 下的红色碰撞 Tile 绘制，不要把整张视觉 Tile 的占格直接当作碰撞。
- `Collision` 使用 `Obstacle` Physics Layer、Static `Rigidbody2D`、`TilemapCollider2D` 和 `CompositeCollider2D`。Tilemap Collider 通过 `Merge` 合并到 Composite，Composite 使用 `Polygons` 生成实心阻挡区域。
- `Collision` 的 `TilemapRenderer` 默认关闭。需要编辑时可以临时开启 Renderer 查看红色碰撞 Tile，完成后重新关闭。`CollisionPalette.prefab` 可作为 Tile Palette 使用。
- `Collisions@20_0` 是完整方格；其余 Tile 提供半格、斜边等轮廓，需要斜坡或转角阻挡时再选用。
- 悬崖墙面可能跨多个格子：只有最下面接触地面的墙脚格负责阻挡，上方墙面只负责遮挡后方道路和单位。当前地形中 `Tilemap_color1_34~36` 是墙脚；其余上层墙面不参与碰撞，但视觉上仍与墙脚放在同一个悬崖段中。
- `WorldObjects/Obstacles` 下的永久障碍 Prefab 根节点使用 `Obstacle` Physics Layer，并在逻辑根上放置非 Trigger `Collider2D`。
- 旧静态导航已移除；`Ground` 仍是地面 Tilemap，`Collision` 仍通过 Collider2D 阻挡物理移动。地图边界应绘制对应碰撞。
- 活动单位的实体碰撞体彼此不形成硬阻挡，地形碰撞仍然阻挡移动。追击同一目标的近战 AI 会预约不同站位，移动中的 AI 会对邻近单位做局部分离；这不保证敌军无法穿身。

## Prefab 命名

- 树木：`Tree_<Type>_<Variant>`，例如 `Tree_Oak_01`
- 矿点：`Ore_<Resource>_<Variant>`，例如 `Ore_Gold_01`
- 可采植物：`Plant_<Type>_<Variant>`，例如 `Plant_Grass_01`
- 装饰物：`Deco_<Type>_<Variant>`
- 障碍物：`Obstacle_<Type>_<Variant>`

## 放置要求

- 所有物件都按地图网格吸附，但保持为独立 GameObject。
- 逻辑根节点使用底部中心落地点；Sprite 保持中心 Pivot 时，通过 `Visual` 子节点向上偏移。树木的碰撞体只覆盖树干底部，不覆盖整片树冠。
- 一个物件只在 `WorldObjects` 的一个分类节点下，避免复制到 Tilemap。
- 多格物件以底部中心所在格作为逻辑锚点，之后的寻路占格和存档都以该锚点计算。
- 需要采集状态的物件后续统一添加采集组件和稳定存档 ID；纯装饰物不添加这些组件。

## 2D 渲染排序

项目使用 `Transparency Sort Mode = Custom Axis`，排序轴为 `(0, 1, 0)`。同一 Sorting Layer、同一 Order 的透明对象按世界坐标 Y 排序：逻辑锚点越靠下，显示越靠前。

Sorting Layer 自后向前约定为：

1. `Water`：水面及始终位于水面层的装饰。
2. `Ground`：地面、地表纹理等不需要与单位穿插的 Tilemap。
3. `World`：单位、完整悬崖段、树木、矿物、灌木、岩石等需要互相前后遮挡的对象。
4. `WorldOverlay`：明确要求始终覆盖普通世界对象的前景或特效，不用于可绕到前后的悬崖。

`World` 对象统一使用 `Sorting Order = 0`，不要再通过给单位设置固定高 Order 的方式决定前后关系。多 Sprite 或带动画的完整对象在逻辑根节点挂 `SortingGroup`，让整组内容作为一个对象参与排序。

逻辑根节点的位置必须是落地点：单位使用脚底，树木与岩石使用底部中心。美术中心与落地点不一致时，把 `SpriteRenderer`、`Animator` 放到 `Visual` 子节点并只偏移 `Visual`；移动、碰撞、寻路、存档和 `SortingGroup` 仍使用根节点。

地面等不参与遮挡的 Tilemap 保持 Chunk 模式。复合悬崖不能拆成固定的前后两层，否则单位位于墙面中间时会被分层画面切开。每一段具有同一墙脚高度的悬崖使用一个 `CliffObstacle_XX` 根节点：根节点位于墙脚，挂 `World / Order 0` 的 `SortingGroup`；该段全部视觉 Tile 放在子节点 `Tiles` 中并保持 Chunk 模式。这样单位脚底位于墙脚后方时整段悬崖覆盖单位，位于墙脚前方时整段悬崖退到单位后面。墙脚高度不同的悬崖必须拆成不同渲染组，物理阻挡仍只画在 `Collision`。
