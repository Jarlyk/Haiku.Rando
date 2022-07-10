using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly Xoroshiro128Plus _random;
        private readonly Dictionary<TransitionNode, TransitionNode> _swaps = new Dictionary<TransitionNode, TransitionNode>();

        public IReadOnlyDictionary<TransitionNode, TransitionNode> Swaps => _swaps;

        public TransitionRandomizer(RandoTopology topology, ulong seed)
        {
            _topology = topology;
            _random = new Xoroshiro128Plus(seed);
        }

        public void Randomize()
        {
            //TODO: Multiple attempts, in case randomization fails
            TryRandomize();
        }

        private bool TryRandomize()
        {
            //We only want to swap transition nodes that change scenes, ignoring the Train
            var availableNodes = _topology.Nodes.OfType<TransitionNode>().Where(n => !n.InScene(SpecialScenes.Train) && n.SceneId1 != n.SceneId2).ToList();

            //The 'save the children' transition is kept intact, as the logic gets funky here
            availableNodes.RemoveAll(n => n.SceneId1 == 95 && n.SceneId2 == 97);

            //Virus entrance kept intact
            availableNodes.RemoveAll(n => n.SceneId1 == 62 && n.SceneId2 == 205);

            //TE fight entrance kept intact
            availableNodes.RemoveAll(n => n.SceneId1 == 139 && n.SceneId2 == 144);

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
                int pick2 = _random.NextRange(0, matchingNodes.Count);
                var node2 = matchingNodes[pick2];
                availableNodes.Remove(node2);

                //Save the mapping, as the TransitionManager needs to know this to actually swap the transition during gameplay
                Debug.Log($"Swapping transition {node1.Name} with {node2.Name}");
                _swaps.Add(node2, node1);
                _swaps.Add(node1, node2);

                //We're going to track the transitions involved here as 1_2 and 3_4
                //Our goal is change the topology so that we end up with 1_3 and 3_2
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
                var swap = node1.Alias2;
                node2.Alias2 = node1.Alias2;
                node1.Alias2 = swap;
                node1.SceneId2 = sceneId4;
                node2.SceneId2 = sceneId2;
            }

            //TODO: Sanity check the resulting topology
            return true;
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
