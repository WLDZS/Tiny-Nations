"""Build the Senen1 scene from the project's current world scene serialization.

Run from the repository root with: python Tools/GenerateSenen1.py
This copies scene structure and asset references; it never changes Demo.unity.
"""

from __future__ import annotations

from collections import Counter, deque
from pathlib import Path
import math
import random
import re
import uuid

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/GameAsset/Scene/Demo.unity"
SCENE = ROOT / "Assets/GameAsset/Scene/Senen1.unity"
PREVIEW = ROOT / "Assets/GameAsset/World/Maps/Senen1Preview.png"
WIDTH = 96
HEIGHT = 64
GRASS_RULE = "Assets/GameAsset/World/Terrain/Tilesets/BaseTerrain/Rules/Ground/GrassGreenRuleTile.asset"
ROAD_TILE = "Assets/GameAsset/World/Terrain/Tilesets/BaseTerrain/Tiles/Tilemap_color4_9.asset"
WATER_TILE = "Assets/GameAsset/World/Terrain/Tilesets/BaseTerrain/Tiles/WaterBackground.asset"
COLLISION_TILE = "Assets/GameAsset/World/Maps/CollisionTiles/Tiles/Collisions@20_0.asset"
TREE_PREFAB = "Assets/GameAsset/World/Prefabs/Harvestables/Trees/Tree_01.prefab"
DECO_TREE_PREFAB = "Assets/GameAsset/World/Prefabs/Decoration/Ground/Deco_Tree_01.prefab"
ORE_PREFAB = "Assets/GameAsset/World/Prefabs/Harvestables/Ores/Ore_Gold_01.prefab"
BUSH_PREFAB = "Assets/GameAsset/World/Prefabs/Harvestables/Plants/Plant_Bush_01.prefab"
ROCK_PREFAB = "Assets/GameAsset/World/Prefabs/Decoration/Ground/Deco_Rock_01.prefab"
BLUE_CASTLE = "Assets/GameAsset/Buildings/Castle/Blue/CastleBlue.png"
RED_CASTLE = "Assets/GameAsset/Buildings/Castle/Red/CastleRed.png"


def asset_path(relative: str) -> Path:
    return ROOT / relative


def guid(relative: str) -> str:
    return re.search(r"^guid: ([0-9a-f]{32})$", asset_path(relative + ".meta").read_text(), re.M).group(1)


def sprite(relative: str) -> tuple[str, str]:
    text = asset_path(relative).read_text()
    match = re.search(r"m_Sprite: \{fileID: ([^,]+), guid: ([0-9a-f]{32})", text)
    return match.group(1), match.group(2)


def split_blocks(text: str) -> tuple[str, list[str]]:
    pieces = re.split(r"(?=^--- !u!)", text, flags=re.M)
    return pieces[0], pieces[1:]


def block_id(block: str) -> int:
    return int(re.match(r"--- !u!\d+ &(\d+)", block).group(1))


def property_value(block: str, name: str) -> str:
    return re.search(r"^  " + re.escape(name) + r": (.*)$", block, re.M).group(1)


def replace_children(block: str, children: list[int]) -> str:
    value = "  m_Children:\n" + "".join(f"  - {{fileID: {child}}}\n" for child in children)
    if not children:
        value = "  m_Children: []\n"
    return re.sub(r"^  m_Children:.*\n(?:  - \{fileID: \d+\}\n)*", value, block, count=1, flags=re.M)


