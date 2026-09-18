# Tiny Nations Unity 目录规范

本文规定项目自有代码、资源、文档与第三方内容的放置方式。目标是让开发者只看路径就能判断“它属于谁、由谁使用、何时加载”。
代码写法和框架边界见 [C# 与框架开发规范](CodingStandards.md)。

## 1. 目录设计原则

1. **先按所有权分层，再按功能归类**：框架、业务、项目资源、第三方依赖互不混放。
2. **业务按功能聚合**：同一功能的入口、规则、事件和 UI 放在相邻位置，避免按技术类型散落全项目。
3. **目录按需创建**：没有实际文件时不预建完整空树。
4. **路径稳定**：不为视觉整齐频繁搬动 Unity 资产；确需移动时连同 `.meta` 一起移动并验证引用。
5. **层级适度**：能在一层清楚表达时不增加第二层；一个功能内同类文件达到约 3 个再拆子目录。

## 2. 项目根目录

```text
Tiny-Nations/
├─ Assets/             Unity 资产与项目代码
├─ Packages/           UPM 依赖声明与本地 package
├─ ProjectSettings/    Unity 项目设置
├─ Bundles/            本地构建或模拟产物，不作为手工编辑源文件
├─ Preproduction/      不进入游戏运行时的策划、美术预研材料
└─ output/             导出、备份或交付过程文件
```

`Library`、`Temp`、`Logs`、`obj`、`UserSettings`、`.idea`、生成的 `.sln/.csproj` 都是本机数据，不属于项目结构设计，
也不作为代码或资源的长期存放位置。

## 3. Assets 标准结构

```text
Assets/
├─ Scripts/
│  ├─ BorFramework/          自有通用框架
│  │  ├─ 1_Core/             无模块归属的基础类型：Hub、ELC、FSM、Singleton
│  │  ├─ 2_Module/           按能力拆分的全局模块
│  │  └─ 3_Boot/             Editor 启动辅助
│  └─ GameLogic/             具体业务功能
│     ├─ GameBoot.cs          应用组合根与 Unity 生命周期入口
│     └─ <Feature>/
├─ GameAsset/                项目运行时内容，优先按业务功能组织
├─ Settings/                 Render Pipeline 等 Unity 配置资产
├─ Resources/                启动前必须同步获得的极少量资产
├─ Plugins/                  第三方代码、编辑器扩展与原始插件
└─ Doc/                      团队开发规范、框架说明与技术记录
```

当前 `1_Core / 2_Module` 是 BorFramework 运行时代码的固定阅读顺序，`3_Boot` 只保留 Boot 场景编辑器辅助；业务目录和资源目录不使用数字排序。
不要把新的顶层职责继续命名为 `4_...`。若出现新的框架职责，应先判断它属于 Core、Module 还是 Boot。

## 4. 框架代码目录

```text
Assets/Scripts/BorFramework/
├─ 1_Core/
│  ├─ Hub/
│  ├─ ELC/
│  ├─ FSM/
│  └─ Singleton/
├─ 2_Module/
│  └─ <Name>Module/
│     ├─ I<Name>Module.cs
│     ├─ <Name>Module.cs
│     └─ 该模块专属的少量类型或子目录
├─ 3_Boot/
│  └─ Editor/
└─ BorFramework.asmdef
```

放置规则：

- Core 只能放多个模块都会使用、且自身不是全局服务的基础类型。
- Module 文件夹同时放接口与实现，方便一次打开看完整契约；不要建立只有一个文件的 `Interface`/`Implementation` 空层。
- `GameBoot` 属于具体应用的组合根，放在 `GameLogic`，可以同时引用框架实现与具体业务类型。
- Editor 代码必须位于 `Editor` 目录并使用 Editor-only asmdef，运行时代码不能引用它。
- 框架依赖第三方库时统一从 asmdef 声明；不得让业务程序集反向成为框架依赖。

## 5. 业务代码目录

业务优先采用“功能一个目录”，从简单结构开始：

```text
Assets/Scripts/GameLogic/Combat/
├─ CombatSystem.cs
├─ CombatState.cs
├─ DamageEvent.cs
└─ CombatHudView.cs
```

文件增加后再按真实职责拆分：

```text
Assets/Scripts/GameLogic/Combat/
├─ CombatBootstrap.cs        该功能的组装入口
├─ Systems/                  GameSystem 与业务流程
├─ Entities/                 Entity 与 Logic
├─ UI/                       View 与 ViewModel
├─ Events/                   只服务该功能的事件
├─ Configs/                  配置类型与读取适配
└─ Tests/                    该功能的测试
```

- 不建立全项目的 `Managers`、`Helpers`、`Misc`、`Common` 垃圾桶目录。
- 只有至少两个功能稳定共享的业务代码才进入 `GameLogic/Shared`，并写清调用边界。
- `Editor` 工具放在所属功能的 `Editor` 子目录；不要把所有工具集中到一个无法追溯归属的目录。
- `GameLogic` 只保留正式业务代码；临时验证内容放在 `Assets/Development`，不作为正式流程依赖。

## 6. 运行时资源目录

正式项目资源先按稳定的游戏对象和内容领域组织，颜色、阵营与动作作为对象内部的变体：

```text
Assets/GameAsset/
├─ Units/                    单位类型优先，颜色或皮肤作为下一层变体
│  ├─ Archer/
│  │  ├─ Blue/
│  │  └─ Red/
│  └─ Worker/
├─ Buildings/                建筑类型优先，颜色或皮肤作为下一层变体
├─ World/
│  ├─ Terrain/
│  │  └─ Tilesets/
│  │     └─ <Tileset>/
│  │        ├─ Textures/    原始 Sprite Sheet 与独立地形纹理
│  │        ├─ Tiles/       切片生成的 Tile 及动画 Tile 资产
│  │        ├─ Rules/       已实际使用的 RuleTile
│  │        └─ Palettes/    仅用于编辑器绘制的 Tile Palette
│  ├─ Props/
│  ├─ Resources/
│  └─ Maps/
│     ├─ Templates/         可复用的 Grid/Tilemap 空白结构
│     └─ <MapName>/         地图场景及该地图专属数据、Prefab
├─ Combat/
│  ├─ Projectiles/
│  └─ VFX/
├─ UI/
│  ├─ Avatars/
│  ├─ Buttons/
│  ├─ Icons/
│  └─ Decorations/
└─ Shared/                   仅放没有单一领域归属、且已被多个领域复用的成品资源
```

- 运行时加载使用稳定、唯一的 YooAsset address；不要让调用方依赖易变的磁盘路径。
- 已确定采用的美术包按项目自身的 `Units / Buildings / World / Combat / UI` 结构接管，不长期保留资源包名称作为顶层分类。
- 单位与建筑不按 `Player / Enemy` 分类；敌对关系、阵营与控制权由 Prefab 或配置表达。
- 同一单位的贴图、动画、Animator 和 Prefab 保持相邻，不建立全局 `Textures / Animations / Controllers` 大目录。
- 场景、Prefab 及其材质、贴图、动画是一个依赖闭包；移动时按实际引用整体检查。
- `Shared` 不是临时中转站。资源必须同时满足“没有更自然的单一领域归属”和“至少被两个顶层领域实际使用”才可进入。
- 暂时不知道放在哪里的内容先留在最接近的拥有者目录并标记待确认，不得为了省事放入 `Shared`。
- 例如同时服务单位、建筑和世界的通用 Shader 或材质可以进入 `Shared`；箭矢、头像、Tile、Worker 工具仍分别属于 `Combat`、`UI`、`World` 和 `Units/Worker`。
- Tile Palette 是编辑器绘制工具，放在对应 Tileset 的 `Palettes`；实际绘制结果保存在 `World/Maps/<MapName>/<MapName>Scene.unity`，不得把正式地图场景放进 `Tilesets`。
- 通用地图网格模板放在 `World/Maps/Templates`。单位、建筑和可交互对象继续使用各自领域的 Prefab，不绘制进 Tilemap。
- 地图 Grid 默认使用矩形网格、`Cell Size (1, 1, 0)` 和 `Scale (1, 1, 1)`；改变地图范围不得通过缩放 Grid 实现。
- 素材包自带的预览场景、宣传图和检查用内容放到 `Assets/Development/ArtPreview`，不进入正式场景列表和 YooAsset 收集范围。
- `Resources` 中只保留框架启动前必须同步加载的资产；普通资源、UI 和场景进入 YooAsset 管理。

## 7. Plugins、Packages 与生成代码

| 内容 | 放置位置 | 规则 |
| --- | --- | --- |
| Asset Store/外部插件 | `Assets/Plugins/<Vendor or Package>` | 尽量保持原目录，不做风格化重构 |
| UPM 依赖 | `Packages/manifest.json` 或 `Packages/<local package>` | 不复制到 GameLogic |
| Input System 生成代码 | 源 `.inputactions` 指定的位置 | 只修改源资产并重新生成 |
| 项目 Editor 工具 | 所属代码旁的 `Editor/` | 与运行时程序集隔离 |
| 临时导入与转换产物 | `output/` 或工作区外 | 验收后只把正式资产放入 Assets |

## 8. Assembly Definition 规则

- `BorFramework.asmdef` 是框架运行时边界。
- Editor 代码使用独立的 Editor-only asmdef，并只引用所需运行时程序集。
- 当 `GameLogic` 规模足以需要独立编译边界时，再新增 `GameLogic.asmdef`；它可以引用 `BorFramework`，反向引用禁止。
- 不为每个小文件夹创建 asmdef。只有需要明确依赖、平台隔离、Editor 隔离或显著改善编译范围时才创建。
- asmdef 名称使用稳定的逻辑名称，不包含版本号或临时项目代号。

## 9. 文件与资产命名

- C# 文件名与主要类型名一致，使用 `PascalCase`。
- 文件夹使用清晰英文名，不使用拼音、模糊缩写、日期或“新建文件夹”。
- 场景使用 `...Scene`，Prefab 使用对象含义命名，不重复附加 `Prefab` 除非有辨识价值。
- 配置资产使用 `<Feature><Purpose>Config`；事件类型使用 `<Meaning>Event`。
- 同一目录内资源名必须唯一；作为 YooAsset address 的文件名在收集范围内也必须唯一。
- 不在文件名中使用 `Final`、`New`、`Copy`、连续编号表达版本；历史版本放到版本管理或工作区外归档。
- 自动切片生成且需要与源图编号对应的 Tile 资产允许保留稳定的下划线编号，例如 `Tilemap_color1_12.asset`。

## 10. 放置决策表

| 新增内容 | 放在哪里 |
| --- | --- |
| 跨玩法的资源、场景、UI、输入等基础服务 | `Scripts/BorFramework/2_Module/<Name>Module` |
| 某个玩法或流程的生命周期 | `Scripts/GameLogic/<Feature>/Systems` |
| 某个功能的 Entity、Logic、事件、UI | 同一个 `Scripts/GameLogic/<Feature>` 下按需分组 |
| 多个功能确认复用的业务代码 | `Scripts/GameLogic/Shared/<Responsibility>` |
| 单位、建筑及其颜色或皮肤变体 | `GameAsset/Units/<Unit>/<Variant>` 或 `GameAsset/Buildings/<Building>/<Variant>` |
| 地形纹理、Tile、RuleTile 与 Tile Palette | `GameAsset/World/Terrain/Tilesets/<Tileset>/<Category>` |
| 地图网格模板 | `GameAsset/World/Maps/Templates` |
| 正式地图场景及地图专属内容 | `GameAsset/World/Maps/<MapName>` |
| 场景摆件与地图资源点 | `GameAsset/World/Props` 或 `GameAsset/World/Resources` |
| 投射物与战斗视觉效果 | `GameAsset/Combat/<Category>` |
| UI 头像、按钮、图标与装饰 | `GameAsset/UI/<Category>` |
| 没有单一领域归属且已被多个领域复用的成品资源 | `GameAsset/Shared/<Category>` |
| 第三方代码或编辑器扩展 | `Plugins/<Vendor or Package>` |
| 开发规范与框架文档 | `Doc` |
| 不参与 Unity 导入的方案、参考、源工程 | `Preproduction` 或 `output`，不放进 `Assets` |
| 仍需在 Unity 中查看但不参与正式构建的素材预览 | `Assets/Development/ArtPreview` |

## 11. 迁移与新增规则

- 本规范先约束新增和正在修改的内容，不为追求整齐一次性搬动所有旧资产。
- 触碰旧目录时，只在能完整验证 GUID、Prefab/Scene 引用和 YooAsset 收集配置的前提下顺手迁移。
- 目录移动必须保留 `.meta`；不手工生成或替换已有 GUID。
- 不提交空目录。Unity 真正需要目录时再创建，由 Unity 生成对应 `.meta`。
- 目录调整完成后至少检查：Missing Script、丢失引用、YooAsset address 冲突、场景列表与编译结果。
