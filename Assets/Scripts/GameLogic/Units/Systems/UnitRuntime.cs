using BorFramework;
using UnityEngine;

namespace GameLogic.Units
{
    internal sealed class UnitRuntime
    {
        public IAssetLease<UnitDefinition> DefinitionLease { get; }

        public GameObject Instance { get; }

        public Transform WorldPositionTransform { get; }

        public Collider2D BodyCollider { get; }

        public EntityId? RigidbodyEntityId { get; }

        public UnitRuntime(
            IAssetLease<UnitDefinition> definitionLease,
            GameObject instance,
            Transform worldPositionTransform,
            Collider2D bodyCollider,
            EntityId? rigidbodyEntityId)
        {
            DefinitionLease = definitionLease;
            Instance = instance;
            WorldPositionTransform = worldPositionTransform;
            BodyCollider = bodyCollider;
            RigidbodyEntityId = rigidbodyEntityId;
        }
    }
}