def tilemap_tiles(cells: dict[tuple[int, int], int], assets: list[tuple[str, str]],
                  sprites: list[tuple[str, str]], bounds: tuple[int, int, int, int],
                  rule_tile: bool = False) -> str:
    # Unity's Tilemap serialization uses unique asset/sprite tables and per-cell indices.
    assert len(assets) == 1
    sprite_counts = Counter(cells.values())
    lines = ["  m_Tiles:\n"]
    for (x, y), sprite_index in sorted(cells.items(), key=lambda pair: (pair[0][1], pair[0][0])):
        lines.extend([
            f"  - first: {{x: {x}, y: {y}, z: 0}}\n",
            "    second:\n",
            "      serializedVersion: 2\n",
            "      m_TileIndex: 0\n",
            f"      m_TileSpriteIndex: {sprite_index}\n",
            "      m_TileMatrixIndex: 0\n",
            "      m_TileColorIndex: 0\n",
            "      m_TileObjectToInstantiateIndex: 65535\n",
            "      dummyAlignment: 0\n",
            f"      m_AllTileFlags: {1073741826 if rule_tile else 1073741825}\n",
        ])
    lines.append("  m_AnimatedTiles: {}\n")
    lines.append("  m_TileAssetArray:\n")
    for file_id, asset_guid in assets:
        lines.extend(["  - serializedVersion: 2\n",
                      f"    m_RefCount: {len(cells)}\n",
                      f"    m_Data: {{fileID: {file_id}, guid: {asset_guid}, type: 2}}\n"])
    lines.append("  m_TileSpriteArray:\n")
    for index, (file_id, sprite_guid) in enumerate(sprites):
        lines.extend(["  - serializedVersion: 2\n",
                      f"    m_RefCount: {sprite_counts[index]}\n",
                      f"    m_Data: {{fileID: {file_id}, guid: {sprite_guid}, type: 3}}\n"])
    lines.extend([
        "  m_TileMatrixArray:\n",
        "  - serializedVersion: 2\n",
        f"    m_RefCount: {len(cells)}\n",
        "    m_Data:\n",
        "      e00: 1\n", "      e01: 0\n", "      e02: 0\n", "      e03: 0\n",
        "      e10: 0\n", "      e11: 1\n", "      e12: 0\n", "      e13: 0\n",
        "      e20: 0\n", "      e21: 0\n", "      e22: 1\n", "      e23: 0\n",
        "      e30: 0\n", "      e31: 0\n", "      e32: 0\n", "      e33: 1\n",
        "  m_TileColorArray:\n",
        "  - serializedVersion: 2\n",
        f"    m_RefCount: {len(cells)}\n",
        "    m_Data: {r: 1, g: 1, b: 1, a: 1}\n",
        "  m_TileObjectToInstantiateArray: []\n",
    ])
    x, y, width, height = bounds
    return "".join(lines), (x, y, width, height)


def write_tilemap(block: str, cells: dict[tuple[int, int], int],
                  assets: list[tuple[str, str]], sprites: list[tuple[str, str]],
                  bounds: tuple[int, int, int, int], rule_tile: bool = False) -> str:
    content, (x, y, width, height) = tilemap_tiles(cells, assets, sprites, bounds,
                                                  rule_tile)
    block = re.sub(r"^  m_Tiles:.*?(?=^  m_AnimationFrameRate:)", content, block,
                   count=1, flags=re.M | re.S)
    block = re.sub(r"^  m_Origin:.*$", f"  m_Origin: {{x: {x}, y: {y}, z: 0}}", block, flags=re.M)
    block = re.sub(r"^  m_Size:.*$", f"  m_Size: {{x: {width}, y: {height}, z: 1}}", block, flags=re.M)
    return block


def grass_sprites(land: set[tuple[int, int]]) -> tuple[dict[tuple[int, int], int], list[tuple[str, str]]]:
    rule = asset_path(GRASS_RULE).read_text()
    texture_guid = guid("Assets/GameAsset/World/Terrain/Tilesets/BaseTerrain/Textures/TilemapColor3.png")
    rules = []
    for part in re.split(r"(?=^  - m_Id:)", rule, flags=re.M)[1:]:
        sprite_id = re.search(r"^    - \{fileID: ([^,]+), guid:", part, re.M).group(1)
        neighbors = [(int(x), int(y)) for x, y in re.findall(r"^    - \{x: (-?\d+), y: (-?\d+), z: 0\}", part, re.M)]
        rules.append((sprite_id, neighbors))
    default_sprite = re.search(r"m_DefaultSprite: \{fileID: ([^,]+)", rule).group(1)
    indices: dict[tuple[int, int], int] = {}
    sprites: list[tuple[str, str]] = []
    lookup: dict[str, int] = {}
    for x, y in sorted(land, key=lambda cell: (cell[1], cell[0])):
        sprite_id = next((sprite_id for sprite_id, neighbors in rules
                          if all((x + dx, y + dy) in land for dx, dy in neighbors)), default_sprite)
        if sprite_id not in lookup:
            lookup[sprite_id] = len(sprites)
            sprites.append((sprite_id, texture_guid))
        indices[(x, y)] = lookup[sprite_id]
    return indices, sprites


def dist_to_segment(x: float, y: float, a: tuple[float, float], b: tuple[float, float]) -> float:
    dx, dy = b[0] - a[0], b[1] - a[1]
    t = max(0.0, min(1.0, ((x - a[0]) * dx + (y - a[1]) * dy) / (dx * dx + dy * dy)))
    return math.hypot(x - (a[0] + t * dx), y - (a[1] + t * dy))


