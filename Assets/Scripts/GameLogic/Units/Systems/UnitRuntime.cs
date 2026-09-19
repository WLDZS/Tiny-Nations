using BorFramework;
using UnityEngine;

namespace GameLogic.Units
{
    internal sealed class UnitRuntime
    {
        public IAssetLease<UnitDefinition> DefinitionLease { get; }

        public GameObject Instance { get; }

        public EntityId? RigidbodyEntityId { get; }

        public UnitRuntime(
            IAssetLease<UnitDefinition> definitionLease,
            GameObject instance,
            EntityId? rigidbodyEntityId)
        {
            DefinitionLease = definitionLease;
            Instance = instance;
            RigidbodyEntityId = rigidbodyEntityId;
        }
    }
}
