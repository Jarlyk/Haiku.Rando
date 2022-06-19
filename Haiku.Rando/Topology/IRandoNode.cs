using System.Collections.Generic;

namespace Haiku.Rando.Topology
{
    public interface IRandoNode
    {
        string Name { get; }

        string GetAlias(int sceneId);

        IReadOnlyList<GraphEdge> Incoming { get; }

        IReadOnlyList<GraphEdge> Outgoing { get; }
    }
}