def mirror(cell: tuple[int, int]) -> tuple[int, int]:
    return WIDTH - 1 - cell[0], HEIGHT - 1 - cell[1]


def kind_family(kind: str) -> str:
    return kind.rsplit("_", 1)[0] if kind != "rock" else kind


def building_sites() -> list[tuple[str, int, int, int, int]]:
    blue = [("Barracks", 18, 23, 3, 3), ("Archery", 18, 38, 3, 3),
            ("HouseNorth", 5, 44, 2, 2), ("HouseSouth", 5, 18, 2, 2),
            ("Monastery", 14, 17, 3, 3), ("Tower", 19, 44, 2, 2)]
    sites = []
    for name, x, y, width, height in blue:
        sites.append((f"Blue{name}Site", x, y, width, height))
        sites.append((f"Red{name}Site", WIDTH - x - width, HEIGHT - y - height,
                      width, height))
    warehouses = [("NorthwestWarehouseSite", 36, 54),
                  ("SouthwestWarehouseSite", 36, 8),
                  ("SoutheastWarehouseSite", 58, 8),
                  ("NortheastWarehouseSite", 58, 54)]
    sites.extend((name, x, y, 2, 2) for name, x, y in warehouses)
    return sites


def site_cells() -> set[tuple[int, int]]:
    return {(x + dx, y + dy) for _, x, y, width, height in building_sites()
            for dx in range(width) for dy in range(height)}


def terrain() -> tuple[set[tuple[int, int]], set[tuple[int, int]], set[tuple[int, int]]]:
    water: set[tuple[int, int]] = set()
    for x in range(WIDTH):
        for y in range(HEIGHT):
            # Continuous sea surrounds a rounded island, including all four edges.
            dx, dy = abs(x - 47.5), abs(y - 31.5)
            coast = (dx / 44) ** 4 + (dy / 28.5) ** 4
            shore = 1 + 0.025 * math.sin(dx / 4.5) * math.cos(dy / 3.5)
            sea = (x < 4 or y < 4 or x >= WIDTH - 4 or y >= HEIGHT - 4
                   or coast > shore)
            # A northern bay and its rotated southern partner shape the coast.
            cove = ((x - 47.5) / 8) ** 2 + ((y - 63) / 9) ** 2 < 1
            cove |= ((x - 47.5) / 8) ** 2 + (y / 9) ** 2 < 1
            # Shallow inland lakes separate the central front from both flanks.
            transfer_gap = 37 <= x <= 42 or 53 <= x <= 58
            upper_lake = 29 <= x <= 66 and 40 <= y <= 44 and not transfer_gap
            lower_lake = 29 <= x <= 66 and 19 <= y <= 23 and not transfer_gap
            if sea or cove or upper_lake or lower_lake:
                water.add((x, y))
    all_cells = {(x, y) for x in range(WIDTH) for y in range(HEIGHT)}
    land = all_cells - water

    # Existing olive ground marks three broad routes and two central transfers.
    north = [(15.5, 31.5), (24, 37), (24, 47), (30, 50),
             (47.5, 50), (65, 50), (71, 47), (71, 37), (79.5, 31.5)]
    transfer = [((40, 31.5), (40, 50)), ((55, 31.5), (55, 50))]
    roads = set()
    for x, y in land:
        main = 14 <= x <= 81 and 28 <= y <= 35
        flank = min(dist_to_segment(x, y, a, b) for a, b in zip(north, north[1:])) <= 2.3
        lower = min(dist_to_segment(x, y, mirror(a), mirror(b)) for a, b in zip(north, north[1:])) <= 2.3
        crosses = any(dist_to_segment(x, y, a, b) <= 2.3 for a, b in transfer)
        crosses |= any(dist_to_segment(x, y, mirror(a), mirror(b)) <= 2.3 for a, b in transfer)
        if main or flank or lower or crosses:
            roads.add((x, y))
    sites = site_cells()
    assert sites <= land
    roads.update(sites)
    return land, water, roads


