using BorFramework;
using GameLogic.Units.Common;
using GameLogic.Units.Effects;
using GameLogic.Units.Skills;
using GameLogic.Navigation;
using UnityEngine;

namespace GameLogic.Units
{
    public sealed class UnitEntity : Entity
    {
        private const string MoveActionName = "Move";
        private const string AttackActionName = "Attack";
        private const string GuardActionName = "Crouch";
        private const float MeleeAIVisionRange = 8f;
        private readonly UnitNavigationLogic _navigationLogic;
        private readonly UnitSkillComp _skills;

        internal UnitAttributeComp Attributes { get; }

        internal UnitLifeComp Life { get; }

        internal UnitTeamComp Team { get; }

        internal UnitGameEffectLogic Effects { get; }

        internal UnitDamageFlashLogic DamageFlash { get; }

        internal UnitEntity(
            GameObject gameObject,
            Transform worldPositionTransform,
            Animator animator,
            SpriteRenderer spriteRenderer,
            Rigidbody2D rigidbody,
            Collider2D bodyCollider,
            UnitDefinition definition,
            UnitAttributeComp attributes,
            int teamId,
            IInputModule inputModule,
            IUnitQuery unitQuery,
            INavigationSystem navigation,
            IUnitRelationResolver relationResolver,
            IEventModule eventModule,
            bool usePlayerInput)
        {
            Go = gameObject;

            var command = new UnitCommandComp();
            command.Entity = this;

            var view = new UnitViewComp(
                gameObject.transform,
                worldPositionTransform,
                animator,
                spriteRenderer);
            view.Entity = this;

            UnitPhysicsComp physics = null;
            if (rigidbody != null && bodyCollider != null)
            {
                physics = new UnitPhysicsComp(rigidbody, bodyCollider);
                physics.Entity = this;
            }

            Attributes = attributes;
            Attributes.Entity = this;

            Life = new UnitLifeComp();
            Life.Entity = this;

            Team = new UnitTeamComp(teamId);
            Team.Entity = this;

            var skills = new UnitSkillComp();
            skills.Entity = this;
            _skills = skills;

            var effects = new GameEffectComp();
            effects.Entity = this;

            var skillRuntimeContext = new SkillRuntimeContext(
                this,
                unitQuery,
                relationResolver);

            for (int i = 0; i < definition.Skills.Count; i++)
            {
                SkillConfig config = definition.Skills[i];
                skills.TryRegister(config.Slot, config.CreateSkill(skillRuntimeContext));
            }

            UnitAIComp ai = null;
            MeleeAttackSkill meleeAttackSkill = null;
            if (!usePlayerInput
                && skills.TryGetSkill(ESkillSlot.Primary, out ISkill primarySkill)
                && primarySkill is MeleeAttackSkill configuredMeleeAttackSkill)
            {
                meleeAttackSkill = configuredMeleeAttackSkill;
                ai = new UnitAIComp(MeleeAIVisionRange);
                ai.Entity = this;
            }

            UnitInputLogic inputLogic = null;
            UnitMeleeAILogic meleeAILogic = null;
            UnitNavigationLogic navigationLogic = navigation != null
                ? new UnitNavigationLogic(this, command, unitQuery, navigation)
                : null;
            _navigationLogic = navigationLogic;
            if (usePlayerInput && inputModule != null)
            {
                inputLogic = new UnitInputLogic(
                    inputModule,
                    command,
                    skills,
                    Life,
                    MoveActionName,
                    AttackActionName,
                    GuardActionName);
            }
            else if (ai != null)
            {
                meleeAILogic = new UnitMeleeAILogic(
                    this,
                    ai,
                    view,
                    skills,
                    Life,
                    meleeAttackSkill,
                    unitQuery,
                    navigationLogic);
            }

            var movementLogic = new UnitMovementLogic(
                view,
                command,
                Attributes,
                skills,
                Life,
                physics,
                this,
                navigationLogic != null ? unitQuery : null,
                navigationLogic != null ? navigation : null);
            var skillLogic = new SkillLogic(skills, Life);
            Effects = new UnitGameEffectLogic(this, effects, Attributes, Life, eventModule);
            DamageFlash = new UnitDamageFlashLogic(view);
            var animationLogic = new UnitAnimationLogic(
                view,
                command,
                skills,
                definition.IdleAnimationStateName,
                definition.MoveAnimationStateName);

            if (inputLogic != null)
                AddLogic(inputLogic);

            if (meleeAILogic != null)
                AddLogic(meleeAILogic);

            if (navigationLogic != null)
                AddLogic(navigationLogic);

            AddLogic(movementLogic);
            AddLogic(skillLogic);
            AddLogic(Effects);
            AddLogic(DamageFlash);
            AddLogic(animationLogic);
        }

        internal bool TryMoveTo(Vector2 destination)
        {
            if (Life.IsDead || _navigationLogic == null
                || !_navigationLogic.TrySetMoveDestination(destination))
                return false;

            _skills.CancelActiveSkill();
            return true;
        }
    }
}
