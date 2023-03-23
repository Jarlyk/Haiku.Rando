namespace Haiku.Rando
{
    public class GenerationSettings
    {
        public string Seed;
        public bool RandomStartLocation;
        internal Bitset64 Pools;
        internal Bitset64 StartingItems;
        public RandomizationLevel Level;

        public bool Contains(Pool p) => Pools.Contains((int)p);
        public bool Contains(StartingItemSet s) => StartingItems.Contains((int)s);
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
        Lore
    }

    public enum StartingItemSet
    {
        Wrench,
        Whistle,
        Maps
    }
}