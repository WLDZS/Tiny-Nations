using System;
using System.Collections.Generic;
using GameLogic.Units.Skills;
using UnityEngine;

namespace GameLogic.Units
{
    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "Game/Units/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        public const string ResourceTag = "UnitDefinition";

        [SerializeField]
        private string _prefabAddress;

        [SerializeField]
        private UnitAttributeSetConfig _attributeSet;

        [SerializeField]
        private string _idleAnimationStateName;

        [SerializeField]
        private string _moveAnimationStateName;

        [SerializeField]
        private SkillConfig[] _skills = Array.Empty<SkillConfig>();

        public string PrefabAddress => _prefabAddress;
        public UnitAttributeSetConfig AttributeSet => _attributeSet;
        public string IdleAnimationStateName => _idleAnimationStateName;
        public string MoveAnimationStateName => _moveAnimationStateName;
        public IReadOnlyList<SkillConfig> Skills => _skills;
    }
}
