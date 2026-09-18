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
│  ├─ Cliffs                # 悬崖 Tilemap
│  ├─ Decoration            # 无交互的草纹、花、碎石等 Tilemap
│  └─ Collision             # 仅用于地图边界的碰撞 Tilemap
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
| 水、草地、悬崖 | Tile / RuleTile | 对应 Tilemap |
| 草纹、花瓣、细小石子 | Tile | `Decoration` |
| 不可交互但需要前后遮挡的灌木、岩石 | Prefab | `WorldObjects/DecorationObjects` |
| 可割除草丛 | Prefab | `WorldObjects/Harvestables/Plants` |
| 可砍伐树木 | Prefab | `WorldObjects/Harvestables/Trees` |
| 可开采金矿 | Prefab | `WorldObjects/Harvestables/Ores` |
| 墙、巨石等永久阻挡物 | Prefab | `WorldObjects/Obstacles` |

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
2. `Ground`：地面、悬崖、地表纹理等 Tilemap。
3. `World`：单位、树木、矿物、灌木、岩石等需要互相前后遮挡的对象。
4. `WorldOverlay`：明确要求始终覆盖普通世界对象的前景或特效；不要用它绕过正常 Y 排序。

`World` 对象统一使用 `Sorting Order = 0`，不要再通过给单位设置固定高 Order 的方式决定前后关系。多 Sprite 或带动画的完整对象在逻辑根节点挂 `SortingGroup`，让整组内容作为一个对象参与排序。

逻辑根节点的位置必须是落地点：单位使用脚底，树木与岩石使用底部中心。美术中心与落地点不一致时，把 `SpriteRenderer`、`Animator` 放到 `Visual` 子节点并只偏移 `Visual`；移动、碰撞、寻路、存档和 `SortingGroup` 仍使用根节点。

地形 Tilemap 保持 Chunk 模式。需要与单位逐个穿插排序的高物体必须制作成 `WorldObjects` Prefab；只有确实需要逐 Tile 穿插时，才为专用 Tilemap 使用 Individual 模式。
