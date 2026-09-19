using UnityEngine;

namespace GameLogic.Units
{
    public readonly struct UnitSpawnRequest
    {
        public string DefinitionAddress { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public int TeamId { get; }

        public bool UsePlayerInput { get; }

        public UnitSpawnRequest(
            string definitionAddress,
            Vector3 position,
            Quaternion rotation,
            int teamId,
            bool usePlayerInput)
        {
            DefinitionAddress = definitionAddress;
            Position = position;
            Rotation = rotation;
            TeamId = teamId;
            UsePlayerInput = usePlayerInput;
        }
    }
}