def map_objects(land: set[tuple[int, int]], water: set[tuple[int, int]],
                roads: set[tuple[int, int]]) -> tuple[list[tuple[str, int, int, str]], set[tuple[int, int]]]:
    rng = random.Random(1051)
    objects: list[tuple[str, int, int, str]] = []
    occupied: set[tuple[int, int]] = set()
    collision: set[tuple[int, int]] = set()

    def put(kind: str, x: int, y: int, name: str, blocking: bool = True) -> None:
        cell = (x, y)
        assert cell in land and cell not in occupied and cell not in roads, (name, cell)
        objects.append((kind, x, y, name))
        occupied.add(cell)
        if blocking:
            collision.add(cell)

    # Mirrored safe resources sit a short walking distance from each castle.
    for index, (x, y, kind) in enumerate([(10, 25, "ore_3"),
                                           (10, 38, "ore_4")], 1):
        put(kind, x, y, f"BlueSafeOre_{index:02}")
        rx, ry = mirror((x, y))
        put(kind, rx, ry, f"RedSafeOre_{index:02}")
    blue_trees = [(5, 30), (7, 28), (12, 26), (15, 27),
                  (5, 33), (7, 35), (12, 37), (15, 36)]
    for index, (x, y) in enumerate(blue_trees, 1):
        kind = f"tree_{(index - 1) % 4 + 1}"
        put(kind, x, y, f"BlueSafeTree_{index:02}")
        rx, ry = mirror((x, y))
        put(kind, rx, ry, f"RedSafeTree_{index:02}")

    outer_resources = [
        ("Northwest", 31, 56, "ore_5", [(27, 56), (29, 58), (33, 58), (35, 57)]),
        ("Southwest", 31, 7, "ore_6", [(27, 7), (29, 5), (33, 5), (35, 6)]),
    ]
    for area, x, y, kind, trees in outer_resources:
        put(kind, x, y, f"{area}ExpansionOre")
        rx, ry = mirror((x, y))
        partner = "Southeast" if area == "Northwest" else "Northeast"
        put(kind, rx, ry, f"{partner}ExpansionOre")
        for index, (tx, ty) in enumerate(trees, 1):
            tree_kind = f"tree_{(index - 1) % 4 + 1}"
            put(tree_kind, tx, ty, f"{area}ExpansionTree_{index:02}")
            rtx, rty = mirror((tx, ty))
            put(tree_kind, rtx, rty, f"{partner}ExpansionTree_{index:02}")

    # A few groves frame the island without reducing the reserved road widths.
    grove_boxes = [(8, 22, 48, 59), (8, 22, 5, 16),
                   (23, 37, 46, 58), (23, 37, 5, 17),
                   (27, 36, 34, 39), (27, 36, 24, 29)]
    candidates = []
    for xmin, xmax, ymin, ymax in grove_boxes:
        candidates.extend((x, y) for x in range(xmin, xmax) for y in range(ymin, ymax))
    rng.shuffle(candidates)
    grove_count = 0
    for x, y in candidates:
        if grove_count >= 24:
            break
        pair = mirror((x, y))
        if (x, y) not in land or pair not in land:
            continue
        if any((x + dx, y + dy) in occupied for dx in range(-2, 3) for dy in range(-2, 3)):
            continue
        if any((pair[0] + dx, pair[1] + dy) in occupied for dx in range(-2, 3) for dy in range(-2, 3)):
            continue
        if any((x + dx, y + dy) in roads or (pair[0] + dx, pair[1] + dy) in roads
               for dx in range(-2, 3) for dy in range(-2, 3)):
            continue
        if math.hypot(x - 12, y - 32) < 10:
            continue
        grove_count += 1
        tree_kind = f"deco_tree_{(grove_count - 1) % 4 + 1}"
        put(tree_kind, x, y, f"GroveTree_{grove_count:03}_A")
        put(tree_kind, *pair, f"GroveTree_{grove_count:03}_B")

    # Small bushes and stones decorate the land off the main paths.
    for kind, target in [("bush", 18), ("rock", 12)]:
        count = 0
        attempts = 0
        while count < target and attempts < 10000:
            attempts += 1
            x, y = rng.randrange(5, 46), rng.randrange(5, 59)
            pair = mirror((x, y))
            if (x, y) not in land or pair not in land:
                continue
            if (x, y) in occupied or pair in occupied:
                continue
            if any((x + dx, y + dy) in roads or (pair[0] + dx, pair[1] + dy) in roads
                   for dx in range(-1, 2) for dy in range(-1, 2)):
                continue
            if math.hypot(x - 12, y - 32) < 9:
                continue
            if 37 <= x <= 58 and 24 <= y <= 39:
                continue
            count += 1
            art_kind = f"bush_{(count - 1) % 4 + 1}" if kind == "bush" else "rock"
            put(art_kind, x, y, f"{kind.title()}_{count:03}_A", blocking=kind == "bush")
            put(art_kind, *pair, f"{kind.title()}_{count:03}_B", blocking=kind == "bush")

    blue_footprint = {(x, y) for x in range(10, 14) for y in range(30, 34)}
    collision.update(blue_footprint)
    collision.update(mirror(cell) for cell in blue_footprint)
    collision.update(site_cells())
    collision.update(water)
    return objects, collision


