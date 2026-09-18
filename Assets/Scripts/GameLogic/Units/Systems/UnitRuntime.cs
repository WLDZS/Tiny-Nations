using BorFramework;

namespace GameLogic.Units
{
    internal sealed class UnitRuntime
    {
        public IAssetLease<UnitDefinition> DefinitionLease { get; }

        public IInstanceLease InstanceLease { get; }

        public UnitRuntime(
            IAssetLease<UnitDefinition> definitionLease,
            IInstanceLease instanceLease)
        {
            DefinitionLease = definitionLease;
            InstanceLease = instanceLease;
        }
    }
}
