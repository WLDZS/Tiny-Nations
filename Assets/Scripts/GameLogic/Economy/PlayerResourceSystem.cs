using System.Collections.Generic;

namespace GameLogic.Economy
{
    public sealed class PlayerResourceSystem : IPlayerResourceSystem
    {
        private readonly Dictionary<int, PlayerResources> _resourcesByPlayer = new();
        private bool _started;

        public void Init()
        {
        }

        public void Start()
        {
            _started = true;
        }

        public void Stop()
        {
            _started = false;
            _resourcesByPlayer.Clear();
        }

        public void Dispose()
        {
            Stop();
        }

        public bool TryAddPlayer(int playerId, PlayerResources resources)
        {
            if (!_started || playerId < 0 || !AreValid(resources))
                return false;

            return _resourcesByPlayer.TryAdd(playerId, resources);
        }

        public bool TryGetResources(int playerId, out PlayerResources resources)
        {
            resources = default;
            return _started && _resourcesByPlayer.TryGetValue(playerId, out resources);
        }

        public bool TrySetGold(int playerId, int gold)
        {
            if (gold < 0 || !TryGetResources(playerId, out PlayerResources current))
                return false;

            _resourcesByPlayer[playerId] =
                new PlayerResources(gold, current.Wood, current.MeatCurrent, current.MeatMaximum);
            return true;
        }

        public bool TrySetWood(int playerId, int wood)
        {
            if (wood < 0 || !TryGetResources(playerId, out PlayerResources current))
                return false;

            _resourcesByPlayer[playerId] =
                new PlayerResources(current.Gold, wood, current.MeatCurrent, current.MeatMaximum);
            return true;
        }

        public bool TrySetMeatCurrent(int playerId, int meatCurrent)
        {
            if (meatCurrent < 0 || !TryGetResources(playerId, out PlayerResources current))
                return false;

            _resourcesByPlayer[playerId] =
                new PlayerResources(current.Gold, current.Wood, meatCurrent, current.MeatMaximum);
            return true;
        }

        public bool TrySetMeatMaximum(int playerId, int meatMaximum)
        {
            if (meatMaximum < 0 || !TryGetResources(playerId, out PlayerResources current))
                return false;

            _resourcesByPlayer[playerId] =
                new PlayerResources(current.Gold, current.Wood, current.MeatCurrent, meatMaximum);
            return true;
        }

        private static bool AreValid(PlayerResources resources)
        {
            return resources.Gold >= 0
                   && resources.Wood >= 0
                   && resources.MeatCurrent >= 0
                   && resources.MeatMaximum >= 0;
        }
    }
}