def prefab_root(relative: str) -> tuple[str, int, int]:
    text = asset_path(relative).read_text()
    _, blocks = split_blocks(text)
    transforms = {block_id(block): block for block in blocks if block.startswith("--- !u!4 &")}
    for block in blocks:
        if not block.startswith("--- !u!1 &"):
            continue
        root_id = block_id(block)
        transform_id = int(re.search(r"^  - component: \{fileID: (\d+)\}", block, re.M).group(1))
        if "m_Father: {fileID: 0}" in transforms[transform_id]:
            return guid(relative), root_id, transform_id
    raise ValueError(relative)


def prefab_instance(relative: str, name: str, x: int, y: int, parent: int,
                    instance_id: int, transform_id: int, kind: str) -> str:
    source_guid, source_go, source_transform = prefab_root(relative)
    def mod(target: int, key: str, value: str) -> str:
        return (f"    - target: {{fileID: {target}, guid: {source_guid}, type: 3}}\n"
                f"      propertyPath: {key}\n      value: {value}\n"
                "      objectReference: {fileID: 0}\n")
    def reference_mod(target: int, key: str, file_id: str,
                      asset_guid: str, asset_type: int) -> str:
        return (f"    - target: {{fileID: {target}, guid: {source_guid}, type: 3}}\n"
                f"      propertyPath: {key}\n      value: \n"
                f"      objectReference: {{fileID: {file_id}, guid: {asset_guid}, type: {asset_type}}}\n")

    art = ""
    family = kind_family(kind)
    if family == "ore":
        number = kind.rsplit("_", 1)[1]
        stem = f"GoldStone{number}"
        base = "Assets/GameAsset/World/Resources/Gold/Deposits"
        clip = f"{base}/{stem}Animation/{stem}Static.anim"
        controller = f"{base}/{stem}Animation/{stem}.controller"
        renderer_id, animator_id = 8929965606917850240, 3975628781542072407
    elif family in ("tree", "deco_tree"):
        number = kind.rsplit("_", 1)[1]
        stem = f"Tree{number}"
        base = "Assets/GameAsset/World/Props/Trees"
        clip = f"{base}/{stem}Animation/{stem}Idle.anim"
        controller = f"{base}/{stem}Animation/{stem}.controller"
        renderer_id, animator_id = 7635962768333753533, 2669905072750184304
    elif family == "bush":
        number = kind.rsplit("_", 1)[1]
        stem = f"Bush{number}"
        base = "Assets/GameAsset/World/Props/Bushes"
        clip = f"{base}/{stem}Animation/{stem}.anim"
        controller = f"{base}/{stem}Animation/{stem}.controller"
        renderer_id, animator_id = 584581702825208097, 4472131189671401105
    if family != "rock":
        clip_text = asset_path(clip).read_text()
        sprite_id, sprite_guid = re.search(
            r"value: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 3\}",
            clip_text).groups()
        art = (reference_mod(renderer_id, "m_Sprite", sprite_id, sprite_guid, 3)
               + reference_mod(animator_id, "m_Controller", "9100000", guid(controller), 2))
        if family in ("tree", "deco_tree") and int(number) >= 3:
            visual_y = "1.4" if family == "tree" else "1.05"
            art += mod(1165918597223554970, "m_LocalPosition.y", visual_y)
    return (f"--- !u!1001 &{instance_id}\nPrefabInstance:\n"
            "  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n"
            f"    serializedVersion: 3\n    m_TransformParent: {{fileID: {parent}}}\n"
            "    m_Modifications:\n"
            + mod(source_transform, "m_LocalPosition.x", str(x + 0.5))
            + mod(source_transform, "m_LocalPosition.y", str(y + 0.5))
            + mod(source_transform, "m_LocalPosition.z", "0")
            + mod(source_go, "m_Name", name)
            + art
            + "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n"
              "    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
            + f"  m_SourcePrefab: {{fileID: 100100000, guid: {source_guid}, type: 3}}\n"
            + f"--- !u!4 &{transform_id} stripped\nTransform:\n"
              f"  m_CorrespondingSourceObject: {{fileID: {source_transform}, guid: {source_guid}, type: 3}}\n"
              f"  m_PrefabInstance: {{fileID: {instance_id}}}\n  m_PrefabAsset: {{fileID: 0}}\n")


