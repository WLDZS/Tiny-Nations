using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Units.Skills
{
    internal sealed class MeleeAttackSkill : ISkill
    {
        private readonly MeleeAttackSkillConfig _config;
        private readonly int[] _animationStateIds;
        private readonly float[] _stageEndTimesSeconds;
        private readonly float _durationSeconds;
        private float _activeTimeRemainingSeconds;
        private float _activeTimeElapsedSeconds;
        private float _cooldownTimeRemainingSeconds;
        private int _animationStageIndex;

        public int AnimationStateId { get; private set; }

        public bool BlocksMovement => true;

        public bool IsActive => _activeTimeRemainingSeconds > 0f;

        public MeleeAttackSkill(MeleeAttackSkillConfig config)
        {
            _config = config;
            IReadOnlyList<SkillAnimationStage> stages = config.AnimationStages;
            var animationStateIds = new List<int>(stages.Count);
            var stageEndTimesSeconds = new List<float>(stages.Count);
            float durationSeconds = 0f;

            for (int i = 0; i < stages.Count; i++)
            {
                SkillAnimationStage stage = stages[i];
                if (stage == null
                    || string.IsNullOrWhiteSpace(stage.StateName)
                    || stage.DurationSeconds <= 0f)
                {
                    continue;
                }

                durationSeconds += stage.DurationSeconds;
                animationStateIds.Add(Animator.StringToHash(stage.StateName));
                stageEndTimesSeconds.Add(durationSeconds);
            }

            _animationStateIds = animationStateIds.ToArray();
            _stageEndTimesSeconds = stageEndTimesSeconds.ToArray();
            _durationSeconds = durationSeconds;
            AnimationStateId = _animationStateIds.Length > 0 ? _animationStateIds[0] : 0;
        }

        public bool CanTrigger(in SkillContext context)
        {
            if (_animationStateIds.Length == 0 || IsActive || _cooldownTimeRemainingSeconds > 0f)
                return false;

            if (!context.HasTarget)
                return true;

            float distance = Vector2.Distance(context.Origin, context.Target.position);
            return distance <= _config.TriggerRange;
        }

        public bool TryStart(in SkillContext context)
        {
            if (!CanTrigger(context))
                return false;

            _activeTimeRemainingSeconds = _durationSeconds;
            _activeTimeElapsedSeconds = 0f;
            _cooldownTimeRemainingSeconds = _config.CooldownSeconds;
            _animationStageIndex = 0;
            AnimationStateId = _animationStateIds[0];
            return true;
        }

        public void Tick(float dt)
        {
            if (_activeTimeRemainingSeconds > 0f)
            {
                _activeTimeRemainingSeconds = Mathf.Max(0f, _activeTimeRemainingSeconds - dt);
                _activeTimeElapsedSeconds += dt;

                while (_animationStageIndex + 1 < _animationStateIds.Length
                       && _activeTimeElapsedSeconds >= _stageEndTimesSeconds[_animationStageIndex])
                {
                    _animationStageIndex++;
                    AnimationStateId = _animationStateIds[_animationStageIndex];
                }
            }

            if (_cooldownTimeRemainingSeconds > 0f)
                _cooldownTimeRemainingSeconds = Mathf.Max(0f, _cooldownTimeRemainingSeconds - dt);
        }

        public void Stop()
        {
            _activeTimeRemainingSeconds = 0f;
        }

        public void Cancel()
        {
            Stop();
        }
    }
}
