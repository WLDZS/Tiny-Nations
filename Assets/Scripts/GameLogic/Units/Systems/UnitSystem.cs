using System.Collections.Generic;
using BorFramework;
using Cysharp.Threading.Tasks;
using GameLogic.Units.Common;
using GameLogic.Units.Skills;
using GameLogic.Navigation;
using UnityEngine;

namespace GameLogic.Units
{
    public sealed class UnitSystem : IUnitSystem
    {
        private readonly IResourceModule _resourceModule;
        private readonly IPrefabPoolModule _prefabPoolModule;
        private readonly IEntityModule _entityModule;
        private readonly IInputModule _inputModule;
        private readonly IEventModule _eventModule;
        private readonly IMonoModule _monoModule;
        private readonly INavigationSystem _navigation;
        private readonly UnitApproachSlots _approachSlots = new();
        private readonly Dictionary<UnitEntity, UnitRuntime> _units = new();
        private readonly Dictionary<EntityId, UnitEntity> _unitsByRigidbodyEntityId = new();
        private readonly List<UnitEntity> _despawnBuffer = new();
        private readonly List<UnitEntity> _pendingDeathUnits = new();
        private bool _started;
        private int _lifecycleVersion;

        public UnitSystem(
            IResourceModule resourceModule,
            IPrefabPoolModule prefabPoolModule,
            IEntityModule entityModule,
            IInputModule inputModule,
            IEventModule eventModule,
            IMonoModule monoModule,
            INavigationSystem navigation)
        {
            _resourceModule = resourceModule;
            _prefabPoolModule = prefabPoolModule;
            _entityModule = entityModule;
            _inputModule = inputModule;
            _eventModule = eventModule;
            _monoModule = monoModule;
            _navigation = navigation;
        }

        public void Init()
        {
        }

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            _lifecycleVersion++;

            _eventModule?.Subscribe<UnitDamageEvent>(OnUnitDamaged);
            _eventModule?.Subscribe<UnitDeathEvent>(OnUnitDied);
            if (_monoModule != null)
                _monoModule.OnLateUpdate += OnLateUpdate;
        }

        public void Stop()
        {
            if (!_started)
                return;

            _started = false;
            _lifecycleVersion++;

            _eventModule?.Unsubscribe<UnitDamageEvent>(OnUnitDamaged);
            _eventModule?.Unsubscribe<UnitDeathEvent>(OnUnitDied);
            if (_monoModule != null)
                _monoModule.OnLateUpdate -= OnLateUpdate;

            _pendingDeathUnits.Clear();
            _despawnBuffer.Clear();
            _approachSlots.Clear();
            _despawnBuffer.AddRange(_units.Keys);

            for (int i = 0; i < _despawnBuffer.Count; i++)
                Despawn(_despawnBuffer[i]);

            _despawnBuffer.Clear();
        }

        public void Dispose()
        {
            Stop();
        }

