---
name: create-unit-prefab
description: Create or update a data-driven Unit prefab in Tiny-Nations, including its attributes, Animator states, UnitDefinition, skill ScriptableObjects, YooAsset wiring, and runtime verification. Use when asked to add, make, copy, configure, or repair a Unit or 兵种 asset in this repository. Do not use for combat-system design that does not create or modify Unit assets.
---

# Create Unit Prefab

适配规范版本：`1.17`

Create the requested Unit through the project's existing data-driven ELC pipeline.

## Required reading

Before changing Unit assets, read `../../../Assets/Doc/UnitPrefabAuthoring.md` completely. Treat it as the canonical specification.

Then inspect only the current files needed for the requested Unit:

- the closest reference Unit listed in the specification;
- `UnitDefinition`, `UnitAttributeSetConfig`, and the relevant `SkillConfig` subclasses;
- `UnitSystem` when the loading contract needs confirmation;
- source Sprite, Animation Clip, Animator Controller, and YooAsset collector entries involved in the change.

Do not rely on a copied asset's serialized values without checking its referenced GUIDs and animation state names.

## Workflow

1. Determine whether the current generic Unit and skill configurations already express the requested behavior.
2. Reuse the nearest reference pattern: WarriorBlue for a multi-stage melee presentation, Skull for a single-stage melee presentation.
3. Create or reuse a valid attribute SO. Every Unit requires Health, MaxHealth, and MoveSpeed; initial Health and MaxHealth must be positive. Mana and MaxMana are an optional pair.
4. Create the Animator states, skill SOs, foot-anchored presentation Prefab with a direct empty `WordPos` child, and `UnitDefinition` described by the specification. When real physical collision is requested, configure the root physics pair and verify physics-driven movement according to the specification. Add a Hurtbox only when another physical hit query requires it; current melee damage does not require one. Configure melee attack range and target relations explicitly; ordinary attacks target `Enemy`. Verify that the skill attack range is the single source for damage and the point where melee AI stops following a target. Verify that the Unit can follow a reachable target around static blocked cells.
5. Keep Unit differences in assets and configuration. Do not add Unit-specific Entity, Comp, Logic, Controller, or `MonoBehaviour` when the existing generic pipeline is sufficient.
6. If a genuinely new capability is required, explain the missing general capability and implement only the smallest reusable extension authorized by the request.
7. Preserve existing `.meta` files and GUIDs. Keep every project-owned C# type in its own script.
8. Verify that invalid skills and duplicate slots fail before spawning with a useful diagnostic. Check repeated animation stages restart their state, and that despawning before the first tick or during an update stops and disposes behavior before returning the Prefab. Verify attribute initialization, affected references, compilation, YooAsset discovery, spawning, `WordPos`-based world position and root fallback, pooled Prefab reuse and reset, runtime `TeamId`, requested animation sequence, configured skill timing and cooldown behavior, positive melee attack range, AI moving toward a visible target on the navigation grid and stopping inside that range, single locked target across hit windows, facing, target relations, GameEffect references, fatal damage and deferred death despawn, Scene range visualization, and any requested collision behavior in proportion to the change.

## Completion report

Report:

- which Prefab, Definition, attribute, Animator, animation, and skill assets were added or changed;
- whether the Unit was discovered and spawned through the YooAsset debug panel;
- which runtime `TeamId` was used and whether ally/enemy filtering was observed when combat targeting changed;
- whether fatal damage emitted death once, stopped the Unit, and returned its Prefab instance at the end of the frame;
- whether a later spawn reused the Prefab instance without retaining Animator, facing, material, or physics state;
- which animation behaviors were actually observed;
- any part that could not be verified in the current environment.

Do not claim visual or runtime acceptance from file inspection alone.

## Keep this Skill synchronized

Whenever `Assets/Doc/UnitPrefabAuthoring.md` changes in a way that affects workflow, responsibilities, paths, naming, configuration, YooAsset collection, or acceptance criteria:

1. update this Skill in the same change;
2. increment this Skill's adapted specification version to match the document version;
3. keep detailed project facts in the document and keep this file focused on execution;
4. run `python -X utf8 .agents/skills/create-unit-prefab/scripts/validate_sync.py`;
5. validate the Skill after editing it.
