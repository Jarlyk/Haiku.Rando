using System.Collections.Generic;

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
    }
}
