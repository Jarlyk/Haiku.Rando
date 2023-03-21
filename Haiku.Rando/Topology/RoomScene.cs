using System;
using System.Collections.Generic;
using System.Linq;

namespace Haiku.Rando.Topology
{
    public sealed class RoomScene
    {

        public RoomScene(int sceneId)
        {
            SceneId = sceneId;
        }

        public int SceneId { get; }

        public List<IRandoNode> Nodes { get; } = new List<IRandoNode>();

        public List<GraphEdge> Edges { get; } = new List<GraphEdge>();

        public IReadOnlyList<IRandoNode> FindNodes(string pattern)
        {
            if (pattern == "*") return Nodes;

            if (pattern.StartsWith("*"))
            {
                return Nodes.Where(n => n.GetAlias(SceneId).EndsWith(pattern.Substring(1), StringComparison.InvariantCultureIgnoreCase))
                            .ToList();
            }

            if (pattern.EndsWith("*"))
            {
                return Nodes.Where(n => n.GetAlias(SceneId).StartsWith(pattern.Substring(0, pattern.Length-1), StringComparison.InvariantCultureIgnoreCase))
                            .ToList();
            }

            return Nodes.Where(n => n.GetAlias(SceneId).Equals(pattern, StringComparison.InvariantCultureIgnoreCase))
                        .ToList();
        }
    }
}
