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
                || _view.Transform == null
                || _unitQuery == null
                || !TryResolveTarget(
                    dt,
                    out Transform targetTransform,
                    out bool targetChanged))
            {
                _navigation.ClearDestination();
                return;
            }

            Vector2 targetOffset = targetTransform.position - _view.Transform.position;
            FaceTarget(targetOffset.x);
            var context = new SkillContext(
                _owner,
                _view.Transform.position,
                _ai.Target);

            if (_attackSkill.IsTargetInRange(context))
            {
                _navigation.ClearDestination();
                _skills.TryTrigger(ESkillSlot.Primary, context);
                return;
            }

            if (targetChanged)
                _navigation.BeginDestination(targetTransform.position);
            else
                _navigation.UpdateDestination(targetTransform.position);
        }

        public override void OnStop()
        {
            ClearState();
        }

        public override void Dispose()
        {
            ClearState();
        }

        private bool TryResolveTarget(
            float dt,
            out Transform targetTransform,
            out bool targetChanged)
        {
            targetChanged = false;
            if (IsTargetValid(out targetTransform))
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
                    _view.Transform.position,
                    _ai.VisionRange,
                    out UnitEntity target))
            {
                return false;
            }

            _ai.SetTarget(target);
            targetChanged = true;
            return IsTargetValid(out targetTransform);
        }

        private bool IsTargetValid(out Transform targetTransform)
        {
            targetTransform = null;
            UnitEntity target = _ai.Target;
            return target != null
                   && !target.Life.IsDead
                   && _unitQuery.TryGetUnitTransform(target, out targetTransform);
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
            _ai.ClearTarget();
            _navigation.ClearDestination();
            _targetSearchTimeRemainingSeconds = 0f;
        }
    }
}
