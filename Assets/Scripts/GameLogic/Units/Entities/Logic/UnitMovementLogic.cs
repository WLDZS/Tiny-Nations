using BorFramework;
using GameLogic.Navigation;
using GameLogic.Units.Skills;
using UnityEngine;

namespace GameLogic.Units.Common
{
    internal sealed class UnitMovementLogic : Logic
    {
        private const float DirectionEpsilon = 0.001f;

        private readonly UnitViewComp _view;
        private readonly UnitCommandComp _command;
        private readonly UnitAttributeComp _attributes;
        private readonly UnitSkillComp _skills;
        private readonly UnitLifeComp _life;
        private readonly UnitPhysicsComp _physics;
        private readonly UnitEntity _owner;
        private readonly IUnitQuery _unitQuery;
        private readonly INavigationSystem _navigation;

        public override ELogicPhase Phase => ELogicPhase.Movement;

        public UnitMovementLogic(
            UnitViewComp view,
            UnitCommandComp command,
            UnitAttributeComp attributes,
            UnitSkillComp skills,
            UnitLifeComp life,
            UnitPhysicsComp physics,
            UnitEntity owner,
            IUnitQuery unitQuery,
            INavigationSystem navigation)
        {
            _view = view;
            _command = command;
            _attributes = attributes;
            _skills = skills;
            _life = life;
            _physics = physics;
            _owner = owner;
            _unitQuery = unitQuery;
            _navigation = navigation;
        }

        protected override void OnTick(float dt)
        {
            if (_life.IsDead)
            {
                StopPhysicsMovement();
                return;
            }

            if (_skills.ActiveSkill != null && _skills.ActiveSkill.BlocksMovement)
            {
                StopPhysicsMovement();
                return;
            }

            if (!_attributes.TryGetCurrentValue(
                    EUnitAttributeType.MoveSpeed,
                    out float moveSpeed))
            {
                StopPhysicsMovement();
                return;
            }

            Vector2 direction = _command.MoveDirection;
            if (direction.sqrMagnitude > DirectionEpsilon * DirectionEpsilon
                && _unitQuery != null && _navigation?.Map != null
                && _unitQuery.TryGetUnitAttackFootprint(
                    _owner, out Vector2 center, out float radius))
            {
                Vector2 separation = _unitQuery.GetLocalSeparation(_owner);
                if (separation.sqrMagnitude > 0f)
                {
                    Vector2 adjusted = Vector2.ClampMagnitude(
                        direction + separation * (1.2f * direction.magnitude), 1f);

                    float step = moveSpeed * Mathf.Max(dt, Time.fixedDeltaTime);
                    if (_navigation.Map.CanTraverseSegment(
                            center, center + adjusted * step, radius))
                    {
                        direction = adjusted;
                    }
                }
            }

            if (_physics?.Rigidbody != null)
                _physics.Rigidbody.linearVelocity = direction * moveSpeed;
            else if (_view.Transform != null)
                _view.Transform.position += (Vector3)(direction * (moveSpeed * dt));

            if (_view.SpriteRenderer == null)
                return;

            if (direction.x > DirectionEpsilon)
                _view.SpriteRenderer.flipX = false;
            else if (direction.x < -DirectionEpsilon)
                _view.SpriteRenderer.flipX = true;
        }

        protected override void OnStop()
        {
            StopPhysicsMovement();
        }

        private void StopPhysicsMovement()
        {
            if (_physics?.Rigidbody != null)
                _physics.Rigidbody.linearVelocity = Vector2.zero;
        }
    }
}
