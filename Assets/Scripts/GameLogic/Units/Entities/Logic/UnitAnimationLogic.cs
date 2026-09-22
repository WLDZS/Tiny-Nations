using BorFramework;
using GameLogic.Units.Skills;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitAnimationLogic : Logic
    {
        private const float MoveInputEpsilon = 0.001f;

        private readonly UnitViewComp _view;
        private readonly UnitCommandComp _command;
        private readonly UnitSkillComp _skills;
        private readonly int _idleStateId;
        private readonly int _moveStateId;
        private ISkill _lastSkill;
        private int _lastAnimationVersion;

        public override ELogicPhase Phase => ELogicPhase.Presentation;

        public UnitAnimationLogic(
            UnitViewComp view,
            UnitCommandComp command,
            UnitSkillComp skills,
            string idleStateName,
            string moveStateName)
        {
            _view = view;
            _command = command;
            _skills = skills;
            _idleStateId = Animator.StringToHash(idleStateName);
            _moveStateId = Animator.StringToHash(moveStateName);
        }

        protected override void OnStart()
        {
            _lastSkill = null;
            Play(_idleStateId, true);
        }

        protected override void OnTick(float dt)
        {
            ISkill activeSkill = _skills.ActiveSkill;
            if (activeSkill != null)
            {
                bool restartAnimation = _lastSkill != activeSkill
                                        || _lastAnimationVersion != activeSkill.AnimationVersion;
                _lastSkill = activeSkill;
                _lastAnimationVersion = activeSkill.AnimationVersion;
                Play(activeSkill.AnimationStateId, restartAnimation);
                return;
            }

            _lastSkill = null;
            int stateId = _command.MoveDirection.sqrMagnitude > MoveInputEpsilon
                ? _moveStateId
                : _idleStateId;

            Play(stateId);
        }

        protected override void OnStop()
        {
            _lastSkill = null;
            Play(_idleStateId, true);
        }

        private void Play(int stateId, bool force = false)
        {
            if (_view.Animator == null || !_view.Animator.isActiveAndEnabled)
                return;

            if (!force && _view.CurrentAnimationStateId == stateId)
                return;

            _view.CurrentAnimationStateId = stateId;
            _view.Animator.Play(stateId, 0, 0f);
        }
    }
}
