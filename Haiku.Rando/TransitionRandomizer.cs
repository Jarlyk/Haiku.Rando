using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Haiku.Rando.Logic;
using Haiku.Rando.Topology;
using Haiku.Rando.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Haiku.Rando
{
    public sealed class TransitionRandomizer
    {
        private readonly RandoTopology _topology;
        private readonly LogicEvaluator _logic;
        private readonly Xoroshiro128Plus _random;
        private readonly Dictionary<TransitionNode, TransitionNode> _swaps = new Dictionary<TransitionNode, TransitionNode>();

        public IReadOnlyDictionary<TransitionNode, TransitionNode> Swaps => _swaps;

        public TransitionRandomizer(RandoTopology topology, LogicEvaluator logic, Seed128 seed)
        {
            _topology = topology;
            _logic = logic;
            _random = new Xoroshiro128Plus(seed.S0, seed.S1);
        }

        // TODO: exclude Old Arcadia and Archives transitions if their respective
        // settings are off

        public void Randomize(GenerationSettings gs)
        {
            switch (gs.Level)
            {
                case RandomizationLevel.Doors:
                    RandomizeDoors();
                    break;
                case RandomizationLevel.Rooms:
                    RandomizeAll();
                    break;
                default:
                    throw new InvalidOperationException("tried to randomize transitions without any transition rando enabled");
            }
        }

        public void RandomizeAll()
        {
            //TODO: Multiple attempts, in case randomization fails
            //We only want to swap transition nodes that change scenes, ignoring the Train
            //We're also only looking at Edge and Door transitions; things get weird with stuff like elevators
            var availableNodes = _topology.Nodes.OfType<TransitionNode>().Where(n => !n.InScene(SpecialScenes.Train) && n.SceneId1 != n.SceneId2
            && (n.Type == TransitionType.RoomEdge || n.Type == TransitionType.Door)).ToList();
            TryRandomize(availableNodes);
        }

        public void RandomizeDoors()
        {
            var doorTransitions = new HashSet<(int, int)>
            {
                (95, 97), // Steam Town
                (24, 166), // Tire Village
                (222, 46), // Freezer
                (138, 227), // First Tree
                (220, 169), // Echo
                (197, 224), // Reaper
                (208, 223), // Elder Snailbot
                (270, 254), // Reactor Core
            };
            var doorNodes = _topology.Transitions
                .Where(n => doorTransitions.Contains((n.SceneId1, n.SceneId2)) ||
                            doorTransitions.Contains((n.SceneId2, n.SceneId1)))
                .ToList();
            TryRandomize(doorNodes);
        }

        private bool TryRandomize(List<TransitionNode> availableNodes)
        {
            var blacklistedTransitions = new Dictionary<int, int>
            {
                { 62, 205 }, // virus entrance
                { 139, 144 }, // TE fight entrance
                { 98, 91 }, // MM drop
                { 34, 10 }, // Start area drop
                { 50, 196 }, // Surface drop left
                { 210, 35 }, // Post-Neutron exit
                { 117, 210 }, // Water-locked transition to right of elevator in Ducts
                { 137, 138} // Door Boss transition
            };

            //Remove blacklisted transitions
            availableNodes.RemoveAll(n => blacklistedTransitions.TryGetValue(n.SceneId1, out var sceneId2) && n.SceneId2 == sceneId2);

            //Blacklist the TE cutscene trigger
            availableNodes.RemoveAll(n => n.SceneId2 == 5);

            //There are also several invalid edges in the topology from leftover transitions that are no longer used
            //We can detect these by looking for all 'false' logic
            availableNodes.RemoveAll(n => (n.Incoming.Count > 0 && n.Incoming.All(e => _logic.IsFalse(e)) ||
                                           n.Outgoing.Count > 0 && n.Outgoing.All(e => _logic.IsFalse(e))));

            const int swapCount = 200;
            for (int i = 0; i < swapCount; i++)
            {
                //If we run out of nodes to swap, we're done
                if (availableNodes.Count == 0) break;

                //Pick any random node from the available set
                int pick1 = _random.NextRange(0, availableNodes.Count);
                var node1 = availableNodes[pick1];
                availableNodes.RemoveAt(pick1);

                //And now pick another node, ignoring those that are a bad match
                //In particular, we don't want swaps that reenter the same room
                int sceneId1 = node1.SceneId1;
                var matchingNodes = availableNodes.Where(n => !n.InScene(sceneId1)).ToList();
                if (matchingNodes.Count == 0)
                {
                    //We've run out of nodes that can be swapped, so we're done
                    break;
                }

                //Check if we've formed mutually disjoint graphs
                //If so, we'll try to pick in a way that rejoins them
                var accessible = GetAllAccessibleNodes(node1);
                var remainder = matchingNodes.Except(accessible).ToList();
                TransitionNode node2;
                if (remainder.Count > 0)
                {
                    int pick2 = _random.NextRange(0, remainder.Count);
                    node2 = remainder[pick2];
                }
                else
                {
                    int pick2 = _random.NextRange(0, matchingNodes.Count);
                    node2 = matchingNodes[pick2];
                }
                availableNodes.Remove(node2);

                //Save the mapping, as the TransitionManager needs to know this to actually swap the transition during gameplay
                Debug.Log($"Swapping transition {node1.Name} with {node2.Name}");
                _swaps.Add(node2, node1);
                _swaps.Add(node1, node2);

                //We're going to track the transitions involved here as 1_2 and 3_4
                //Our goal is change the topology so that we end up with 1_4 and 3_2
                int sceneId2 = node1.SceneId2;
                int sceneId3 = node2.SceneId1;
                int sceneId4 = node2.SceneId2;
                //var scene1 = _topology.Scenes[sceneId1];
                var scene2 = _topology.Scenes[sceneId2];
                //var scene3 = _topology.Scenes[sceneId3];
                var scene4 = _topology.Scenes[sceneId4];

                //Replace node linkages in scenes 2 and 4
                //Scenes 1 and 3 remain linked to their original node
                AdjustEdges(node1, node2, scene2);
                AdjustEdges(node2, node1, scene4);

                //We also need to update the scene references and aliases, as otherwise logic won't bind properly
                scene2.Nodes.Remove(node1);
                scene2.Nodes.Add(node2);
                scene4.Nodes.Remove(node2);
                scene4.Nodes.Add(node1);
                var swap = node2.Alias2;
                node2.Alias2 = node1.Alias2;
                node1.Alias2 = swap;
                node1.SceneId2 = sceneId4;
                node2.SceneId2 = sceneId2;
            }

            //TODO: Sanity check the resulting topology
            return true;
        }

        private IEnumerable<TransitionNode> GetAllAccessibleNodes(TransitionNode start)
        {
            var visited = new HashSet<TransitionNode>();
            var pending = new Stack<TransitionNode>();

            pending.Push(start);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                visited.Add(node);

                foreach (var edge in node.Outgoing)
                {
                    if (edge.Destination is TransitionNode next && !visited.Contains(next) && !pending.Contains(next))
                    {
                        pending.Push(next);
                    }
                }
            }

            return visited;
        }

        private void AdjustEdges(TransitionNode oldNode, TransitionNode newNode, RoomScene scene)
        {
            foreach (var edge in scene.Edges.Where(e => e.Destination == oldNode))
            {
                edge.Destination = newNode;
                oldNode.Incoming.Remove(edge);
                newNode.Incoming.Add(edge);
            }

            foreach (var edge in scene.Edges.Where(e => e.Origin == oldNode))
            {
                edge.Origin = newNode;
                oldNode.Outgoing.Remove(edge);
                newNode.Outgoing.Add(edge);
            }
        }
    }
}