def empty_object(name: str, go_id: int, transform_id: int, parent: int,
                 children: list[int], x: float = 0, y: float = 0) -> str:
    child_lines = ("\n" + "".join(f"  - {{fileID: {child}}}\n" for child in children)) if children else " []\n"
    return (f"--- !u!1 &{go_id}\nGameObject:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            f"  serializedVersion: 6\n  m_Component:\n  - component: {{fileID: {transform_id}}}\n"
            f"  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n"
            "  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
            f"--- !u!4 &{transform_id}\nTransform:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go_id}}}\n"
            "  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
            f"  m_LocalPosition: {{x: {x}, y: {y}, z: 0}}\n"
            "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n"
            f"  m_Children:{child_lines}  m_Father: {{fileID: {parent}}}\n"
            "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n")


def castle_visual(name: str, x: int, y: int, texture: str, parent: int,
                  ids: tuple[int, int, int, int, int, int]) -> str:
    root_go, root_transform, sorting, visual_go, visual_transform, renderer = ids
    sample = asset_path(TREE_PREFAB).read_text()
    _, blocks = split_blocks(sample)
    wanted = [4199052027199651547, 6180612925226802574, 334428838828568488,
              4230124417422638063, 1165918597223554970, 7635962768333753533]
    mapping = dict(zip(wanted, ids))
    result = []
    for block in blocks:
        if block_id(block) not in mapping:
            continue
        for old, new in mapping.items():
            block = re.sub(r"(?<=&)" + str(old) + r"\b", str(new), block)
            block = block.replace(f"{{fileID: {old}}}", f"{{fileID: {new}}}")
        if block_id(block) == root_go:
            block = re.sub(r"^  - component: \{fileID: \d+\}\n(?=  m_Layer:)", "", block, flags=re.M)
            block = block.replace("m_Name: Tree_01", f"m_Name: {name}")
        elif block_id(block) == root_transform:
            block = block.replace("m_LocalPosition: {x: 0, y: 0, z: 0}",
                                  f"m_LocalPosition: {{x: {x + 0.5}, y: {y + 0.5}, z: 0}}")
            block = block.replace("m_Father: {fileID: 0}", f"m_Father: {{fileID: {parent}}}")
        elif block_id(block) == visual_go:
            block = re.sub(r"^  - component: \{fileID: \d+\}\n(?=  m_Layer:)", "", block, flags=re.M)
        elif block_id(block) == visual_transform:
            block = block.replace("m_LocalPosition: {x: 0, y: 2, z: 0}",
                                  "m_LocalPosition: {x: 0, y: 1.65, z: 0}")
        elif block_id(block) == renderer:
            png_meta = asset_path(texture + ".meta").read_text()
            sprite_id = re.search(r"^      internalID: (-?\d+)$", png_meta, re.M).group(1)
            block = re.sub(r"^  m_Sprite: .*?$",
                           f"  m_Sprite: {{fileID: {sprite_id}, guid: {guid(texture)}, type: 3}}",
                           block, flags=re.M)
        result.append(block)
    return "".join(result)


def validate_paths(land: set[tuple[int, int]], collision: set[tuple[int, int]],
                   objects: list[tuple[str, int, int, str]]) -> None:
    walkable = land - collision
    start = (17, 32)
    assert start in walkable
    reached = {start}
    pending = deque([start])
    while pending:
        x, y = pending.popleft()
        for cell in [(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)]:
            if cell in walkable and cell not in reached:
                reached.add(cell)
                pending.append(cell)
    for target in [(78, 31), (47, 31), (47, 50), (48, 13)]:
        assert target in reached, f"Unreachable route or expansion: {target}"
    for ore in [(10, 25), (10, 38), (85, 38), (85, 25),
                (31, 56), (31, 7), (64, 7), (64, 56)]:
        x, y = ore
        assert any(cell in reached for cell in [(x - 1, y), (x + 1, y),
                                                (x, y - 1), (x, y + 1)]), ore
    for kind, x, y, name in objects:
        if kind_family(kind) not in ("ore", "tree"):
            continue
        assert any(cell in reached for cell in [(x - 1, y), (x + 1, y),
                                                (x, y - 1), (x, y + 1)]), name
    for name, x, y, width, height in building_sites():
        neighbors = {(x + dx, y - 1) for dx in range(width)}
        neighbors.update((x + dx, y + height) for dx in range(width))
        neighbors.update((x - 1, y + dy) for dy in range(height))
        neighbors.update((x + width, y + dy) for dy in range(height))
        assert neighbors & reached, name
    assert all((x, y) in walkable for x in range(39, 57) for y in range(25, 39))
    assert len(reached) > 3500, len(reached)


