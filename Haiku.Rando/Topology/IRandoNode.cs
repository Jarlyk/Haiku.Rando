using System.Collections.Generic;
using UnityEngine;

namespace Haiku.Rando.Topology
{
    public interface IRandoNode
    {
        string Name { get; }

        string GetAlias(int sceneId);

        Vector2 GetPosition(int sceneId);

        bool InScene(int sceneId);

        IReadOnlyList<GraphEdge> Incoming { get; }

        IReadOnlyList<GraphEdge> Outgoing { get; }
    }
}
