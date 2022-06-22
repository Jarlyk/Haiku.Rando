using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Haiku.Rando.Topology;
using MonoMod.Utils;
using UnityEngine;

namespace Haiku.Rando.Logic
{
    public sealed class LogicLayer
    {

        public LogicLayer(IReadOnlyDictionary<int, SceneLogic> logicByScene)
        {
            LogicByScene = logicByScene;

            var logicByEdge = new Dictionary<GraphEdge, IReadOnlyList<LogicSet>>();
            foreach (var sceneLogic in LogicByScene.Values)
            {
                foreach (var logicSet in sceneLogic.LogicByEdge)
                {
                    logicByEdge.Add(logicSet.Key, logicSet.Value);
                }
            }

            LogicByEdge = logicByEdge;
        }

        public IReadOnlyDictionary<int, SceneLogic> LogicByScene { get; }

        public IReadOnlyDictionary<GraphEdge, IReadOnlyList<LogicSet>> LogicByEdge { get; }

        public static LogicLayer Deserialize(RandoTopology topology, StreamReader reader)
        {
            var logicByScene = new Dictionary<int, Dictionary<GraphEdge, List<LogicSet>>>();

            int lineNumber = 0;
            RoomScene scene = null;
            Dictionary<GraphEdge, List<LogicSet>> logicByEdge = null;
            while (!reader.EndOfStream)
            {
                lineNumber++;
                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("//")) continue;

                if (line.StartsWith("scene", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!int.TryParse(line.Substring(5).Trim(), out var sceneId))
                    {
                        throw new FormatException(
                            $"Invalid scene specifier in logic file at line #{lineNumber}: '{line}'");
                    }

                    scene = topology.Scenes[sceneId];
                    if (!logicByScene.TryGetValue(sceneId, out logicByEdge))
                    {
                        logicByEdge = new Dictionary<GraphEdge, List<LogicSet>>();
                        logicByScene.Add(sceneId, logicByEdge);
                    }
                    continue;
                }

                var colonIndex = line.IndexOf(':');
                if (colonIndex == -1)
                {
                    Debug.LogError($"Missing colon in logic file at line #{lineNumber}: '{line}'");
                    continue;
                }

                var edgeName = line.Substring(0, colonIndex).Trim();
                var split = edgeName.Split('_');
                if (split.Length != 2)
                {
                    Debug.LogError($"Unexpected edge format in logic file at line #{lineNumber}: '{line}'");
                    continue;
                }

                if (scene == null)
                {
                    Debug.LogError($"No scene defined yet in logic file at line #{lineNumber}: '{line}'");
                    continue;
                }

                var nodes1 = scene.FindNodes(split[0]);
                if (nodes1.Count == 0)
                {
                    Debug.LogError($"Unable to resolve nodes for '{split[0]}' in scene {scene.SceneId} in logic file at line #{lineNumber}: '{line}'");
                    continue;
                }

                var nodes2 = scene.FindNodes(split[1]);
                if (nodes2.Count == 0)
                {
                    Debug.LogError($"Unable to resolve nodes for '{split[1]}' in scene {scene.SceneId} in logic file at line #{lineNumber}: '{line}'");
                    continue;
                }

                foreach (var node1 in nodes1)
                {
                    foreach (var node2 in nodes2)
                    {
                        var edge = scene.Edges.FirstOrDefault(e => e.Origin == node1 && e.Destination == node2);
                        if (edge != null)
                        {
                            if (!logicByEdge.TryGetValue(edge, out var logicList))
                            {
                                logicList = new List<LogicSet>();
                                logicByEdge.Add(edge, logicList);
                            }

                            var conditions = new List<LogicCondition>();
                            //TODO: Parse expression into set of conditions

                            var set = new LogicSet(conditions);
                            logicList.Add(set);
                        }
                    }
                }
            }

            return new LogicLayer(logicByScene.ToDictionary(p => p.Key,
                                                            p => new SceneLogic(p.Value.ToDictionary(x => x.Key,
                                                                x => (IReadOnlyList<LogicSet>)x.Value))));
        }
    }
}
