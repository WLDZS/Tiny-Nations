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

        public override void OnStart()
        {
            Play(_idleStateId, true);
        }

        protected override void OnTick(float dt)
        {
            if (_skills.ActiveSkill != null)
            {
                Play(_skills.ActiveSkill.AnimationStateId);
                return;
            }

            int stateId = _command.MoveDirection.sqrMagnitude > MoveInputEpsilon
                ? _moveStateId
                : _idleStateId;

            Play(stateId);
        }

        public override void OnStop()
        {
            Play(_idleStateId, true);
        }

        private void Play(int stateId, bool force = false)
        {
            if (!force && _view.CurrentAnimationStateId == stateId)
                return;

            _view.CurrentAnimationStateId = stateId;
            _view.Animator.Play(stateId, 0, 0f);
        }
    }
}