        public async UniTask<UnitEntity> SpawnAsync(UnitSpawnRequest request)
        {
            if (!_started || string.IsNullOrWhiteSpace(request.DefinitionAddress))
                return null;

            int lifecycleVersion = _lifecycleVersion;
            IAssetLease<UnitDefinition> definitionLease =
                await _resourceModule.LoadAssetAsync<UnitDefinition>(request.DefinitionAddress);

            if (!IsCurrent(lifecycleVersion))
            {
                definitionLease?.Dispose();
                return null;
            }

            if (definitionLease == null || !definitionLease.IsValid)
                return null;

            UnitDefinition definition = definitionLease.Asset;
            if (string.IsNullOrWhiteSpace(definition.PrefabAddress))
            {
                Debug.LogError(
                    $"单位配置缺少预制体地址：{request.DefinitionAddress}",
                    definition);
                definitionLease.Dispose();
                return null;
            }

            if (!TryValidateSkills(definition, out string skillError))
            {
                Debug.LogError($"单位技能配置无效：{request.DefinitionAddress}，{skillError}", definition);
                definitionLease.Dispose();
                return null;
            }

            if (!UnitAttributeComp.TryCreate(
                    definition.AttributeSet,
                    out UnitAttributeComp attributes,
                    out string attributeError))
            {
                Debug.LogError(
                    $"单位属性配置无效：{request.DefinitionAddress}，{attributeError}",
                    definition);
                definitionLease.Dispose();
                return null;
            }

            GameObject instance = await _prefabPoolModule.RentAsync(
                definition.PrefabAddress,
                request.Position,
                request.Rotation,
                null);

            if (!IsCurrent(lifecycleVersion))
            {
                if (instance != null)
                    _prefabPoolModule.Return(instance);

                definitionLease.Dispose();
                return null;
            }

            if (instance == null)
            {
                definitionLease.Dispose();
                return null;
            }

            Transform worldPositionTransform = instance.transform.Find("WordPos");
            if (worldPositionTransform == null)
                worldPositionTransform = instance.transform;

            instance.transform.position += request.Position - worldPositionTransform.position;

            Animator animator = instance.GetComponentInChildren<Animator>(true);
            SpriteRenderer spriteRenderer = instance.GetComponentInChildren<SpriteRenderer>(true);
            if (animator == null || spriteRenderer == null)
            {
                Debug.LogError(
                    $"单位预制体缺少Animator或SpriteRenderer：{definition.PrefabAddress}",
                    instance);
                _prefabPoolModule.Return(instance);
                definitionLease.Dispose();
                return null;
            }

            Rigidbody2D rigidbody = instance.GetComponent<Rigidbody2D>();
            Collider2D bodyCollider = instance.GetComponent<Collider2D>();
            if ((rigidbody == null) != (bodyCollider == null))
            {
                Debug.LogError(
                    $"单位预制体的Rigidbody2D与Collider2D必须成对配置：{definition.PrefabAddress}",
                    instance);
                _prefabPoolModule.Return(instance);
                definitionLease.Dispose();
                return null;
            }

            PrepareInactiveInstance(
                spriteRenderer,
                rigidbody);

            var unit = new UnitEntity(
                instance,
                worldPositionTransform,
                animator,
                spriteRenderer,
                rigidbody,
                bodyCollider,
                definition,
                attributes,
                request.TeamId,
                _inputModule,
                this,
                _navigation,
                this,
                _eventModule,
                request.UsePlayerInput);
            EntityId? rigidbodyEntityId = rigidbody != null
                ? rigidbody.GetEntityId()
                : null;
            if (_entityModule.AddEntity(unit) == null)
            {
                unit.Dispose();
                _prefabPoolModule.Return(instance);
                definitionLease.Dispose();
                Debug.LogError($"单位注册失败：{request.DefinitionAddress}");
                return null;
            }

            _units.Add(unit, new UnitRuntime(
                definitionLease,
                instance,
                worldPositionTransform,
                bodyCollider,
                rigidbodyEntityId));

            if (rigidbodyEntityId.HasValue)
                _unitsByRigidbodyEntityId.Add(rigidbodyEntityId.Value, unit);

            instance.SetActive(true);
            IgnoreOtherUnitBodyCollisions(bodyCollider);
            ResetAnimator(definition, animator);
            return unit;
        }

        public bool Despawn(UnitEntity unit)
        {
            if (unit == null || !_units.Remove(unit, out UnitRuntime runtime))
                return false;

            _approachSlots.Release(unit);
            _approachSlots.ReleaseTarget(unit);

            if (runtime.RigidbodyEntityId.HasValue)
            {
                _unitsByRigidbodyEntityId.Remove(
                    runtime.RigidbodyEntityId.Value);
            }

            if (!_entityModule.RemoveEntity(unit, () => ReleaseRuntime(runtime)))
            {
                unit.Dispose();
                ReleaseRuntime(runtime);
            }

            return true;
        }