def draw_preview(land: set[tuple[int, int]], water: set[tuple[int, int]],
                 roads: set[tuple[int, int]], objects: list[tuple[str, int, int, str]]) -> None:
    colors = {(x, y): (89, 157, 88) for x, y in land}
    colors.update({cell: (71, 171, 169) for cell in water})
    colors.update({cell: (159, 155, 99) for cell in roads})
    for kind, x, y, _ in objects:
        family = kind_family(kind)
        colors[(x, y)] = {"tree": (28, 91, 59), "deco_tree": (28, 91, 59),
                          "ore": (229, 189, 55),
                          "bush": (58, 115, 65), "rock": (104, 111, 105)}[family]
    image = Image.new("RGB", (WIDTH, HEIGHT))
    pixels = image.load()
    for (x, y), color in colors.items():
        pixels[x, HEIGHT - 1 - y] = color
    image = image.resize((WIDTH * 8, HEIGHT * 8), Image.Resampling.NEAREST)
    draw = ImageDraw.Draw(image)
    for x, y, color in [(12, 32, (40, 94, 239)), (83, 31, (222, 61, 66))]:
        cx, cy = x * 8 + 4, (HEIGHT - 1 - y) * 8 + 4
        draw.ellipse((cx - 13, cy - 13, cx + 13, cy + 13), fill=color, outline="white", width=2)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    image.save(PREVIEW)


