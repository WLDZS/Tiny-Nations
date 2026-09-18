using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Units
{
    [CreateAssetMenu(
        fileName = "UnitAttributeSet",
        menuName = "Game/Units/Unit Attribute Set")]
    public sealed class UnitAttributeSetConfig : ScriptableObject
    {
        [SerializeField]
        private UnitStatConfig[] _stats = Array.Empty<UnitStatConfig>();

        [SerializeField]
        private UnitResourceConfig[] _resources = Array.Empty<UnitResourceConfig>();

        public IReadOnlyList<UnitStatConfig> Stats => _stats;

        public IReadOnlyList<UnitResourceConfig> Resources => _resources;
    }
}
