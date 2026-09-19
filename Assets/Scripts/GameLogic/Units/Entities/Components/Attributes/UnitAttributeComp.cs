using System.Collections.Generic;
using BorFramework;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitAttributeComp : Comp
    {
        private readonly Dictionary<EUnitAttributeType, UnitAttributeValue> _attributes;

        private UnitAttributeComp(Dictionary<EUnitAttributeType, UnitAttributeValue> attributes)
        {
            _attributes = attributes;
        }

        public static bool TryCreate(
            UnitAttributeSetConfig config,
            out UnitAttributeComp attributeComp,
            out string errorMessage)
        {
            attributeComp = null;
            errorMessage = string.Empty;

            if (config == null)
            {
                errorMessage = "缺少 UnitAttributeSetConfig。";
                return false;
            }

            var attributes = new Dictionary<EUnitAttributeType, UnitAttributeValue>();
            IReadOnlyList<UnitStatConfig> stats = config.Stats;

            for (int i = 0; i < stats.Count; i++)
            {
                UnitStatConfig stat = stats[i];
                if (stat == null || !IsFiniteNonNegative(stat.BaseValue))
                {
                    errorMessage = $"属性配置包含无效 Stat，索引：{i}。";
                    return false;
                }

                var value = new UnitAttributeValue(
                    EUnitAttributeKind.Stat,
                    stat.BaseValue,
                    false,
                    default);

                if (!attributes.TryAdd(stat.Type, value))
                {
                    errorMessage = $"属性类型重复：{stat.Type}。";
                    return false;
                }
            }

            IReadOnlyList<UnitResourceConfig> resources = config.Resources;
            for (int i = 0; i < resources.Count; i++)
            {
                UnitResourceConfig resource = resources[i];
                if (resource == null || !IsFiniteNonNegative(resource.InitialValue))
                {
                    errorMessage = $"属性配置包含无效 Resource，索引：{i}。";
                    return false;
                }

                if (!attributes.TryGetValue(
                        resource.MaximumAttributeType,
                        out UnitAttributeValue maximum)
                    || maximum.Kind != EUnitAttributeKind.Stat)
                {
                    errorMessage =
                        $"资源 {resource.Type} 缺少有效上限属性：{resource.MaximumAttributeType}。";
                    return false;
                }

                float initialValue = Mathf.Clamp(
                    resource.InitialValue,
                    0f,
                    maximum.CurrentValue);
                var value = new UnitAttributeValue(
                    EUnitAttributeKind.Resource,
                    initialValue,
                    true,
                    resource.MaximumAttributeType);

                if (!attributes.TryAdd(resource.Type, value))
                {
                    errorMessage = $"属性类型重复：{resource.Type}。";
                    return false;
                }
            }

            if (!ValidateRequiredAttributes(attributes, out errorMessage))
                return false;

            attributeComp = new UnitAttributeComp(attributes);
            return true;
        }

        public bool HasAttribute(EUnitAttributeType type)
        {
            return _attributes.ContainsKey(type);
        }

        public bool TryGetBaseValue(EUnitAttributeType type, out float value)
        {
            value = 0f;
            if (!_attributes.TryGetValue(type, out UnitAttributeValue attribute))
                return false;

            value = attribute.BaseValue;
            return true;
        }

        public bool TryGetCurrentValue(EUnitAttributeType type, out float value)
        {
            value = 0f;
            if (!_attributes.TryGetValue(type, out UnitAttributeValue attribute))
                return false;

            value = attribute.CurrentValue;
            return true;
        }

        public bool TrySetBaseValue(EUnitAttributeType type, float value)
        {
            if (!IsFiniteNonNegative(value)
                || !_attributes.TryGetValue(type, out UnitAttributeValue attribute))
            {
                return false;
            }

            float clampedValue = ClampValue(attribute, value, false);
            attribute.SetBaseAndCurrentValue(clampedValue);
            ClampResourcesUsing(type, true);
            return true;
        }

        public bool TrySetCurrentValue(EUnitAttributeType type, float value)
        {
            if (!IsFiniteNonNegative(value)
                || !_attributes.TryGetValue(type, out UnitAttributeValue attribute))
            {
                return false;
            }

            attribute.SetCurrentValue(ClampValue(attribute, value, true));
            ClampResourcesUsing(type, false);
            return true;
        }

        public bool TryResetCurrentValue(EUnitAttributeType type)
        {
            if (!_attributes.TryGetValue(type, out UnitAttributeValue attribute))
                return false;

            attribute.SetCurrentValue(ClampValue(attribute, attribute.BaseValue, true));
            ClampResourcesUsing(type, false);
            return true;
        }

        public bool TryChangeResource(
            EUnitAttributeType type,
            float delta,
            out float actualDelta)
        {
            actualDelta = 0f;
            if (float.IsNaN(delta)
                || float.IsInfinity(delta)
                || !_attributes.TryGetValue(type, out UnitAttributeValue attribute)
                || attribute.Kind != EUnitAttributeKind.Resource)
            {
                return false;
            }

            float nextValue = ClampValue(
                attribute,
                attribute.CurrentValue + delta,
                true);
            actualDelta = nextValue - attribute.CurrentValue;
            attribute.SetCurrentValue(nextValue);
            return true;
        }

        private static bool ValidateRequiredAttributes(
            IReadOnlyDictionary<EUnitAttributeType, UnitAttributeValue> attributes,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!HasKind(attributes, EUnitAttributeType.MaxHealth, EUnitAttributeKind.Stat))
            {
                errorMessage = "单位必须配置 Stat：MaxHealth。";
                return false;
            }

            if (!HasKind(attributes, EUnitAttributeType.MoveSpeed, EUnitAttributeKind.Stat))
            {
                errorMessage = "单位必须配置 Stat：MoveSpeed。";
                return false;
            }

            if (!attributes.TryGetValue(
                    EUnitAttributeType.Health,
                    out UnitAttributeValue health)
                || health.Kind != EUnitAttributeKind.Resource
                || !health.HasMaximum
                || health.MaximumAttributeType != EUnitAttributeType.MaxHealth)
            {
                errorMessage = "单位必须配置上限为 MaxHealth 的 Resource：Health。";
                return false;
            }

            bool hasMana = attributes.ContainsKey(EUnitAttributeType.Mana);
            bool hasMaxMana = attributes.ContainsKey(EUnitAttributeType.MaxMana);
            if (hasMana != hasMaxMana)
            {
                errorMessage = "Mana 与 MaxMana 必须同时配置或同时省略。";
                return false;
            }

            if (!hasMana)
                return true;

            if (!HasKind(attributes, EUnitAttributeType.MaxMana, EUnitAttributeKind.Stat)
                || !attributes.TryGetValue(
                    EUnitAttributeType.Mana,
                    out UnitAttributeValue mana)
                || mana.Kind != EUnitAttributeKind.Resource
                || !mana.HasMaximum
                || mana.MaximumAttributeType != EUnitAttributeType.MaxMana)
            {
                errorMessage = "Mana 必须是上限为 MaxMana 的 Resource。";
                return false;
            }

            return true;
        }

        private static bool HasKind(
            IReadOnlyDictionary<EUnitAttributeType, UnitAttributeValue> attributes,
            EUnitAttributeType type,
            EUnitAttributeKind kind)
        {
            return attributes.TryGetValue(type, out UnitAttributeValue attribute)
                   && attribute.Kind == kind;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private float ClampValue(
            UnitAttributeValue attribute,
            float value,
            bool useCurrentMaximum)
        {
            if (!attribute.HasMaximum
                || !_attributes.TryGetValue(
                    attribute.MaximumAttributeType,
                    out UnitAttributeValue maximum))
            {
                return Mathf.Max(0f, value);
            }

            float maximumValue = useCurrentMaximum
                ? maximum.CurrentValue
                : maximum.BaseValue;
            return Mathf.Clamp(value, 0f, maximumValue);
        }

        private void ClampResourcesUsing(
            EUnitAttributeType maximumAttributeType,
            bool clampBaseValue)
        {
            foreach (UnitAttributeValue attribute in _attributes.Values)
            {
                if (!attribute.HasMaximum
                    || attribute.MaximumAttributeType != maximumAttributeType)
                {
                    continue;
                }

                float currentValue = ClampValue(attribute, attribute.CurrentValue, true);
                if (!clampBaseValue)
                {
                    attribute.SetCurrentValue(currentValue);
                    continue;
                }

                float baseValue = ClampValue(attribute, attribute.BaseValue, false);
                attribute.SetBaseValue(baseValue);
                attribute.SetCurrentValue(currentValue);
            }
        }
    }
}
