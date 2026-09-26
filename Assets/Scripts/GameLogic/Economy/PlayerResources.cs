namespace GameLogic.Economy
{
    /// <summary>One player's gold, wood, and meat population values during a match.</summary>
    public readonly struct PlayerResources
    {
        public int Gold { get; }

        public int Wood { get; }

        public int MeatCurrent { get; }

        public int MeatMaximum { get; }

        public PlayerResources(int gold, int wood, int meatCurrent, int meatMaximum)
        {
            Gold = gold;
            Wood = wood;
            MeatCurrent = meatCurrent;
            MeatMaximum = meatMaximum;
        }
    }
}
