using BorFramework;
using GameLogic.Navigation;
using GameLogic.Units.Common;
using GameLogic.Units.Effects;
using GameLogic.Units.Skills;
using UnityEngine;

namespace GameLogic.Units
{
    public sealed class UnitEntity : Entity
    {
        private const string MoveActionName = "Move";
        private const string AttackActionName = "Attack";
        private const string GuardActionName = "Crouch";
        private const float MeleeAIVisionRange = 8f;

        internal UnitAttributeComp Attributes { get; }

        internal UnitLifeComp Life { get; }

        internal UnitTeamComp Team { get; }

        internal UnitGameEffectLogic Effects { get; }

        internal UnitDamageFlashLogic DamageFlash { get; }

        internal UnitEntity(
            GameObject gameObject,
            Animator animator,
            SpriteRenderer spriteRenderer,
            Rigidbody2D rigidbody,
            Collider2D bodyCollider,
            UnitDefinition definition,
            UnitAttributeComp attributes,
            int teamId,
            IInputModule inputModule,
            IUnitQuery unitQuery,
            IUnitRelationResolver relationResolver,
            IEventModule eventModule,
            INavigationSystem navigationSystem,
            bool usePlayerInput)
        {
            Go = gameObject;

            var command = new UnitCommandComp();
            command.Entity = this;

            var view = new UnitViewComp(
                gameObject.transform,
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

            var effects = new GameEffectComp();
            effects.Entity = this;

            var skillRuntimeContext = new SkillRuntimeContext(
                this,
                gameObject.transform,
                view,
                unitQuery,
                relationResolver);

            for (int i = 0; i < definition.Skills.Count; i++)
            {
                SkillConfig config = definition.Skills[i];
                skills.TryRegister(config.Slot, config.CreateSkill(skillRuntimeContext));
            }

            UnitAIComp ai = null;
            UnitNavigationComp navigation = null;
            MeleeAttackSkill meleeAttackSkill = null;
            Vector2 navigationAnchorOffset = Vector2.zero;
            float navigationClearanceRadius = 0f;
            if (!usePlayerInput
                && skills.TryGetSkill(ESkillSlot.Primary, out ISkill primarySkill)
                && primarySkill is MeleeAttackSkill configuredMeleeAttackSkill)
            {
                bool hasSupportedNavigationGeometry = physics == null
                                                      || physics.TryGetNavigationGeometry(
                                                          out navigationAnchorOffset,
                                                          out navigationClearanceRadius);
                if (!hasSupportedNavigationGeometry)
                {
                    Debug.LogError(
                        $"AI单位的身体碰撞体不支持导航净空：{bodyCollider.GetType().Name}。",
                        gameObject);
                }
                else
                {
                    meleeAttackSkill = configuredMeleeAttackSkill;
                    ai = new UnitAIComp(MeleeAIVisionRange);
                    ai.Entity = this;
                    navigation = new UnitNavigationComp();
                    navigation.Entity = this;
                }
            }

            UnitInputLogic inputLogic = null;
            UnitMeleeAILogic meleeAILogic = null;
            UnitNavigationLogic navigationLogic = null;
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
                    navigation,
                    skills,
                    Life,
                    meleeAttackSkill,
                    unitQuery);
                navigationLogic = new UnitNavigationLogic(
                    view,
                    command,
                    navigation,
                    skills,
                    Life,
                    navigationSystem,
                    navigationAnchorOffset,
                    navigationClearanceRadius);
            }

            var movementLogic = new UnitMovementLogic(
                view,
                command,
                Attributes,
                skills,
                Life,
                physics);
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
    }
}
