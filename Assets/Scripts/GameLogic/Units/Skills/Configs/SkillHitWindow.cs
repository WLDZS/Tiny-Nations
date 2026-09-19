using System;
using UnityEngine;

namespace GameLogic.Units.Skills
{
    [Serializable]
    public sealed class SkillHitWindow
    {
        [SerializeField]
        [Min(0f)]
        private float _startTimeSeconds;

        [SerializeField]
        [Min(0f)]
        private float _durationSeconds = 0.2f;

        public float StartTimeSeconds => _startTimeSeconds;

        public float DurationSeconds => _durationSeconds;
    }
}
