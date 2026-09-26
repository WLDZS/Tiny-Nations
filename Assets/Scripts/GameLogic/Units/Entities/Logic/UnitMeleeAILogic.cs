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
        private readonly UnitSkillComp _skills;
        private readonly UnitLifeComp _life;
        private readonly MeleeAttackSkill _attackSkill;
        private readonly IUnitQuery _unitQuery;
        private readonly UnitNavigationLogic _navigation;
        private float _targetSearchTimeRemainingSeconds;

        public override ELogicPhase Phase => ELogicPhase.Command;

        public UnitMeleeAILogic(
            UnitEntity owner,
            UnitAIComp ai,
            UnitViewComp view,
            UnitSkillComp skills,
            UnitLifeComp life,
            MeleeAttackSkill attackSkill,
            IUnitQuery unitQuery,
            UnitNavigationLogic navigation)
        {
            _owner = owner;
            _ai = ai;
            _view = view;
            _skills = skills;
            _life = life;
            _attackSkill = attackSkill;
            _unitQuery = unitQuery;
            _navigation = navigation;
        }

        protected override void OnTick(float dt)
        {
            if (_life.IsDead
                || _view.WorldPositionTransform == null
                || _unitQuery == null)
            {
                _navigation?.ClearTarget();
                return;
            }

            if (_navigation != null && _navigation.HasMoveDestination)
            {
                _ai.ClearTarget();
                return;
            }

            if (_attackSkill.IsActive)
            {
                _navigation?.PauseForAttack();
                FaceCurrentAttackTarget();
                return;
            }

            UnitEntity attackTarget = _ai.Target;
            bool hasAttackTarget = attackTarget != null
                                   && _attackSkill.IsTargetInRange(new SkillContext(attackTarget));
            if (!hasAttackTarget)
                hasAttackTarget = _attackSkill.TryFindTargetInRange(out attackTarget);

            if (hasAttackTarget
                && _unitQuery.TryGetUnitWorldPosition(attackTarget, out Vector3 attackTargetPosition))
            {
                _ai.SetTarget(attackTarget);
                _navigation?.SetTarget(attackTarget, _attackSkill.AttackRange);
                FaceTarget(attackTargetPosition.x - _view.WorldPositionTransform.position.x);
                if (_navigation == null || _navigation.IsReadyToAttack(attackTarget))
                {
                    _navigation?.PauseForAttack();
                    _skills.TryTrigger(ESkillSlot.Primary, new SkillContext(attackTarget));
                }
                return;
            }

            if (!TryResolveTarget(dt, out Vector3 targetPosition))
            {
                _navigation?.ClearTarget();
                return;
            }

            Vector2 targetOffset = targetPosition - _view.WorldPositionTransform.position;
            FaceTarget(targetOffset.x);
            _navigation?.SetTarget(_ai.Target, _attackSkill.AttackRange);
        }

        private void FaceCurrentAttackTarget()
        {
            UnitEntity target = _attackSkill.AttackTarget;
            if (target != null
                && _unitQuery.TryGetUnitWorldPosition(target, out Vector3 targetPosition))
            {
                FaceTarget(targetPosition.x - _view.WorldPositionTransform.position.x);
            }
        }

        protected override void OnStop()
        {
            ClearState();
        }

        private bool TryResolveTarget(float dt, out Vector3 targetPosition)
        {
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
            return IsTargetValid(out targetPosition);
        }

        private bool IsTargetValid(out Vector3 targetPosition)
        {
            targetPosition = default;
            UnitEntity target = _ai.Target;
            return target != null
                   && !target.Life.IsDead
                   && _unitQuery.TryGetUnitWorldPosition(target, out targetPosition)
                   && (targetPosition - _view.WorldPositionTransform.position).sqrMagnitude
                   <= _ai.VisionRange * _ai.VisionRange;
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
            _navigation?.ClearTarget();
            _ai.ClearTarget();
            _targetSearchTimeRemainingSeconds = 0f;
        }
    }
}
