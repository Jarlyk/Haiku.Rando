namespace Haiku.Rando.Topology
{
    public sealed class InRoomEdge : IRandoEdge
    {
        public string Name => $"{Origin?.GetAlias(SceneId) ?? string.Empty}_{Destination?.GetAlias(SceneId) ?? string.Empty}";

        public int SceneId { get; set; }

        public IRandoNode Origin { get; set; }

        public IRandoNode Destination { get; set; }

        public InRoomEdge(IRandoNode origin, IRandoNode destination)
        {
            Origin = origin;
            Destination = destination;
        }
    }
}
