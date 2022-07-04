using System;

namespace Haiku.Rando.Topology
{
    [Serializable]
    public enum TransitionType
    {
        RoomEdge,
        Door,
        CapsuleElevator,
        Train,
        StartPoint,
        RepairStation,
        HaikuWake
    }
}
