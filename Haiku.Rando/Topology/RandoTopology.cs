using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Haiku.Rando.Topology
{
    public sealed class RandoTopology
    {
        public RandoTopology(IReadOnlyDictionary<int, RoomScene> scenes, IReadOnlyList<IRandoNode> nodes, IReadOnlyList<GraphEdge> edges)
        {
            Scenes = scenes;
            Nodes = nodes;
            Transitions = Nodes.OfType<TransitionNode>().ToArray();
            Checks = Nodes.OfType<RandoCheck>().ToArray();
            Edges = edges;
        }

        public IReadOnlyDictionary<int, RoomScene> Scenes { get; }

        public IReadOnlyList<IRandoNode> Nodes { get; }

        public IReadOnlyList<TransitionNode> Transitions { get; }

        public IReadOnlyList<RandoCheck> Checks { get; }

        public IReadOnlyList<GraphEdge> Edges { get; }

        public void Serialize(StreamWriter writer)
        {
            var dto = new TopologyDto();
            dto.transitions = new TransitionNodeDto[Transitions.Count];
            dto.checks = new RandoCheckDto[Checks.Count];
            dto.edges = new EdgeDto[Edges.Count];

            for (int i = 0; i < dto.transitions.Length; i++)
            {
                ToDto(Transitions[i], ref dto.transitions[i]);
            }

            for (int i = 0; i < dto.checks.Length; i++)
            {
                ToDto(Checks[i], ref dto.checks[i]);
            }

            for (int i = 0; i < dto.edges.Length; i++)
            {
                ToDto(Edges[i], ref dto.edges[i]);
            }

            var text = JsonUtility.ToJson(dto, true);
            writer.Write(text);
        }

        public static RandoTopology Deserialize(StreamReader reader)
        {
            var text = reader.ReadToEnd();
            var dto = JsonUtility.FromJson<TopologyDto>(text);

            var scenes = new Dictionary<int, RoomScene>();
            var transitions = new TransitionNode[dto.transitions.Length];
            for (int i = 0; i < transitions.Length; i++)
            {
                transitions[i] = FromDto(ref dto.transitions[i]);
                GetScene(scenes, transitions[i].SceneId1).Nodes.Add(transitions[i]);
                GetScene(scenes, transitions[i].SceneId2).Nodes.Add(transitions[i]);
            }

            var checks = new RandoCheck[dto.checks.Length];
            for (int i = 0; i < checks.Length; i++)
            {
                checks[i] = FromDto(ref dto.checks[i]);
                GetScene(scenes, checks[i].SceneId).Nodes.Add(checks[i]);
            }

            var nodes = transitions.Cast<IRandoNode>().Concat(checks).ToArray();
            var edges = new GraphEdge[dto.edges.Length];
            for (int i = 0; i < edges.Length; i++)
            {
                edges[i] = FromDto(nodes, ref dto.edges[i]);

                //Wire up edge references
                ((TransitionNode)edges[i].Origin).Outgoing.Add(edges[i]);
                if (edges[i].Destination is TransitionNode dest)
                {
                    dest.Incoming.Add(edges[i]);
                }
                else if (edges[i].Destination is RandoCheck check)
                {
                    check.Incoming.Add(edges[i]);
                }

                GetScene(scenes, edges[i].SceneId).Edges.Add(edges[i]);
            }

            return new RandoTopology(scenes, nodes, edges);
        }

        private static RoomScene GetScene(Dictionary<int, RoomScene> scenes, int sceneId)
        {
            if (!scenes.TryGetValue(sceneId, out var scene))
            {
                scene = new RoomScene(sceneId);
                scenes.Add(scene.SceneId, scene);
            }

            return scene;
        }

        private static void ToDto(TransitionNode data, ref TransitionNodeDto dto)
        {
            dto.name = data.Name;
            dto.type = data.Type;
            dto.sceneId1 = data.SceneId1;
            dto.sceneId2 = data.SceneId2;
            dto.position1 = data.Position1;
            dto.position2 = data.Position2;
            dto.alias1 = data.Alias1;
            dto.alias2 = data.Alias2;
        }

        private static void ToDto(RandoCheck data, ref RandoCheckDto dto)
        {
            dto.type = data.Type;
            dto.position = data.Position;
            dto.sceneId = data.SceneId;
            dto.checkId = data.CheckId;
            dto.saveId = data.SaveId;
            dto.isShopItem = data.IsShopItem;
            dto.alias = data.Alias;
        }

        private static void ToDto(GraphEdge edge, ref EdgeDto dto)
        {
            dto.sceneId = edge.SceneId;
            dto.origin = edge.Origin.Name;
            dto.destination = edge.Destination.Name;
        }

        private static TransitionNode FromDto(ref TransitionNodeDto dto)
        {
            var data = new TransitionNode(dto.name, dto.type, dto.sceneId1, dto.sceneId2);
            data.Position1 = dto.position1;
            data.Position2 = dto.position2;
            data.Alias1 = dto.alias1;
            data.Alias2 = dto.alias2;
            return data;
        }

        private static RandoCheck FromDto(ref RandoCheckDto dto)
        {
            var data = new RandoCheck(dto.type, dto.sceneId, dto.position, dto.checkId);
            data.SaveId = dto.saveId;
            data.IsShopItem = dto.isShopItem;
            data.Alias = dto.alias;
            return data;
        }

        private static GraphEdge FromDto(IReadOnlyList<IRandoNode> nodes, ref EdgeDto dto)
        {
            var originName = dto.origin;
            var destinationName = dto.destination;
            var origin = nodes.FirstOrDefault(n => n.Name == originName);
            var destination = nodes.FirstOrDefault(n => n.Name == destinationName);
            var edge = new GraphEdge(dto.sceneId, origin, destination);
            return edge;
        }

        private struct TopologyDto
        {
            public TransitionNodeDto[] transitions;
            public RandoCheckDto[] checks;
            public EdgeDto[] edges;
        }

        private struct TransitionNodeDto
        {
            public string name;
            public TransitionType type;
            public int sceneId1;
            public int sceneId2;
            public Vector2 position1;
            public Vector2 position2;
            public string alias1;
            public string alias2;
        }

        private struct RandoCheckDto
        {
            public CheckType type;
            public Vector2 position;
            public int sceneId;
            public int checkId;
            public int saveId;
            public bool isShopItem;
            public string alias;
        }

        private struct EdgeDto
        {
            public int sceneId;
            public string origin;
            public string destination;
        }
    }
}
