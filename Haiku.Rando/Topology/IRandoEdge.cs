namespace Haiku.Rando.Topology
{
    public interface IRandoEdge
    {
        string Name { get; }

        IRandoNode Origin { get; }

        IRandoNode Destination { get; }
    }
}
