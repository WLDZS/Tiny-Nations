using BorFramework;
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
        private readonly SkillComp _skills;
        private readonly UnitPhysicsComp _physics;

        public override ELogicPhase Phase => ELogicPhase.Movement;

        public UnitMovementLogic(
            UnitViewComp view,
            UnitCommandComp command,
            UnitAttributeComp attributes,
            SkillComp skills,
            UnitPhysicsComp physics)
        {
            _view = view;
            _command = command;
            _attributes = attributes;
            _skills = skills;
            _physics = physics;
        }

        protected override void OnTick(float dt)
        {
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
            if (_physics != null)
                _physics.Rigidbody.linearVelocity = direction * moveSpeed;
            else
                _view.Transform.position += (Vector3)(direction * (moveSpeed * dt));

            if (direction.x > DirectionEpsilon)
                _view.SpriteRenderer.flipX = false;
            else if (direction.x < -DirectionEpsilon)
                _view.SpriteRenderer.flipX = true;
        }

        public override void OnStop()
        {
            StopPhysicsMovement();
        }

        public override void Dispose()
        {
            StopPhysicsMovement();
        }

        private void StopPhysicsMovement()
        {
            if (_physics != null)
                _physics.Rigidbody.linearVelocity = Vector2.zero;
        }
    }
}
