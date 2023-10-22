using System;

namespace Haiku.Rando.Topology
{
    //TODO: Check for the 'children' in save the children quest in Steam Town

    [Serializable]
    public enum CheckType
    {
        Wrench,
        Bulblet,
        Ability,
        Item,
        Chip,
        ChipSlot,
        MapDisruptor,
        Lore,
        Lever,
        Splunk,
        PowerCell,
        Coolant,
        FireRes,
        WaterRes,
        TrainStation,
        Clock,
        RESERVED, // used to be Filler; kept to avoid renumbering the others
        MapMarker,
        MoneyPile
    }
}
