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
                                                                                           
        public Vector2 Position1 { get; set; }

        public Vector2 Position2 { get; set; }

        public string Alias1 { get; set; }

        public string Alias2 { get; set; }

        public string GetAlias(int sceneId)
        {
            if (sceneId == SceneId1) return Alias1 ?? Name;
            if (sceneId == SceneId2) return Alias2 ?? Name;
            return Name;
        }

        public Vector2 GetPosition(int sceneId)
        {
            if (sceneId == SceneId1) return Position1;
            if (sceneId == SceneId2) return Position2;
            return Vector2.zero;
        }

        public List<GraphEdge> Incoming { get; } = new List<GraphEdge>();

        public List<GraphEdge> Outgoing { get; } = new List<GraphEdge>();

        IReadOnlyList<GraphEdge> IRandoNode.Incoming => Incoming;

        IReadOnlyList<GraphEdge> IRandoNode.Outgoing => Outgoing;
    }
}
