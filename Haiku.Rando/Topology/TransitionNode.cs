using System.Collections.Generic;
using UnityEngine;

namespace Haiku.Rando.Topology
{
    public sealed class TransitionNode : IRandoNode
    {
        public TransitionNode(string name, TransitionType type, int sceneId1, int sceneId2)
        {
            Name = name;
            Type = type;
            SceneId1 = sceneId1;
            SceneId2 = sceneId2;
        }
        public string Name { get; }

        public TransitionType Type { get; }

        public int SceneId1 { get; }

        public int SceneId2 { get; }

        public RoomScene Scene1 { get; set; }

        public RoomScene Scene2 { get; set; }

        public Vector2 Position1 { get; set; }

        public Vector2 Position2 { get; set; }

        public List<IRandoEdge> Incoming { get; set; } = new List<IRandoEdge>();

        public List<IRandoEdge> Outgoing { get; set; } = new List<IRandoEdge>();

        IReadOnlyList<IRandoEdge> IRandoNode.Incoming => Incoming;

        IReadOnlyList<IRandoEdge> IRandoNode.Outgoing => Outgoing;
    }
}
