using BorFramework;

namespace GameLogic.Economy
{
    /// <summary>Stores each player's resource values for the active match.</summary>
    public interface IPlayerResourceSystem : IGameSystem
    {
        /// <summary>Adds a player with nonnegative values; fails for an existing ID.</summary>
        bool TryAddPlayer(int playerId, PlayerResources resources);

        /// <summary>Reads one player's current values.</summary>
        bool TryGetResources(int playerId, out PlayerResources resources);

        /// <summary>Sets a nonnegative gold value for an existing player.</summary>
        bool TrySetGold(int playerId, int gold);

        /// <summary>Sets a nonnegative wood value for an existing player.</summary>
        bool TrySetWood(int playerId, int wood);

        /// <summary>Sets nonnegative occupied population for an existing player.</summary>
        bool TrySetMeatCurrent(int playerId, int meatCurrent);

        /// <summary>Sets nonnegative capacity; occupancy is retained if it exceeds the new capacity.</summary>
        bool TrySetMeatMaximum(int playerId, int meatMaximum);
    }
}
