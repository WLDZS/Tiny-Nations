using BorFramework;

namespace GameLogic.Units.Common
{
    internal sealed class UnitTeamComp : Comp
    {
        public int TeamId { get; }

        public UnitTeamComp(int teamId)
        {
            TeamId = teamId;
        }
    }
}
