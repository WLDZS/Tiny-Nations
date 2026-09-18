namespace GameLogic.Units.Skills
{
    internal interface ISkill
    {
        int AnimationStateId { get; }

        bool BlocksMovement { get; }

        bool IsActive { get; }

        bool CanTrigger(in SkillContext context);

        bool TryStart(in SkillContext context);

        void Tick(float dt);

        void Stop();

        void Cancel();
    }
}