def main() -> None:
    land, water, roads = terrain()
    objects, collision = map_objects(land, water, roads)
    validate_paths(land, collision, objects)
    assert all((x, 0) in water and (x, HEIGHT - 1) in water for x in range(WIDTH))
    assert all((0, y) in water and (WIDTH - 1, y) in water for y in range(HEIGHT))
    assert all(mirror(cell) in water for cell in water)
    assert all(mirror(cell) in roads for cell in roads)
    assert all(mirror(cell) in collision for cell in collision)

    header, source_blocks = split_blocks(SOURCE.read_text(encoding="utf-8-sig"))
    go_names = {"World", "Grid", "Ground", "Water", "Decoration", "Collision",
                "WorldObjects", "DecorationObjects", "Harvestables", "Trees", "Ores",
                "Plants", "Obstacles", "Cameras", "Main Camera"}
    keep_ids = {1, 2, 3, 4, 9223372036854775807}
    for block in source_blocks:
        if block.startswith("--- !u!1 &") and property_value(block, "m_Name") in go_names:
            keep_ids.add(block_id(block))
            keep_ids.update(map(int, re.findall(r"^  - component: \{fileID: (\d+)\}", block, re.M)))

    grass_indices, grass_sprite_array = grass_sprites(land)
    grass_assets = [("11400000", guid(GRASS_RULE))]
    road_assets = [("11400000", guid(ROAD_TILE))]
    road_sprites = [sprite(ROAD_TILE)]
    water_assets = [("11400000", guid(WATER_TILE))]
    water_sprites = [sprite(WATER_TILE)]
    collision_assets = [("11400000", guid(COLLISION_TILE))]
    collision_sprites = [sprite(COLLISION_TILE)]
    water_floor = {(x, y): 0 for x in range(WIDTH) for y in range(HEIGHT)}
    tilemaps = {
        370122077: (grass_indices, grass_assets, grass_sprite_array, (0, 0, WIDTH, HEIGHT)),
        581889747: (water_floor, water_assets, water_sprites, (0, 0, WIDTH, HEIGHT)),
        1110483795: ({cell: 0 for cell in collision}, collision_assets, collision_sprites, (0, 0, WIDTH, HEIGHT)),
        1166461970: ({cell: 0 for cell in roads}, road_assets, road_sprites, (0, 0, WIDTH, HEIGHT)),
    }

    next_id = 3000000000
    def ids(count: int) -> tuple[int, ...]:
        nonlocal next_id
        values = tuple(range(next_id, next_id + count))
        next_id += count
        return values

    player_go, player_transform = ids(2)
    castle_blue_ids = ids(6)
    castle_red_ids = ids(6)
    marker_go, marker_transform = ids(2)
    marker_specs = [("BlueUnitSpawn", 17.5, 32.5),
                    ("RedUnitSpawn", 78.5, 31.5)]
    marker_specs.extend((name, x + width / 2, y + height / 2)
                        for name, x, y, width, height in building_sites())
    marker_ids = [ids(2) for _ in marker_specs]
    parent_for = {"tree": 1330466830, "deco_tree": 1633703063,
                  "ore": 1641810560, "bush": 551337912, "rock": 1633703063}
    prefab_for = {"tree": TREE_PREFAB, "deco_tree": DECO_TREE_PREFAB,
                  "ore": ORE_PREFAB,
                  "bush": BUSH_PREFAB, "rock": ROCK_PREFAB}
    props: list[str] = []
    prop_children: dict[int, list[int]] = {parent: [] for parent in parent_for.values()}
    for kind, x, y, name in objects:
        instance_id, transform_id = ids(2)
        family = kind_family(kind)
        parent = parent_for[family]
        prop_children[parent].append(transform_id)
        props.append(prefab_instance(prefab_for[family], name, x, y, parent,
                                     instance_id, transform_id, kind))

    replacements = []
    for block in source_blocks:
        fid = block_id(block)
        if fid not in keep_ids:
            continue
        if fid in tilemaps:
            block = write_tilemap(block, *tilemaps[fid], rule_tile=fid == 370122077)
        if block.startswith("--- !u!4 &"):
            child_section = re.search(r"^  m_Children:.*?^  m_Father:", block, re.M | re.S)
            children = [int(value) for value in re.findall(r"^  - \{fileID: (\d+)\}",
                          child_section.group(0), re.M) if int(value) in keep_ids]
            children.extend(prop_children.get(fid, []))
            if fid == 1617835320:
                children.extend([player_transform, marker_transform])
            block = replace_children(block, children)
        if fid == 621859534:
            block = block.replace("m_LocalPosition: {x: 0, y: 0, z: -10}",
                                  "m_LocalPosition: {x: 12.5, y: 32.5, z: -10}")
        if fid == 621859533:
            block = re.sub(r"^  m_ClearFlags: \d+$", "  m_ClearFlags: 2", block,
                           flags=re.M)
            block = re.sub(r"^  m_BackGroundColor: .*?$",
                           "  m_BackGroundColor: {r: 0.2784314, g: 0.6705883, b: 0.6627451, a: 1}",
                           block, flags=re.M)
        replacements.append(block)

    additions = [
        empty_object("PlayerStarts", player_go, player_transform, 1617835320,
                     [castle_blue_ids[1], castle_red_ids[1]]),
        castle_visual("BlueStart_CastleVisual", 12, 32, BLUE_CASTLE,
                      player_transform, castle_blue_ids),
        castle_visual("RedStart_CastleVisual", 83, 31, RED_CASTLE,
                      player_transform, castle_red_ids),
        empty_object("MapMarkers", marker_go, marker_transform, 1617835320,
                     [transform for _, transform in marker_ids]),
    ]
    for (name, x, y), (go_id, transform_id) in zip(marker_specs, marker_ids):
        additions.append(empty_object(name, go_id, transform_id, marker_transform,
                                      [], x, y))
    scene_roots = [block for block in replacements if block.startswith("--- !u!1660057539 &")]
    replacements = [block for block in replacements if not block.startswith("--- !u!1660057539 &")]
    staged_scene = ROOT / "Tools/.Senen1.generated.tmp"
    staged_scene.write_text(header + "".join(replacements + additions + props + scene_roots),
                            encoding="utf-8", newline="\n")
    staged_scene.replace(SCENE)
    meta = SCENE.with_suffix(".unity.meta")
    if not meta.exists():
        meta.write_text("fileFormatVersion: 2\n" + f"guid: {uuid.uuid4().hex}\n"
                        + "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
                        + "  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    draw_preview(land, water, roads, objects)
    preview_meta = PREVIEW.with_suffix(".png.meta")
    preview_guid = (re.search(r"^guid: ([0-9a-f]{32})$", preview_meta.read_text(), re.M).group(1)
                    if preview_meta.exists() else uuid.uuid4().hex)
    source_meta = asset_path("Assets/GameAsset/World/Terrain/Tilesets/BaseTerrain/Textures/WaterBackground.png.meta").read_text()
    source_meta = source_meta.split("AssetOrigin:")[0].rstrip() + "\n"
    source_meta = re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {preview_guid}", source_meta, count=1, flags=re.M)
    preview_meta.write_text(source_meta, encoding="utf-8")
    print(f"Senen1: {len(land)} ground, {len(water_floor)} water-floor, "
          f"{len(water)} exposed-water, {len(roads)} road, "
          f"{len(collision)} collision cells, {len(objects)} world props")
    print(f"Scene: {SCENE} ({SCENE.stat().st_size / 1048576:.1f} MiB)")
    print(f"Preview: {PREVIEW}")


if __name__ == "__main__":
    main()
