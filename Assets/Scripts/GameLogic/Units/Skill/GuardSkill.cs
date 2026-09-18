using UnityEngine;

namespace GameLogic.Units.Skills
{
    internal sealed class GuardSkill : ISkill
    {
        private readonly float _cooldownSeconds;
        private readonly float _minimumDurationSeconds;
        private float _activeTimeElapsedSeconds;
        private float _cooldownTimeRemainingSeconds;
        private bool _stopRequested;

        public int AnimationStateId { get; }

        public bool BlocksMovement => true;

        public bool IsActive { get; private set; }

        public float DamageReductionRatio { get; }

        public GuardSkill(GuardSkillConfig config)
        {
            AnimationStateId = Animator.StringToHash(config.AnimationStateName);
            DamageReductionRatio = config.DamageReductionRatio;
            _cooldownSeconds = config.CooldownSeconds;
            _minimumDurationSeconds = config.MinimumDurationSeconds;
        }

        public bool CanTrigger(in SkillContext context)
        {
            return !IsActive && _cooldownTimeRemainingSeconds <= 0f;
        }

        public bool TryStart(in SkillContext context)
        {
            if (!CanTrigger(context))
                return false;

            IsActive = true;
            _activeTimeElapsedSeconds = 0f;
            _stopRequested = false;
            return true;
        }

        public void Tick(float dt)
        {
            if (_cooldownTimeRemainingSeconds > 0f)
                _cooldownTimeRemainingSeconds = Mathf.Max(0f, _cooldownTimeRemainingSeconds - dt);

            if (!IsActive)
                return;

            _activeTimeElapsedSeconds += dt;
            if (_stopRequested && _activeTimeElapsedSeconds >= _minimumDurationSeconds)
                Finish();
        }

        public void Stop()
        {
            if (!IsActive)
                return;

            _stopRequested = true;
            if (_activeTimeElapsedSeconds < _minimumDurationSeconds)
                return;

            Finish();
        }

        public void Cancel()
        {
            if (!IsActive)
                return;

            Finish();
        }

        private void Finish()
        {
            IsActive = false;
            _stopRequested = false;
            _cooldownTimeRemainingSeconds = _cooldownSeconds;
        }
    }
}
