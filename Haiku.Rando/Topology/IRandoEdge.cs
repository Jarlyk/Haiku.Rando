namespace Haiku.Rando.Topology
{
    public interface IRandoEdge
    {
        string Name { get; }

        int SceneId { get; }

        IRandoNode Origin { get; }

        IRandoNode Destination { get; }
    }
}
