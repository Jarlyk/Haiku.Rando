﻿using Haiku.Rando.Topology;
using Haiku.Rando.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Haiku.Rando.Checks;
using UnityEngine;

namespace Haiku.Rando.Logic
{
    public sealed class CheckRandomizer : ICheckRandoContext
    {
        private readonly RandoTopology _topology;
        private readonly LogicEvaluator _logic;
        private readonly int? _startScene;
        private readonly HashSet<string> _acquiredStates = new HashSet<string>();
        private readonly Dictionary<RandoCheck, RandoCheck> _checkMapping = new Dictionary<RandoCheck, RandoCheck>();
        private readonly CheckPool _startingPool = new CheckPool();
        private readonly CheckPool _pool = new CheckPool();
        private readonly List<RandoCheck> _visitedChecks = new List<RandoCheck>();
        private readonly Xoroshiro128Plus _random;
        private bool _randomized;
        private int _numFillersAdded;

        public CheckRandomizer(RandoTopology topology, LogicEvaluator logic, GenerationSettings gs, Seed128 seed, int? startScene)
        {
            _topology = topology;
            _logic = logic;
            _startScene = startScene;

            Settings = gs;
            _random = new Xoroshiro128Plus(seed.S0, seed.S1);
            _logic.Context = this;
        }

        public GenerationSettings Settings { get; }

        public RandoTopology Topology => _topology;

        public IReadOnlyDictionary<RandoCheck, RandoCheck> CheckMapping => _checkMapping;

        public int? StartScene => _startScene;

        public IReadOnlyList<LogicLayer> Logic => _logic.Layers;

        public bool Randomize()
        {
            if (_randomized)
            {
                throw new InvalidOperationException("Randomization already complete; to randomize again, please create a new instance of CheckRandomizer");
            }

            SyncedRng.SequenceSeed = _random.NextULong();
            BuildPool();
            bool success = ArrangeChecks();

            _randomized = true;
            return success;
        }

        private bool ArrangeChecks()
        {
            //We're going to explore, keeping track of what we can reach and what frontier of edges are not yet passable
            var remainingChecks = _startingPool.ToList();
            var checksToReplace = new List<InLogicCheck>();
            var frontier = new List<FrontierEdge>();
            var explored = new List<FrontierEdge>();
            _visitedChecks.Clear();

            //Populate our initial frontier based on our start point
            TransitionNode startTrans;
            if (_startScene == null)
            {
                startTrans = _topology.Transitions.First(t => t.Name == $"{SpecialScenes.GameStart}Wake");
            }
            else
            {
                startTrans = _topology.Scenes[_startScene.Value].Nodes.OfType<TransitionNode>()
                                      .First(n => n.Type == TransitionType.RepairStation);
            }

            frontier.AddRange(startTrans.Outgoing.Select(e => new FrontierEdge(e, 0)));
            Debug.Log($"Rando: Starting frontier with {frontier.Count} edges starting at {startTrans.Name}");

            //We want to keep exploring and populating checks for as long as we have remaining checks in our pools
            int depth = 1;
            while (_pool.Count > 0)
            {
                //Explore the frontier, expanding our available checks
                Explore(depth, checksToReplace, frontier, explored);
                depth++;
                if (frontier.Count == 0)
                {
                    //If there's no more frontier, we've fully progressed
                    //All remaining checks in pools can be populated at will
                    Debug.Log($"Rando: Frontier has been fully explored, so placing remaining {remainingChecks.Count} checks");
                    PlaceAllRemainingChecks(checksToReplace);
                    break;
                }

                //Compute what checks we have available for each pool
                UpdateFrontierLogic(frontier, depth);

                //Determine possible frontier edges we want to unlock and score them to weight random selection
                var frontierSet = WeightedSet<FrontierEdge>.Build(frontier.Where(e => e.CanUnlock), WeighFrontier);
                if (frontierSet.Count == 0)
                {
                    Debug.LogWarning("Frontier has run out of checks that can be unlocked");
                    PlaceAllRemainingChecks(checksToReplace);
                    break;
                }

                //Randomly choose an edge we want to unlock
                var nextEdge = frontierSet.PickItem(_random.NextDouble());
                Debug.Log($"Rando: Chose edge {nextEdge.Edge.SceneId}:{nextEdge.Edge.Name} for placing conditions");

                //Unlock the checks required for this edge
                foreach (var condition in nextEdge.MissingLogic)
                {
                    if (!PlaceChecksToSatisfyCondition(checksToReplace, condition, remainingChecks)) return false;
                }
            }

            if (_pool.Count > 10)
            {
                Debug.LogWarning($"There are still {_pool.Count} checks not yet placed; this is too many, so rerolling");
                return false;
            }
            else if (_pool.Count > 0)
            {
                Debug.LogWarning($"Completed with {_pool.Count} checks not yet placed; this is low enough that will still attempt to use this seed");
            }

            //Successfully arranged all checks
            return true;
        }

