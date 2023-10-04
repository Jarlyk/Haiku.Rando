using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haiku.Rando.Topology
{
    public sealed class RandoCheck : IRandoNode
    {
        public RandoCheck(CheckType type, int sceneId, Vector2 position, int checkId)
        {
            Type = type;
            SceneId = sceneId;
            Position = position;
            CheckId = checkId;
        }

        public string Name => $"{Type}[{CheckId}]";

        public CheckType Type { get; }

        public Vector2 Position { get; }

        public int SceneId { get; }

        public int CheckId { get; }

        public int SaveId { get; set; }

        public bool IsShopItem { get; set; }

        public string Alias { get; set; }

        public int Index { get; set; }

        public string GetAlias(int sceneId) => Alias;

        public Vector2 GetPosition(int sceneId) => Position;

        public bool InScene(int sceneId) => sceneId == SceneId;

        public List<GraphEdge> Incoming { get; } = new List<GraphEdge>();

        IReadOnlyList<GraphEdge> IRandoNode.Incoming => Incoming;

        IReadOnlyList<GraphEdge> IRandoNode.Outgoing => Array.Empty<GraphEdge>();

        public override string ToString() => $"{SceneId}:{Name}";
    }
}
