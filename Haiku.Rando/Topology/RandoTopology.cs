using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

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

            Debug.Log($"Serializing topology: {dto.transitions.Length} transitions");
            for (int i = 0; i < dto.transitions.Length; i++)
            {
                ToDto(Transitions[i], ref dto.transitions[i]);
            }

            Debug.Log($"Serializing topology: {dto.checks.Length} checks");
            for (int i = 0; i < dto.checks.Length; i++)
            {
                ToDto(Checks[i], ref dto.checks[i]);
            }

            Debug.Log($"Serializing topology: {dto.edges.Length} edges");
            for (int i = 0; i < dto.edges.Length; i++)
            {
                ToDto(Edges[i], ref dto.edges[i]);
            }

            var serializer = new JsonSerializer();
            serializer.Serialize(writer, dto);
        }

        public static RandoTopology Deserialize(StreamReader reader)
        {
            var serializer = new JsonSerializer();
            var dto = serializer.Deserialize<TopologyDto>(new JsonTextReader(reader));

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
            dto.position1 = data.Position1.ToString();
            dto.position2 = data.Position2.ToString();
            dto.alias1 = data.Alias1;
            dto.alias2 = data.Alias2;
        }

        private static void ToDto(RandoCheck data, ref RandoCheckDto dto)
        {
            dto.type = data.Type;
            dto.position = data.Position.ToString();
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
            data.Position1 = Parse(dto.position1);
            data.Position2 = Parse(dto.position2);
            data.Alias1 = dto.alias1;
            data.Alias2 = dto.alias2;
            return data;
        }

        private static RandoCheck FromDto(ref RandoCheckDto dto)
        {
            var data = new RandoCheck(dto.type, dto.sceneId, Parse(dto.position), dto.checkId);
            data.SaveId = dto.saveId;
            data.IsShopItem = dto.isShopItem;
            data.Alias = dto.alias;
            return data;
        }

        private static GraphEdge FromDto(IReadOnlyList<IRandoNode> nodes, ref EdgeDto dto)
        {
            var originName = dto.origin;
            var destinationName = dto.destination;
            var sceneId = dto.sceneId;
            var origin = nodes.FirstOrDefault(n => n.InScene(sceneId) && n.Name == originName);
            var destination = nodes.FirstOrDefault(n => n.InScene(sceneId) && n.Name == destinationName);
            var edge = new GraphEdge(sceneId, origin, destination);
            return edge;
        }

        private static Vector2 Parse(string text)
        {
            text = text.Trim('(', ')');
            var split = text.Split(',');
            var x = float.Parse(split[0].Trim());
            var y = float.Parse(split[1].Trim());
            return new Vector2(x, y);
        }
    }

    [Serializable]
    public class TopologyDto
    {
        public TransitionNodeDto[] transitions;
        public RandoCheckDto[] checks;
        public EdgeDto[] edges;
    }

    [Serializable]
    public struct TransitionNodeDto
    {
        public string name;
        public TransitionType type;
        public int sceneId1;
        public int sceneId2;
        public string position1;
        public string position2;
        public string alias1;
        public string alias2;
    }

    [Serializable]
    public struct RandoCheckDto
    {
        public CheckType type;
        public string position;
        public int sceneId;
        public int checkId;
        public int saveId;
        public bool isShopItem;
        public string alias;
    }

    [Serializable]
    public struct EdgeDto
    {
        public int sceneId;
        public string origin;
        public string destination;
    }
}
