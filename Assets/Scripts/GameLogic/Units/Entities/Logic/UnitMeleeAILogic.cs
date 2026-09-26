using BorFramework;
using GameLogic.Units.Skills;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitMeleeAILogic : Logic
    {
        private const float TargetSearchIntervalSeconds = 0.25f;
        private const float HorizontalDirectionEpsilon = 0.001f;

        private readonly UnitEntity _owner;
        private readonly UnitAIComp _ai;
        private readonly UnitViewComp _view;
        private readonly UnitNavigationComp _navigation;
        private readonly UnitSkillComp _skills;
        private readonly UnitLifeComp _life;
        private readonly MeleeAttackSkill _attackSkill;
        private readonly IUnitQuery _unitQuery;
        private float _targetSearchTimeRemainingSeconds;

        public override ELogicPhase Phase => ELogicPhase.Command;

        public UnitMeleeAILogic(
            UnitEntity owner,
            UnitAIComp ai,
            UnitViewComp view,
            UnitNavigationComp navigation,
            UnitSkillComp skills,
            UnitLifeComp life,
            MeleeAttackSkill attackSkill,
            IUnitQuery unitQuery)
        {
            _owner = owner;
            _ai = ai;
            _view = view;
            _navigation = navigation;
            _skills = skills;
            _life = life;
            _attackSkill = attackSkill;
            _unitQuery = unitQuery;
        }

        protected override void OnTick(float dt)
        {
            if (_life.IsDead
                || _view.WorldPositionTransform == null
                || _unitQuery == null
                || !TryResolveTarget(
                    dt,
                    out Vector3 targetPosition,
                    out bool targetChanged))
            {
                _unitQuery?.ReleaseApproachPosition(_owner);
                _navigation.ClearDestination();
                return;
            }

            Vector2 targetOffset = targetPosition - _view.WorldPositionTransform.position;
            FaceTarget(targetOffset.x);
            var context = new SkillContext(_ai.Target);

            if (_attackSkill.IsTargetInRange(context))
            {
                _navigation.ClearDestination();
                _skills.TryTrigger(ESkillSlot.Primary, context);
                return;
            }

            if (!_unitQuery.TryGetApproachPosition(
                    _owner, _ai.Target, out Vector3 destination))
            {
                _navigation.ClearDestination();
                return;
            }

            if (targetChanged)
                _navigation.BeginDestination(destination);
            else
                _navigation.UpdateDestination(destination);
        }

        protected override void OnStop()
        {
            ClearState();
        }

        private bool TryResolveTarget(
            float dt,
            out Vector3 targetPosition,
            out bool targetChanged)
        {
            targetChanged = false;
            if (IsTargetValid(out targetPosition))
            {
                _targetSearchTimeRemainingSeconds = 0f;
                return true;
            }

            _ai.ClearTarget();
            _targetSearchTimeRemainingSeconds -= Mathf.Max(0f, dt);
            if (_targetSearchTimeRemainingSeconds > 0f)
                return false;

            _targetSearchTimeRemainingSeconds = TargetSearchIntervalSeconds;
            if (!_unitQuery.TryFindClosestEnemy(
                    _owner,
                    _view.WorldPositionTransform.position,
                    _ai.VisionRange,
                    out UnitEntity target))
            {
                return false;
            }

            _ai.SetTarget(target);
            targetChanged = true;
            return IsTargetValid(out targetPosition);
        }

        private bool IsTargetValid(out Vector3 targetPosition)
        {
            targetPosition = default;
            UnitEntity target = _ai.Target;
            return target != null
                   && !target.Life.IsDead
                   && _unitQuery.TryGetUnitWorldPosition(target, out targetPosition);
        }

        private void FaceTarget(float horizontalDirection)
        {
            if (_view.SpriteRenderer == null)
                return;

            if (horizontalDirection > HorizontalDirectionEpsilon)
                _view.SpriteRenderer.flipX = false;
            else if (horizontalDirection < -HorizontalDirectionEpsilon)
                _view.SpriteRenderer.flipX = true;
        }

        private void ClearState()
        {
            _unitQuery?.ReleaseApproachPosition(_owner);
            _ai.ClearTarget();
            _navigation.ClearDestination();
            _targetSearchTimeRemainingSeconds = 0f;
        }
    }
}
