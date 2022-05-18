using System.Collections.Generic;

namespace Haiku.Rando.Topology
{
    public sealed class RoomScene
    {
        public int SceneId { get; set; }

        public List<IRandoNode> Nodes { get; set; } = new List<IRandoNode>();
    }
}
