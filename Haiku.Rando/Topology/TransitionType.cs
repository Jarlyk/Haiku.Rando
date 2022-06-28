using System;

namespace Haiku.Rando.Topology
{
    [Serializable]
    public enum TransitionType
    {
        Standard,
        CapsuleElevator,
        Train,
        StartPoint,
        RepairStation,
        HaikuWake
    }
}
