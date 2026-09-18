using BorFramework;
using GameLogic.Units.Skills;

namespace GameLogic.Units.Common
{
    internal sealed class UnitInputLogic : Logic
    {
        private readonly IInputModule _inputModule;
        private readonly UnitViewComp _view;
        private readonly UnitCommandComp _command;
        private readonly SkillComp _skills;
        private readonly string _moveActionName;
        private readonly string _primarySkillActionName;
        private readonly string _secondarySkillActionName;

        public override ELogicPhase Phase => ELogicPhase.Input;

        public UnitInputLogic(
            IInputModule inputModule,
            UnitViewComp view,
            UnitCommandComp command,
            SkillComp skills,
            string moveActionName,
            string primarySkillActionName,
            string secondarySkillActionName)
        {
            _inputModule = inputModule;
            _view = view;
            _command = command;
            _skills = skills;
            _moveActionName = moveActionName;
            _primarySkillActionName = primarySkillActionName;
            _secondarySkillActionName = secondarySkillActionName;
        }

        protected override void OnTick(float dt)
        {
            _command.SetMoveDirection(_inputModule.ReadVector2(_moveActionName));

            if (_inputModule.WasPressedThisFrame(_primarySkillActionName))
                TryTrigger(ESkillSlot.Primary);

            if (_inputModule.IsPressed(_secondarySkillActionName) && _skills.ActiveSkill == null)
                TryTrigger(ESkillSlot.Secondary);

            if (_inputModule.WasReleasedThisFrame(_secondarySkillActionName))
                _skills.Release(ESkillSlot.Secondary);
        }

        public override void OnStop()
        {
            _command.Clear();
            _skills.CancelActiveSkill();
        }

        private void TryTrigger(ESkillSlot slot)
        {
            var context = new SkillContext(
                _skills.Entity,
                _view.Transform.position,
                null);

            _skills.TryTrigger(slot, context);
        }
    }
}
