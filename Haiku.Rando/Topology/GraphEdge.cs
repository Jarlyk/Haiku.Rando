namespace Haiku.Rando.Topology
{
    public sealed class GraphEdge
    {
        public string Name => $"{Origin?.GetAlias(SceneId) ?? string.Empty}_{Destination?.GetAlias(SceneId) ?? string.Empty}";

        public int SceneId { get; }

        public IRandoNode Origin { get; }

        public IRandoNode Destination { get; }

        public GraphEdge(int sceneId, IRandoNode origin, IRandoNode destination)
        {
            SceneId = sceneId;
            Origin = origin;
            Destination = destination;
        }
    }
}
