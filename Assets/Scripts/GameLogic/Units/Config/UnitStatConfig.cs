using System;
using UnityEngine;

namespace GameLogic.Units
{
    [Serializable]
    public sealed class UnitStatConfig
    {
        [SerializeField]
        private EUnitAttributeType _type;

        [SerializeField]
        [Min(0f)]
        private float _baseValue;

        public EUnitAttributeType Type => _type;

        public float BaseValue => _baseValue;
    }
}
