using System.Collections.Generic;
using BorFramework;
using Cysharp.Threading.Tasks;
using GameLogic.Navigation;
using GameLogic.Units.Common;
using GameLogic.Units.Effects;
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
        private readonly INavigationSystem _navigationSystem;
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
            INavigationSystem navigationSystem)
        {
            _resourceModule = resourceModule;
            _prefabPoolModule = prefabPoolModule;
            _entityModule = entityModule;
            _inputModule = inputModule;
            _eventModule = eventModule;
            _monoModule = monoModule;
            _navigationSystem = navigationSystem;
        }

        public void Init()
        {
        }

        public void Start()
        {
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
                animator,
                spriteRenderer,
                rigidbody,
                bodyCollider,
                definition,
                attributes,
                request.TeamId,
                _inputModule,
                this,
                this,
                _eventModule,
                _navigationSystem,
                request.UsePlayerInput);
            EntityId? rigidbodyEntityId = rigidbody != null
                ? rigidbody.GetEntityId()
                : null;
            _entityModule.AddEntity(unit);
            _units.Add(unit, new UnitRuntime(
                definitionLease,
                instance,
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

            if (runtime.RigidbodyEntityId.HasValue)
            {
                _unitsByRigidbodyEntityId.Remove(
                    runtime.RigidbodyEntityId.Value);
            }

            _entityModule.RemoveEntity(unit);
            _prefabPoolModule.Return(runtime.Instance);
            runtime.DefinitionLease.Dispose();
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
                if (candidate == null
                    || candidate.Life.IsDead
                    || instance == null
                    || !TryGetRelation(source, candidate, out EUnitRelation relation)
                    || relation != EUnitRelation.Enemy)
                {
                    continue;
                }

                float distanceSquared = (instance.transform.position - origin).sqrMagnitude;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string sourceName = GetUnitName(damageEvent.Source, "环境");
            string targetName = GetUnitName(damageEvent.Target, "未知单位");
            string effectName = damageEvent.GameEffect != null
                ? damageEvent.GameEffect.name
                : "未知效果";
            string remainingHealth = damageEvent.Target != null
                                     && damageEvent.Target.Attributes.TryGetCurrentValue(
                                          EUnitAttributeType.Health,
                                          out float health)
                ? health.ToString("0.##")
                : "未知";
            string message =
                $"[单位伤害] {sourceName} 对 {targetName} 造成 "
                + $"{damageEvent.ActualDamage:0.##} 点伤害，"
                + $"剩余生命：{remainingHealth}，效果：{effectName}";

            if (damageEvent.Target != null
                && _units.TryGetValue(
                    damageEvent.Target,
                    out UnitRuntime targetRuntime)
                && targetRuntime.Instance != null)
            {
                Debug.Log(message, targetRuntime.Instance);
                return;
            }

            Debug.Log(message);
#endif
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string sourceName = GetUnitName(deathEvent.Source, "环境");
            string targetName = GetUnitName(target, "未知单位");
            string effectName = deathEvent.KillingEffect != null
                ? deathEvent.KillingEffect.name
                : "未知效果";
            string message =
                $"[单位死亡] {targetName} 被 {sourceName} 击杀，效果：{effectName}";

            if (_units.TryGetValue(target, out UnitRuntime targetRuntime)
                && targetRuntime.Instance != null)
            {
                Debug.Log(message, targetRuntime.Instance);
            }
            else
            {
                Debug.Log(message);
            }
#endif
        }

        private void OnLateUpdate(float dt)
        {
            if (!_started || _pendingDeathUnits.Count == 0)
                return;

            for (int i = 0; i < _pendingDeathUnits.Count; i++)
                Despawn(_pendingDeathUnits[i]);

            _pendingDeathUnits.Clear();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string GetUnitName(UnitEntity unit, string fallbackName)
        {
            if (unit != null
                && _units.TryGetValue(unit, out UnitRuntime runtime)
                && runtime.Instance != null)
            {
                string unitName = runtime.Instance.name;
                return $"{unitName}[Team {unit.Team.TeamId}]";
            }

            return fallbackName;
        }
#endif
    }
}
