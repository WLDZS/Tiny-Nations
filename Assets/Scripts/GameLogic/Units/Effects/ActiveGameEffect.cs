using UnityEngine;

namespace GameLogic.Units.Effects
{
    internal sealed class ActiveGameEffect
    {
        private float _elapsedSeconds;
        private float _nextApplicationTimeSeconds;

        public GameEffectConfig Config { get; }

        public UnitEntity Source { get; }

        public bool IsComplete => _elapsedSeconds >= Config.DurationSeconds;

        public ActiveGameEffect(GameEffectConfig config, UnitEntity source)
        {
            Config = config;
            Source = source;
            _nextApplicationTimeSeconds = config.PeriodSeconds;
        }

        public int Advance(float dt)
        {
            _elapsedSeconds = Mathf.Min(
                Config.DurationSeconds,
                _elapsedSeconds + Mathf.Max(0f, dt));

            int applicationCount = 0;
            while (_nextApplicationTimeSeconds <= _elapsedSeconds)
            {
                applicationCount++;
                _nextApplicationTimeSeconds += Config.PeriodSeconds;
            }

            return applicationCount;
        }
    }
}
