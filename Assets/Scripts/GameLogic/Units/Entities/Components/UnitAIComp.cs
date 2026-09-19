using BorFramework;

namespace GameLogic.Units.Common
{
    internal sealed class UnitAIComp : Comp
    {
        public float VisionRange { get; }

        public UnitEntity Target { get; private set; }

        public UnitAIComp(float visionRange)
        {
            VisionRange = visionRange;
        }

        public void SetTarget(UnitEntity target)
        {
            Target = target;
        }

        public void ClearTarget()
        {
            Target = null;
        }
    }
}
