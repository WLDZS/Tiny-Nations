using System;
using UnityEngine;

namespace GameLogic.Units.Skills
{
    [Serializable]
    internal sealed class SkillAnimationStage
    {
        [SerializeField]
        private string _stateName;

        [SerializeField]
        [Min(0.01f)]
        private float _durationSeconds = 0.1f;

        public string StateName => _stateName;

        public float DurationSeconds => _durationSeconds;
    }
}
