using BorFramework;
using GameLogic.Units.Skills;

namespace GameLogic.Units.Common
{
    internal sealed class UnitInputLogic : Logic
    {
        private readonly IInputModule _inputModule;
        private readonly UnitCommandComp _command;
        private readonly UnitSkillComp _skills;
        private readonly UnitLifeComp _life;
        private readonly string _moveActionName;
        private readonly string _primarySkillActionName;
        private readonly string _secondarySkillActionName;

        public override ELogicPhase Phase => ELogicPhase.Input;

        public UnitInputLogic(
            IInputModule inputModule,
            UnitCommandComp command,
            UnitSkillComp skills,
            UnitLifeComp life,
            string moveActionName,
            string primarySkillActionName,
            string secondarySkillActionName)
        {
            _inputModule = inputModule;
            _command = command;
            _skills = skills;
            _life = life;
            _moveActionName = moveActionName;
            _primarySkillActionName = primarySkillActionName;
            _secondarySkillActionName = secondarySkillActionName;
        }

        protected override void OnTick(float dt)
        {
            if (_life.IsDead)
            {
                _command.Clear();
                _skills.CancelActiveSkill();
                return;
            }

            _command.SetMoveDirection(_inputModule.ReadVector2(_moveActionName));

            if (_inputModule.WasPressedThisFrame(_primarySkillActionName))
                TryTrigger(ESkillSlot.Primary);

            if (_inputModule.IsPressed(_secondarySkillActionName) && _skills.ActiveSkill == null)
                TryTrigger(ESkillSlot.Secondary);

            if (_inputModule.WasReleasedThisFrame(_secondarySkillActionName))
                _skills.Release(ESkillSlot.Secondary);
        }

        protected override void OnStop()
        {
            _command.Clear();
            _skills.CancelActiveSkill();
        }

        private void TryTrigger(ESkillSlot slot)
        {
            _skills.TryTrigger(slot, new SkillContext(null));
        }
    }
}
