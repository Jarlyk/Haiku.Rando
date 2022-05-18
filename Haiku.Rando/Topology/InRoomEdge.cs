namespace Haiku.Rando.Topology
{
    public sealed class InRoomEdge : IRandoEdge
    {
        public string Name => $"{Origin?.Name ?? string.Empty}_{Destination?.Name ?? string.Empty}";

        public IRandoNode Origin { get; set; }

        public IRandoNode Destination { get; set; }

        public InRoomEdge(IRandoNode origin, IRandoNode destination)
        {
            Origin = origin;
            Destination = destination;
        }
    }
}
