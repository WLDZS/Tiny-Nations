using System.Collections.Generic;
using BorFramework;
using Cysharp.Threading.Tasks;
using GameLogic.Units.Common;
using UnityEngine;

namespace GameLogic.Units
{
    public sealed class UnitSystem : IUnitSystem
    {
        private readonly IResourceModule _resourceModule;
        private readonly IEntityModule _entityModule;
        private readonly IInputModule _inputModule;
        private readonly Dictionary<UnitEntity, UnitRuntime> _units = new();
        private readonly List<UnitEntity> _despawnBuffer = new();
        private bool _started;
        private int _lifecycleVersion;

        public UnitSystem(
            IResourceModule resourceModule,
            IEntityModule entityModule,
            IInputModule inputModule)
        {
            _resourceModule = resourceModule;
            _entityModule = entityModule;
            _inputModule = inputModule;
        }

        public void Init()
        {
        }

        public void Start()
        {
            _started = true;
            _lifecycleVersion++;
        }

        public void Stop()
        {
            if (!_started)
                return;

            _started = false;
            _lifecycleVersion++;
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

        public async UniTask<UnitEntity> SpawnAsync(
            string definitionAddress,
            Vector3 position,
            Quaternion rotation,
            bool usePlayerInput)
        {
            if (!_started || string.IsNullOrWhiteSpace(definitionAddress))
                return null;

            int lifecycleVersion = _lifecycleVersion;
            IAssetLease<UnitDefinition> definitionLease =
                await _resourceModule.LoadAssetAsync<UnitDefinition>(definitionAddress);

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
                Debug.LogError($"单位配置缺少预制体地址：{definitionAddress}", definition);
                definitionLease.Dispose();
                return null;
            }

            if (!UnitAttributeComp.TryCreate(
                    definition.AttributeSet,
                    out UnitAttributeComp attributes,
                    out string attributeError))
            {
                Debug.LogError(
                    $"单位属性配置无效：{definitionAddress}，{attributeError}",
                    definition);
                definitionLease.Dispose();
                return null;
            }

            IInstanceLease instanceLease = await _resourceModule.InstantiateAsync(
                definition.PrefabAddress,
                position,
                rotation,
                null);

            if (!IsCurrent(lifecycleVersion))
            {
                instanceLease?.Dispose();
                definitionLease.Dispose();
                return null;
            }

            if (instanceLease == null || !instanceLease.IsValid)
            {
                definitionLease.Dispose();
                return null;
            }

            Animator animator = instanceLease.Instance.GetComponentInChildren<Animator>(true);
            SpriteRenderer spriteRenderer = instanceLease.Instance.GetComponentInChildren<SpriteRenderer>(true);
            if (animator == null || spriteRenderer == null)
            {
                Debug.LogError(
                    $"单位预制体缺少Animator或SpriteRenderer：{definition.PrefabAddress}",
                    instanceLease.Instance);
                instanceLease.Dispose();
                definitionLease.Dispose();
                return null;
            }

            Rigidbody2D rigidbody = instanceLease.Instance.GetComponent<Rigidbody2D>();
            Collider2D bodyCollider = instanceLease.Instance.GetComponent<Collider2D>();
            if ((rigidbody == null) != (bodyCollider == null))
            {
                Debug.LogError(
                    $"单位预制体的Rigidbody2D与Collider2D必须成对配置：{definition.PrefabAddress}",
                    instanceLease.Instance);
                instanceLease.Dispose();
                definitionLease.Dispose();
                return null;
            }

            var unit = new UnitEntity(
                instanceLease.Instance,
                animator,
                spriteRenderer,
                rigidbody,
                bodyCollider,
                definition,
                attributes,
                _inputModule,
                usePlayerInput);
            _entityModule.AddEntity(unit);
            _units.Add(unit, new UnitRuntime(definitionLease, instanceLease));
            return unit;
        }

        public bool Despawn(UnitEntity unit)
        {
            if (unit == null || !_units.Remove(unit, out UnitRuntime runtime))
                return false;

            _entityModule.RemoveEntity(unit);
            runtime.InstanceLease.Dispose();
            runtime.DefinitionLease.Dispose();
            return true;
        }

        private bool IsCurrent(int lifecycleVersion)
        {
            return _started && lifecycleVersion == _lifecycleVersion;
        }

    }
}