        private string ListToString<T>(IReadOnlyList<T> list)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < list.Count; i++)
            {
                if (i > 0) builder.Append(',');
                var item = list[i];
                builder.Append(item.ToString());
            }

            return builder.ToString();
        }

        private bool PlaceChecksToSatisfyCondition(List<InLogicCheck> checksToReplace, LogicCondition condition, List<RandoCheck> remainingChecks)
        {
            //Find and weigh remaining check locations
            var candidates = WeightedSet<InLogicCheck>.Build(checksToReplace, WeighCheckPlacement);
            if (candidates.Count < condition.Count)
            {
                Debug.LogWarning($"Ran out of locations to place check state {condition.StateName}; logic may not be solvable");
                return false;
            }

            //Choose from weighted distribution and replace each check in turn
            for (int i = 0; i < condition.Count; i++)
            {
                var match = remainingChecks.FirstOrDefault(c => LogicEvaluator.MatchesState(c.SceneId, c, condition.StateName));
                if (match == null)
                {
                    Debug.LogWarning(
                        $"Failed to locate check for check state {condition.StateName}; logic may not be solvable");
                    return false;
                }

                var original = candidates.PickItem(_random.NextDouble());
                if (i < condition.Count - 1)
                {
                    candidates.Remove(original);
                }

                ApplyProximityPenalty(checksToReplace, original.Check, 3);
                _checkMapping.Add(original.Check, match);
                remainingChecks.Remove(match);
                _pool.Remove(match);
                checksToReplace.Remove(original);
                AddState(LogicEvaluator.GetStateName(match));
                Debug.Log($"To satisfy condition {condition}, replaced check {original.Check.Name} with {match.Name}");
            }

            return true;
        }

        private void UpdateFrontierLogic(List<FrontierEdge> frontier, int depth)
        {
            //Update current missing logic on the frontier
            foreach (var edge in frontier)
            {
                edge.MissingLogic = _logic.GetMissingLogic(edge.Edge, _random);
                edge.BacktrackDepth = depth - edge.Depth;

                //TODO: Need to take into account logic requirements in context of overlapping pools
                var canUnlock = true;
                foreach (var logic in edge.MissingLogic)
                {
                    var matching = _pool
                                        .Where(c => LogicEvaluator.MatchesState(edge.Edge.SceneId, c, logic.StateName))
                                        .ToList();
                    var availCount = matching.Count;
                    canUnlock &= availCount >= logic.Count;
                }

                edge.CanUnlock = canUnlock;
            }

            //Compute total of each missing logic state
            var logicTotals = new Dictionary<string, int>();
            foreach (var logic in frontier.SelectMany(e => e.MissingLogic))
            {
                if (!logicTotals.TryGetValue(logic.StateName, out int value))
                {
                    logicTotals.Add(logic.StateName, logic.Count);
                }
                else
                {
                    logicTotals[logic.StateName] += logic.Count;
                }
            }

            //Compute uniqueness
            foreach (var edge in frontier)
            {
                int n = 0;
                double p = 1;
                for (var i = 0; i < edge.MissingLogic.Count; i++)
                {
                    var logic = edge.MissingLogic[i];
                    p *= logic.Count/(double)logicTotals[logic.StateName];
                }

                edge.Uniqueness = 1 - p;
            }
        }

        // This is the maximum number of checks that can fit in a Bitset64.
        // Any more than that will be left blank (not vanilla), by replacing with
        // a check for which AlreadyHasCheck returns true always.
        internal const int MaxFillerChecks = 64;

        private RandoCheck GetArbitraryCheck()
        {
            if (_pool.Count > 0)
            {
                var last = _pool.Count - 1;
                var item = _pool[last];
                _pool.RemoveAt(last);
                return item;
            }
            var filler = new RandoCheck(CheckType.Filler, 0, new(0, 0), _numFillersAdded);
            if (_numFillersAdded >= MaxFillerChecks)
            {
                Debug.Log("Out of filler checks. Will leave placement blank.");
            }
            _numFillersAdded++;
            return filler;
        }

        private void PlaceAllRemainingChecks(List<InLogicCheck> checksToReplace)
        {
            //Find and weigh remaining check locations
            var candidates = WeightedSet<InLogicCheck>.Build(checksToReplace, WeighCheckPlacement);

            //Choose from weighted distribution and replace each check in turn
            while (candidates.Count > 0)
            {
                // TODO: Might want to add duplicate check support
                var match = GetArbitraryCheck();
                var original = candidates.PickItem(_random.NextDouble());
                candidates.Remove(original);

                Debug.Log($"Remaining checks, replaced {original.Check} with {match}");
                _checkMapping.Add(original.Check, match);
                checksToReplace.Remove(original);
            }
        }

        private double WeighFrontier(FrontierEdge edge)
        {
            var u = edge.Uniqueness;
            var d = edge.BacktrackDepth;
            var c = edge.MissingLogic.Count;
            return (100*u*u*u + d*d)/(c + 1);
        }

        private double WeighCheckPlacement(InLogicCheck check)
        {
            //We prefer placement deeper in logic
            var d = check.Depth;

            //We prefer not to place progression in shops, especially when we're at low depth
            //This is intended to bias against placements that require farming money to progress
            var shopPenalty = check.Check.IsShopItem ? 10/check.Depth : 1;

            return (double)(d*d*d)/(1 + check.ProximityPenalty + shopPenalty);
        }

        public static (string, string) TransitionNodeStates(TransitionNode node) => (
            $"Transition[{node.SceneId1}][{node.Alias1}]",
            $"Transition[{node.SceneId2}][{node.Alias2}]"
        );

        private void Explore(int depth, List<InLogicCheck> checksToReplace, List<FrontierEdge> frontier, List<FrontierEdge> explored)
        {
            var pendingExploration = new Stack<FrontierEdge>(frontier);
            while (pendingExploration.Count > 0)
            {
                var edge = pendingExploration.Pop();
                var edgeLogic = _logic.GetAllLogic(edge.Edge);
                if (edgeLogic.Count == 1 && 
                    edgeLogic[0].Conditions.Count == 1 && 
                    string.Equals(edgeLogic[0].Conditions[0].StateName, "false", StringComparison.InvariantCultureIgnoreCase))
                {
                    //Edges with only false logic aren't worth considering
                    continue;
                }

                if (_logic.CanTraverse(edge.Edge))
                {
                    frontier.Remove(edge);
                    explored.Add(edge);
                    if (edge.Edge.Destination is RandoCheck check)
                    {
                        if (!_visitedChecks.Contains(check))
                        {
                            if (_startingPool.Contains(check))
                            {
                                Debug.Log($"Found check {check} at depth {depth} from edge {edge.Edge}; will replace from pool");
                                checksToReplace.Add(new InLogicCheck(check, depth));
                            }
                            else
                            {
                                Debug.Log($"Found check {check} at depth {depth} from edge {edge.Edge}; leaving as vanilla");
                                AddState(LogicEvaluator.GetStateName(check));
                            }
                            _visitedChecks.Add(check);
                        }
                    }
                    else if (edge.Edge.Destination is TransitionNode node)
                    {
                        var (s1, s2) = TransitionNodeStates(node);
                        AddState(s1);
                        AddState(s2);
                        foreach (var edgeOut in node.Outgoing.Where(e => explored.All(x => x.Edge != e) && pendingExploration.All(x => x.Edge != e) && frontier.All(x => x.Edge != e)))
                        {
                            //Debug.Log($"Adding to exploration: {edgeOut.SceneId}:{edgeOut.Name} from {edgeOut.Origin.Name} to {edgeOut.Destination.Name}");
                            pendingExploration.Push(new FrontierEdge(edgeOut, depth));
                        }
                    }
                }
                else
                {
                    if (!frontier.Any(e => e.Edge == edge.Edge))
                    {
                        //Debug.Log($"Cannot traverse {edge.Edge}; adding to frontier");
                        frontier.Add(edge);
                    }
                }
            }
        }

        private void ApplyProximityPenalty(List<InLogicCheck> checksToReplace, RandoCheck origin, int startPenalty)
        {
            var visitedNodes = new List<IRandoNode>();
            visitedNodes.Add(origin);

            var edges = new Stack<DepthEdge>(origin.Incoming.Select(e => new DepthEdge(e, startPenalty)));
            while (edges.Count > 0)
            {
                var edge = edges.Pop();

                if (edge.Edge.Origin is TransitionNode node && !visitedNodes.Contains(node))
                {
                    visitedNodes.Add(node);

                    foreach (var check in node.Outgoing.Select(e => e.Destination).OfType<RandoCheck>())
                    {
                        var ctr = checksToReplace.FirstOrDefault(c => c.Check == check);
                        if (ctr != null)
                        {
                            ctr.ProximityPenalty += edge.Depth;
                        }
                    }

                    if (edge.Depth > 1)
                    {
                        foreach (var inEdge in node.Incoming)
                        {
                            edges.Push(new DepthEdge(inEdge, edge.Depth - 1));
                        }
                    }
                }
            }
        }

        private void BuildPool()
        {
            _pool.Clear();
            _startingPool.Clear();
            if (Settings.Contains(Pool.Wrench)) AddToPool(CheckType.Wrench);
            if (Settings.Contains(Pool.Bulblet)) AddToPool(CheckType.Bulblet);
            if (Settings.Contains(Pool.Abilities)) AddToPool(CheckType.Ability);
            if (Settings.Contains(Pool.Items)) AddToPool(CheckType.Item);
            if (Settings.Contains(Pool.Chips)) AddToPool(CheckType.Chip);
            if (Settings.Contains(Pool.ChipSlots)) AddToPool(CheckType.ChipSlot);
            if (Settings.Contains(Pool.MapDisruptors)) AddToPool(CheckType.MapDisruptor);
            //if (Settings.IncludeLevers.Value) AddToPool(CheckType.Lever);
            if (Settings.Contains(Pool.PowerCells)) AddToPool(CheckType.PowerCell);
            if (Settings.Contains(Pool.Coolant)) AddToPool(CheckType.Coolant);
            if (Settings.Contains(Pool.Sealants)) AddToPool(CheckType.FireRes);
            if (Settings.Contains(Pool.Sealants)) AddToPool(CheckType.WaterRes);
            if (Settings.Contains(Pool.Lore)) AddToPool(CheckType.Lore);
            if (Settings.Contains(Pool.MapMarkers)) AddToPool(CheckType.MapMarker);

            //Starting pool contains all the checks we're going to replace eventually
            _startingPool.AddRange(_pool);

            //We remove a few checks from the source pool based on special starting conditions
            if (Settings.Contains(StartingItemSet.Wrench))
            {
                _pool.RemoveAll(c => c.Type == CheckType.Wrench ||
                                    (c.Type == CheckType.Item && c.CheckId == (int)ItemId.Wrench));
                AddState(LogicStateNames.Heal);
            }
            if (Settings.Contains(StartingItemSet.Whistle))
            {
                _pool.RemoveAll(c => c.Type == CheckType.Item && c.CheckId == (int)ItemId.Whistle);
            }
            if (Settings.Contains(StartingItemSet.Maps))
            {
                _pool.RemoveAll(c => c.Type == CheckType.MapDisruptor);
            }
        }

        private void AddToPool(CheckType checkType)
        {
            _pool.AddRange(_topology.Checks.Where(c => c.Type == checkType));
        }

        private void AddState(string state)
        {
            _acquiredStates.Add(state);
            //Debug.Log($"Added logic state as reachable: {state}");
        }

        public bool HasState(string state)
        {
            return _acquiredStates.Contains(state);
        }

        public int GetCount(string state)
        {
            return _acquiredStates.Count(s => s.StartsWith(state));
        }

        private sealed class CheckPool : List<RandoCheck>
        {
        }

        private sealed class FrontierEdge : DepthEdge
        {
            public FrontierEdge(GraphEdge edge, int depth)
            : base(edge, depth)
            {
            }

            public IReadOnlyList<LogicCondition> MissingLogic { get; set; }

            public bool CanUnlock { get; set; }

            public double Uniqueness { get; set; }

            public int BacktrackDepth { get; set; }
        }

        private sealed class InLogicCheck
        {
            public InLogicCheck(RandoCheck check, int depth)
            {
                Check = check;
                Depth = depth;
            }

            public RandoCheck Check { get; }

            public int Depth { get; }

            public int ProximityPenalty { get; set; }

            public override string ToString()
            {
                return $"D{Depth}:{Check.Name}";
            }
        }

        private class DepthEdge
        {
            public DepthEdge(GraphEdge edge, int depth)
            {
                Edge = edge;
                Depth = depth;
            }

            public GraphEdge Edge { get; }

            public int Depth { get; }
        }
    }
}
