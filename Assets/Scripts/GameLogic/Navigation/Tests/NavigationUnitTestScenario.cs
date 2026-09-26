using UnityEngine;

namespace GameLogic.Navigation
{
    internal readonly struct NavigationUnitTestScenario
    {
        public string Name { get; }

        public string MoverDefinitionAddress { get; }

        public Vector3 MoverStart { get; }

        public Vector3 TargetPosition { get; }

        public Vector3? BystanderPosition { get; }

        public float MinimumTravelDistance { get; }

        public float TimeoutSeconds { get; }

        public NavigationUnitTestScenario(
            string name,
            string moverDefinitionAddress,
            Vector3 moverStart,
            Vector3 targetPosition,
            float minimumTravelDistance,
            float timeoutSeconds,
            Vector3? bystanderPosition = null)
        {
            Name = name;
            MoverDefinitionAddress = moverDefinitionAddress;
            MoverStart = moverStart;
            TargetPosition = targetPosition;
            BystanderPosition = bystanderPosition;
            MinimumTravelDistance = minimumTravelDistance;
            TimeoutSeconds = timeoutSeconds;
        }
    }
}
