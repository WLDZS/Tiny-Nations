using BorFramework;
using GameLogic.Units.Common;
using GameLogic.Units.Skills;
using UnityEngine;

namespace GameLogic.Units
{
    public sealed class UnitEntity : Entity
    {
        private const string MoveActionName = "Move";
        private const string AttackActionName = "Attack";
        private const string GuardActionName = "Crouch";

        private readonly UnitAttributeComp _attributes;

        internal UnitEntity(
            GameObject gameObject,
            Animator animator,
            SpriteRenderer spriteRenderer,
            Rigidbody2D rigidbody,
            Collider2D bodyCollider,
            UnitDefinition definition,
            UnitAttributeComp attributes,
            IInputModule inputModule,
            bool usePlayerInput)
        {
            Go = gameObject;
            _attributes = attributes;

            var command = new UnitCommandComp
            {
                Entity = this
            };

            var view = new UnitViewComp(
                gameObject.transform,
                animator,
                spriteRenderer)
            {
                Entity = this
            };

            UnitPhysicsComp physics = null;
            if (rigidbody != null && bodyCollider != null)
            {
                physics = new UnitPhysicsComp(rigidbody, bodyCollider)
                {
                    Entity = this
                };
            }

            attributes.Entity = this;

            var skills = new SkillComp
            {
                Entity = this
            };

            for (int i = 0; i < definition.Skills.Count; i++)
            {
                SkillConfig config = definition.Skills[i];
                if (config == null)
                    continue;

                if (!skills.TryRegister(config.Slot, config.CreateSkill()))
                    Debug.LogWarning($"单位技能槽重复，已忽略：{config.Slot}", gameObject);
            }

            AddComp(command);
            AddComp(view);
            AddComp(attributes);
            AddComp(skills);

            if (physics != null)
                AddComp(physics);

            if (usePlayerInput && inputModule != null)
            {
                AddLogic(new UnitInputLogic(
                    inputModule,
                    view,
                    command,
                    skills,
                    MoveActionName,
                    AttackActionName,
                    GuardActionName));
            }

            AddLogic(new UnitMovementLogic(
                view,
                command,
                attributes,
                skills,
                physics));
            AddLogic(new SkillLogic(skills));
            AddLogic(new UnitAnimationLogic(
                view,
                command,
                skills,
                definition.IdleAnimationStateName,
                definition.MoveAnimationStateName));
        }

        public bool HasAttribute(EUnitAttributeType type)
        {
            return _attributes.HasAttribute(type);
        }

        public bool TryGetAttributeBaseValue(
            EUnitAttributeType type,
            out float value)
        {
            return _attributes.TryGetBaseValue(type, out value);
        }

        public bool TryGetAttributeCurrentValue(
            EUnitAttributeType type,
            out float value)
        {
            return _attributes.TryGetCurrentValue(type, out value);
        }

        public bool TryChangeResource(
            EUnitAttributeType type,
            float delta,
            out float actualDelta)
        {
            return _attributes.TryChangeResource(type, delta, out actualDelta);
        }
    }
}