        private void ReleaseRuntime(UnitRuntime runtime)
        {
            _prefabPoolModule.Return(runtime.Instance);
            runtime.DefinitionLease.Dispose();
        }

        private static bool TryValidateSkills(UnitDefinition definition, out string errorMessage)
        {
            var slots = new HashSet<ESkillSlot>();
            for (int i = 0; i < definition.Skills.Count; i++)
            {
                SkillConfig skill = definition.Skills[i];
                if (skill == null)
                {
                    errorMessage = $"技能列表第 {i} 项为空。";
                    return false;
                }

                if (!skill.TryValidate(out string skillError))
                {
                    errorMessage = $"技能 {skill.name}：{skillError}";
                    return false;
                }

                if (!slots.Add(skill.Slot))
                {
                    errorMessage = $"技能槽重复：{skill.Slot}。";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private bool IsCurrent(int lifecycleVersion)
        {
            return _started && lifecycleVersion == _lifecycleVersion;
        }

        private static void PrepareInactiveInstance(
            SpriteRenderer spriteRenderer,
            Rigidbody2D rigidbody)
        {
            spriteRenderer.flipX = false;
            spriteRenderer.SetPropertyBlock(null);

            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector2.zero;
                rigidbody.angularVelocity = 0f;
            }
        }

        private static void ResetAnimator(
            UnitDefinition definition,
            Animator animator)
        {
            int idleStateId = Animator.StringToHash(definition.IdleAnimationStateName);
            animator.Play(idleStateId, 0, 0f);
            animator.Update(0f);
        }

        private void IgnoreOtherUnitBodyCollisions(Collider2D bodyCollider)
        {
            if (bodyCollider == null)
                return;

            // 当前单位只与地图发生身体碰撞。
            foreach (UnitRuntime runtime in _units.Values)
            {
                Collider2D otherBodyCollider = runtime.BodyCollider;
                if (otherBodyCollider == null || otherBodyCollider == bodyCollider)
                    continue;

                Physics2D.IgnoreCollision(
                    bodyCollider,
                    otherBodyCollider,
                    true);
            }
        }

        public bool TryGetUnit(Collider2D collider, out UnitEntity unit)
        {
            unit = null;
            if (collider == null || collider.attachedRigidbody == null)
                return false;

            EntityId rigidbodyEntityId = collider.attachedRigidbody.GetEntityId();
            return _unitsByRigidbodyEntityId.TryGetValue(
                rigidbodyEntityId,
                out unit);
        }

        internal void CopyActiveUnits(List<UnitEntity> units)
        {
            units.Clear();
            foreach (UnitEntity unit in _units.Keys)
            {
                if (!unit.Life.IsDead)
                    units.Add(unit);
            }
        }

        internal bool IsActiveUnit(UnitEntity unit)
        {
            return unit != null && !unit.Life.IsDead && _units.ContainsKey(unit);
        }

        internal bool TryIssueMoveCommand(UnitEntity unit, Vector2 destination)
        {
            return _started && IsActiveUnit(unit) && unit.TryMoveTo(destination);
        }

        internal bool TryGetSelectionRenderer(UnitEntity unit, out SpriteRenderer renderer)
        {
            renderer = null;
            if (!IsActiveUnit(unit)
                || !_units.TryGetValue(unit, out UnitRuntime runtime)
                || runtime.Instance == null)
                return false;

            renderer = runtime.Instance.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
        }

        public bool TryGetUnitTransform(UnitEntity unit, out Transform transform)
        {
            transform = null;
            if (unit == null
                || !_units.TryGetValue(unit, out UnitRuntime runtime)
                || runtime.Instance == null)
            {
                return false;
            }

            transform = runtime.Instance.transform;
            return true;
        }

        public bool TryGetUnitWorldPosition(UnitEntity unit, out Vector3 position)
        {
            position = default;
            if (unit == null
                || !_units.TryGetValue(unit, out UnitRuntime runtime)
                || runtime.WorldPositionTransform == null)
            {
                return false;
            }

            position = runtime.WorldPositionTransform.position;
            return true;
        }

        public bool TryGetUnitAttackFootprint(
            UnitEntity unit,
            out Vector2 center,
            out float radius)
        {
            center = default;
            radius = 0f;
            if (unit == null
                || !_units.TryGetValue(unit, out UnitRuntime runtime)
                || runtime.Instance == null
                || !runtime.Instance.activeInHierarchy
                || runtime.WorldPositionTransform == null)
            {
                return false;
            }

            center = runtime.WorldPositionTransform.position;
            Collider2D bodyCollider = runtime.BodyCollider;
            if (bodyCollider == null || !bodyCollider.enabled)
                return true;

            Bounds bounds = bodyCollider.bounds;
            center = bounds.center;
            radius = bodyCollider is CircleCollider2D
                ? Mathf.Max(bounds.extents.x, bounds.extents.y)
                : bounds.extents.magnitude;
            return true;
        }

        public bool IsTargetInAttackRange(
            UnitEntity source,
            UnitEntity target,
            float attackRange)
        {
            if (attackRange <= 0f
                || !TryGetUnitAttackFootprint(source, out Vector2 sourceCenter, out float sourceRadius)
                || !TryGetUnitAttackFootprint(target, out Vector2 targetCenter, out float targetRadius))
            {
                return false;
            }

            float reach = sourceRadius + attackRange + targetRadius;
            return (targetCenter - sourceCenter).sqrMagnitude <= reach * reach;
        }

        public bool TryGetApproachPosition(
            UnitEntity source,
            UnitEntity target,
            float attackRange,
            out Vector2 position,
            out bool canAttack)
        {
            position = default;
            canAttack = false;
            if (source == null || target == null || target.Life.IsDead
                || _navigation?.Map == null
                || !TryGetUnitAttackFootprint(source, out Vector2 sourceCenter, out float sourceRadius)
                || !TryGetUnitAttackFootprint(target, out Vector2 targetCenter, out float targetRadius))
            {
                return false;
            }

            return _approachSlots.TryGetPosition(
                source, target, sourceCenter, sourceRadius, targetCenter, targetRadius,
                attackRange, _navigation.Map, out position, out canAttack);
        }

        public void ReleaseApproachPosition(UnitEntity source)
        {
            _approachSlots.Release(source);
        }

        public Vector2 GetLocalSeparation(UnitEntity source)
        {
            if (!TryGetUnitAttackFootprint(source, out Vector2 sourceCenter, out float sourceRadius))
                return Vector2.zero;

            float sourceCrowdRadius = Mathf.Max(0.22f, sourceRadius);
            Vector2 separation = Vector2.zero;
            foreach (UnitEntity other in _units.Keys)
            {
                if (other == source || other.Life.IsDead
                    || !TryGetUnitAttackFootprint(other, out Vector2 otherCenter, out float otherRadius))
                {
                    continue;
                }

                Vector2 offset = sourceCenter - otherCenter;
                float distance = offset.magnitude;
                float spacing = sourceCrowdRadius + Mathf.Max(0.22f, otherRadius) + 0.08f;
                if (distance >= spacing)
                    continue;

                if (distance < 0.001f)
                {
                    EntityId sourceId = _units[source].Instance.GetEntityId();
                    EntityId otherId = _units[other].Instance.GetEntityId();
                    offset = sourceId < otherId ? Vector2.left : Vector2.right;
                    distance = 0f;
                }

                separation += offset.normalized * ((spacing - distance) / spacing);
            }

            return Vector2.ClampMagnitude(separation, 1f);
        }

        public bool TryFindClosestEnemyInAttackRange(
            UnitEntity source,
            float attackRange,
            out UnitEntity unit)
        {
            unit = null;
            if (source == null || attackRange <= 0f
                || !TryGetUnitAttackFootprint(source, out Vector2 sourceCenter, out float sourceRadius))
            {
                return false;
            }

            float closestGap = float.PositiveInfinity;
            foreach (KeyValuePair<UnitEntity, UnitRuntime> pair in _units)
            {
                UnitEntity candidate = pair.Key;
                if (candidate == null
                    || candidate.Life.IsDead
                    || !candidate.Attributes.TryGetCurrentValue(
                        EUnitAttributeType.Health,
                        out float health)
                    || health <= 0f
                    || !TryGetRelation(source, candidate, out EUnitRelation relation)
                    || relation != EUnitRelation.Enemy
                    || !TryGetUnitAttackFootprint(candidate, out Vector2 targetCenter, out float targetRadius))
                {
                    continue;
                }

                float gap = Mathf.Max(0f,
                    Vector2.Distance(sourceCenter, targetCenter) - sourceRadius - targetRadius);
                if (gap > attackRange || gap >= closestGap)
                    continue;

                closestGap = gap;
                unit = candidate;
            }

            return unit != null;
        }

#if DEBUG
        internal void CopyDebugUnitPositions(List<Vector3> positions)
        {
            positions.Clear();
            foreach (UnitRuntime runtime in _units.Values)
            {
                if (runtime.Instance != null
                    && runtime.Instance.activeInHierarchy
                    && runtime.WorldPositionTransform != null)
                {
                    positions.Add(runtime.WorldPositionTransform.position);
                }
            }
        }
#endif

        public bool TryFindClosestEnemy(
            UnitEntity source,
            Vector3 origin,
            float maxDistance,
            out UnitEntity unit)
        {
            unit = null;
            if (source == null || maxDistance <= 0f)
                return false;

            float closestDistanceSquared = maxDistance * maxDistance;
            foreach (KeyValuePair<UnitEntity, UnitRuntime> pair in _units)
            {
                UnitEntity candidate = pair.Key;
                GameObject instance = pair.Value.Instance;
                Transform worldPositionTransform = pair.Value.WorldPositionTransform;
                if (candidate == null
                    || candidate.Life.IsDead
                    || instance == null
                    || worldPositionTransform == null
                    || !TryGetRelation(source, candidate, out EUnitRelation relation)
                    || relation != EUnitRelation.Enemy)
                {
                    continue;
                }

                float distanceSquared = (worldPositionTransform.position - origin).sqrMagnitude;
                if (distanceSquared > closestDistanceSquared)
                    continue;

                closestDistanceSquared = distanceSquared;
                unit = candidate;
            }

            return unit != null;
        }

        public bool TryGetRelation(
            UnitEntity source,
            UnitEntity target,
            out EUnitRelation relation)
        {
            relation = default;
            if (source == null || target == null)
                return false;

            if (source == target)
            {
                relation = EUnitRelation.Self;
                return true;
            }

            relation = source.Team.TeamId == target.Team.TeamId
                ? EUnitRelation.Ally
                : EUnitRelation.Enemy;
            return true;
        }

        private void OnUnitDamaged(UnitDamageEvent damageEvent)
        {
            damageEvent.Target?.DamageFlash.Play();
        }

        private void OnUnitDied(UnitDeathEvent deathEvent)
        {
            UnitEntity target = deathEvent.Target;
            if (target == null
                || !_units.ContainsKey(target)
                || _pendingDeathUnits.Contains(target))
            {
                return;
            }

            _pendingDeathUnits.Add(target);
        }

        private void OnLateUpdate(float _)
        {
            if (!_started)
                return;

            for (int i = 0; i < _pendingDeathUnits.Count; i++)
                Despawn(_pendingDeathUnits[i]);

            _pendingDeathUnits.Clear();
        }
    }
}
