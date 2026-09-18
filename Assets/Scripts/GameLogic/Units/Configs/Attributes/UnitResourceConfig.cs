using System;
using UnityEngine;

namespace GameLogic.Units
{
    [Serializable]
    public sealed class UnitResourceConfig
    {
        [SerializeField]
        private EUnitAttributeType _type;

        [SerializeField]
        [Min(0f)]
        private float _initialValue;

        [SerializeField]
        private EUnitAttributeType _maximumAttributeType;

        public EUnitAttributeType Type => _type;

        public float InitialValue => _initialValue;

        public EUnitAttributeType MaximumAttributeType => _maximumAttributeType;
    }
}
