using BorFramework;
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

        public bool IsDead => GetComp<UnitLifeComp>()?.IsDead == true;

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
            bool usePlayerInput)
        {
            Go = gameObject;

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

            var life = new UnitLifeComp
            {
                Entity = this
            };

            var team = new UnitTeamComp(teamId)
            {
                Entity = this
            };

            var skills = new UnitSkillComp
            {
                Entity = this
            };

            var effects = new GameEffectComp(attributes, life, eventModule)
            {
                Entity = this
            };

            var skillRuntimeContext = new SkillRuntimeContext(
                this,
                gameObject.transform,
                view,
                unitQuery,
                relationResolver);

            for (int i = 0; i < definition.Skills.Count; i++)
            {
                SkillConfig config = definition.Skills[i];
                if (config == null)
                    continue;

                if (!skills.TryRegister(
                        config.Slot,
                        config.CreateSkill(skillRuntimeContext)))
                    Debug.LogWarning($"单位技能槽重复，已忽略：{config.Slot}", gameObject);
            }

            AddComp(command);
            AddComp(view);
            AddComp(attributes);
            AddComp(life);
            AddComp(team);
            AddComp(skills);
            AddComp(effects);

            if (physics != null)
                AddComp(physics);

            if (usePlayerInput && inputModule != null)
            {
                AddLogic(new UnitInputLogic(
                    inputModule,
                    view,
                    command,
                    skills,
                    life,
                    MoveActionName,
                    AttackActionName,
                    GuardActionName));
            }
            AddLogic(new UnitMovementLogic(
                view,
                command,
                attributes,
                skills,
                life,
                physics));
            AddLogic(new SkillLogic(skills, life));
            AddLogic(new UnitGameEffectLogic(effects));
            AddLogic(new UnitDamageFlashLogic(view));
            AddLogic(new UnitAnimationLogic(
                view,
                command,
                skills,
                definition.IdleAnimationStateName,
                definition.MoveAnimationStateName));
        }

        public bool HasAttribute(EUnitAttributeType type)
        {
            return GetComp<UnitAttributeComp>()?.HasAttribute(type) == true;
        }

        public bool TryGetTeamId(out int teamId)
        {
            UnitTeamComp team = GetComp<UnitTeamComp>();
            if (team != null)
            {
                teamId = team.TeamId;
                return true;
            }

            teamId = 0;
            return false;
        }

        public bool TryGetAttributeBaseValue(
            EUnitAttributeType type,
            out float value)
        {
            UnitAttributeComp attributes = GetComp<UnitAttributeComp>();
            if (attributes != null)
                return attributes.TryGetBaseValue(type, out value);

            value = 0f;
            return false;
        }

        public bool TryGetAttributeCurrentValue(
            EUnitAttributeType type,
            out float value)
        {
            UnitAttributeComp attributes = GetComp<UnitAttributeComp>();
            if (attributes != null)
                return attributes.TryGetCurrentValue(type, out value);

            value = 0f;
            return false;
        }

        public bool TryChangeResource(
            EUnitAttributeType type,
            float delta,
            out float actualDelta)
        {
            UnitAttributeComp attributes = GetComp<UnitAttributeComp>();
            if (attributes != null)
                return attributes.TryChangeResource(type, delta, out actualDelta);

            actualDelta = 0f;
            return false;
        }

        public bool TryApplyGameEffect(
            GameEffectConfig config,
            UnitEntity source)
        {
            return GetComp<GameEffectComp>()?.TryApply(config, source) == true;
        }

        internal void PlayDamageFlash()
        {
            GetLogic<UnitDamageFlashLogic>()?.Play();
        }
    }
}
