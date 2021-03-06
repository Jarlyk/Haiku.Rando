using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Haiku.Rando.Checks;
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
            var groups = new Dictionary<string, List<string>>();

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

                    groups.Clear();
                    continue;
                }

                if (line.StartsWith("$"))
                {
                    var startBrace = line.IndexOf('{');
                    var endBrace = line.IndexOf('}');
                    if (startBrace == -1 || endBrace == -1)
                    {
                        Debug.LogError($"Missing curly braces in group definition at line #{lineNumber}: '{line}'");
                    }

                    var groupName = line.Substring(1, startBrace-1).Trim();
                    var listText = line.Substring(startBrace + 1, endBrace - startBrace - 1);
                    var listSplit = listText.Split(',').Select(s => s.Trim()).ToList();
                    groups.Add(groupName, listSplit);
                    continue;
                }

                var colonIndex = line.IndexOf(':');
                if (colonIndex == -1)
                {
                    Debug.LogError($"Missing colon in logic file at line #{lineNumber}: '{line}'");
                    continue;
                }

                bool twoWay = false;
                var edgeName = line.Substring(0, colonIndex).Trim();
                var split = edgeName.Split('_');
                if (split.Length != 2)
                {
                    split = edgeName.Split('=');
                    if (split.Length != 2)
                    {
                        Debug.LogError($"Unexpected edge format in logic file at line #{lineNumber}: '{line}'");
                        continue;
                    }

                    twoWay = true;
                }

                if (scene == null)
                {
                    Debug.LogError($"No scene defined yet in logic file at line #{lineNumber}: '{line}'");
                    continue;
                }

                var nodes1 = FindNodes(scene, split[0], groups);
                if (nodes1.Count == 0)
                {
                    Debug.LogError($"Unable to resolve nodes for '{split[0]}' in scene {scene.SceneId} in logic file at line #{lineNumber}: '{line}'");
                    continue;
                }

                var nodes2 = FindNodes(scene, split[1], groups);
                if (nodes2.Count == 0)
                {
                    Debug.LogError($"Unable to resolve nodes for '{split[1]}' in scene {scene.SceneId} in logic file at line #{lineNumber}: '{line}'");
                    continue;
                }

                //Parse the actual conditions list
                var conditionText = line.Substring(colonIndex + 1);
                var conditionSplit = conditionText.Split('+');
                var conditions = new List<LogicCondition>();
                for (int i = 0; i < conditionSplit.Length; i++)
                {
                    var hashIndex = conditionSplit[i].IndexOf('#');
                    string stateText;
                    int count = 1;
                    if (hashIndex > -1)
                    {
                        var countText = conditionSplit[i].Substring(0, hashIndex).Trim();
                        stateText = conditionSplit[i].Substring(hashIndex + 1).Trim();
                        if (!int.TryParse(countText, out count))
                        {
                            Debug.LogError($"Unexpected numeric format for condition count '{conditionSplit[i]}' in scene {scene.SceneId} in logic file at line #{lineNumber}: '{line}'");

                        }
                    }
                    else
                    {
                        stateText = conditionSplit[i].Trim();
                    }

                    stateText = ExpandAlias(stateText, scene);
                    conditions.Add(new LogicCondition(stateText, count));
                }

                //Apply this logic set to all edges between the nodes
                var set = new LogicSet(conditions);
                AddLogic(nodes1, nodes2, scene, logicByEdge, set);
                if (twoWay)
                {
                    AddLogic(nodes2, nodes1, scene, logicByEdge, set);
                }
            }

            //Expose final result as an immutable collection
            return new LogicLayer(logicByScene.ToDictionary(p => p.Key,
                                                            p => new SceneLogic(p.Value.ToDictionary(x => x.Key,
                                                                x => (IReadOnlyList<LogicSet>)x.Value))));
        }

        private static IReadOnlyList<IRandoNode> FindNodes(RoomScene scene, string pattern, Dictionary<string, List<string>> groups)
        {
            return groups.TryGetValue(pattern, out var list)
                ? list.SelectMany(scene.FindNodes).ToList()
                : scene.FindNodes(pattern);
        }

        private static void AddLogic(IReadOnlyList<IRandoNode> nodes1, IReadOnlyList<IRandoNode> nodes2, RoomScene scene, Dictionary<GraphEdge, List<LogicSet>> logicByEdge,
                                     LogicSet set)
        {
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

                        logicList.Add(set);
                    }
                }
            }
        }

        private static string ExpandAlias(string stateText, RoomScene scene)
        {
            var check = scene.Nodes.OfType<RandoCheck>().FirstOrDefault(c => c.Alias == stateText);
            return check != null ? LogicEvaluator.GetStateName(check) : stateText;
        }
    }
}
