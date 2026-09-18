namespace GameLogic.Units.Common
{
    internal sealed class UnitAttributeValue
    {
        public EUnitAttributeKind Kind { get; }

        public float BaseValue { get; private set; }

        public float CurrentValue { get; private set; }

        public bool HasMaximum { get; }

        public EUnitAttributeType MaximumAttributeType { get; }

        public UnitAttributeValue(
            EUnitAttributeKind kind,
            float baseValue,
            bool hasMaximum,
            EUnitAttributeType maximumAttributeType)
        {
            Kind = kind;
            BaseValue = baseValue;
            CurrentValue = baseValue;
            HasMaximum = hasMaximum;
            MaximumAttributeType = maximumAttributeType;
        }

        public void SetBaseAndCurrentValue(float value)
        {
            BaseValue = value;
            CurrentValue = value;
        }

        public void SetBaseValue(float value)
        {
            BaseValue = value;
        }

        public void SetCurrentValue(float value)
        {
            CurrentValue = value;
        }
    }
}
