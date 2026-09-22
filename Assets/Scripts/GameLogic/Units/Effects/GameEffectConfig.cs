using UnityEngine;

namespace GameLogic.Units.Effects
{
    [CreateAssetMenu(
        fileName = "GameEffect",
        menuName = "Game/Units/Effects/Damage Game Effect")]
    public sealed class GameEffectConfig : ScriptableObject
    {
        [SerializeField]
        private EGameEffectDurationPolicy _durationPolicy;

        [SerializeField]
        [Min(0f)]
        private float _damagePerApplication = 10f;

        [SerializeField]
        [Min(0f)]
        private float _durationSeconds = 3f;

        [SerializeField]
        [Min(0.01f)]
        private float _periodSeconds = 1f;

        public EGameEffectDurationPolicy DurationPolicy => _durationPolicy;

        public float DamagePerApplication => _damagePerApplication;

        public float DurationSeconds => _durationSeconds;

        public float PeriodSeconds => _periodSeconds;

        internal bool IsValid()
        {
            if (!IsFinitePositive(_damagePerApplication))
                return false;

            if (_durationPolicy == EGameEffectDurationPolicy.Instant)
                return true;

            return _durationPolicy == EGameEffectDurationPolicy.Duration
                   && IsFinitePositive(_durationSeconds)
                   && IsFinitePositive(_periodSeconds)
                   && _periodSeconds <= _durationSeconds;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
