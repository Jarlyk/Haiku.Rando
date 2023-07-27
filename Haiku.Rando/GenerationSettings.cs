namespace Haiku.Rando
{
    public class GenerationSettings
    {
        public string Seed;
        public bool RandomStartLocation;
        public bool TrainLoverMode;
        internal Bitset64 Pools;
        internal Bitset64 StartingItems;
        internal Bitset64 Skips;
        public RandomizationLevel Level;

        public bool Contains(Pool p) => Pools.Contains((int)p);
        public bool Contains(StartingItemSet s) => StartingItems.Contains((int)s);
        public bool Contains(Skip s) => Skips.Contains((int)s);
    }

    public enum Pool
    {
        Wrench,
        Bulblet,
        Abilities,
        Items,
        Chips,
        ChipSlots,
        MapDisruptors,
        MapMarkers,
        PowerCells,
        Coolant,
        Sealants,
        Lore,
        ScrapShrines,
        Clock,
        Levers
    }

    public enum StartingItemSet
    {
        Wrench,
        Whistle,
        Maps
    }

    public enum Skip
    {
        EnemyPogos,
        BLJ,
        BombJumps,
        DarkRooms,
        HazardRooms,
        SkillChips,
        DoubleJumpChains
    }
}