using Haiku.Rando.Topology;
using Haiku.Rando.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Haiku.Rando.Logic
{
    public sealed class CheckRandomizer : ICheckRandoContext
    {
        private readonly CheckRandomizerConfig _config;
        private readonly RandoTopology _topology;
        private readonly LogicEvaluator _logic;
        private readonly HashSet<string> _acquiredStates = new HashSet<string>();
        private readonly Dictionary<RandoCheck, RandoCheck> _checkMapping = new Dictionary<RandoCheck, RandoCheck>();
        private readonly List<CheckPool> _pools = new List<CheckPool>();
        private readonly List<RandoCheck> _visitedChecks = new List<RandoCheck>();
        private readonly Xoroshiro128Plus _random;
        private bool _randomized;

        public CheckRandomizer(CheckRandomizerConfig config, RandoTopology topology, LogicEvaluator logic)
        {
            _config = config;
            _topology = topology;
            _logic = logic;
            _random = new Xoroshiro128Plus(_config.Seed);
            _logic.Context = this;
        }

        public RandoTopology Topology => _topology;

        public IReadOnlyDictionary<RandoCheck, RandoCheck> CheckMapping => _checkMapping;

        public void Randomize()
        {
            if (_randomized)
            {
                throw new InvalidOperationException("Randomization already complete; to randomize again, please create a new instance of CheckRandomizer");
            }

            BuildPools();
            ArrangeChecks();

            _randomized = true;
        }

        private bool ArrangeChecks()
        {
            //We're going to explore, keeping track of what we can reach and what frontier of edges are not yet passable
            var remainingChecks = _topology.Checks.ToList();
            var checksToReplace = new List<InLogicCheck>();
            var frontier = new List<FrontierEdge>();
            var explored = new List<FrontierEdge>();
            _visitedChecks.Clear();

            //Populate our initial frontier based on our start point
            //TODO: Support other start locations besides standard wake
            var startTrans = _topology.Transitions.First(t => t.Name == $"{SpecialScenes.GameStart}Wake");
            frontier.AddRange(startTrans.Outgoing.Select(e => new FrontierEdge(e, 0)));
            Debug.Log($"Rando: Starting frontier with {frontier.Count} edges starting at Wake");

            //We want to keep exploring and populating checks for as long as we have remaining checks in our pools
            int depth = 1;
            while (_pools.Any(p => p.Count > 0))
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
                Debug.Log($"Rando: All available checks to replace: {ListToString(checksToReplace)}");
                Debug.Log($"Rando: Updating frontier logic for available checks");
                UpdateFrontierLogic(frontier, depth);

                //Determine possible frontier edges we want to unlock and score them to weight random selection
                var frontierSet = WeightedSet<FrontierEdge>.Build(frontier.Where(e => e.CanUnlock), WeighFrontier);
                if (frontierSet.Count == 0)
                {
                    Debug.LogWarning("Frontier has run out of checks that can be unlocked; logic may not be solvable");
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

                _checkMapping.Add(original.Check, match);
                remainingChecks.Remove(match);
                foreach (var pool in _pools)
                    pool.Remove(match);
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
                edge.MissingLogic = _logic.GetMissingLogic(edge.Edge);
                edge.BacktrackDepth = depth - edge.Depth;

                //TODO: Need to take into account logic requirements in context of overlapping pools
                var canUnlock = true;
                foreach (var logic in edge.MissingLogic)
                {
                    var matchingPools = _pools
                                        .Where(
                                            p => p.Any(c => LogicEvaluator.MatchesState(edge.Edge.SceneId, c, logic.StateName)))
                                        .ToList();
                    var availCount = matchingPools.Count > 0 ? matchingPools.Sum(p => p.Count) : 0;
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

        private void PlaceAllRemainingChecks(List<InLogicCheck> checksToReplace)
        {
            //Find and weigh remaining check locations
            var candidates = WeightedSet<InLogicCheck>.Build(checksToReplace, WeighCheckPlacement);

            //Choose from weighted distribution and replace each check in turn
            while (candidates.Count > 0)
            {
                if (_pools.All(p => p.Count == 0))
                {
                    //No more checks to place; leave the rest as vanilla
                    Debug.Log(
                        $"Ran out of checks to place with {candidates.Count} locations still remaining to populate; leaving as vanilla");
                    break;
                }

                //TODO: Multiple pools support
                var match = _pools[0][0];
                var original = candidates.PickItem(_random.NextDouble());
                candidates.Remove(original);

                Debug.Log($"Remaining checks, replaced {original.Check} with {match}");
                _checkMapping.Add(original.Check, match);
                _pools[0].Remove(match);
                checksToReplace.Remove(original);

                //Technically this isn't required, but might be useful for debugging
                //_acquiredStates.Add(_logic.GetStateName(match));
            }
        }

        private double WeighFrontier(FrontierEdge edge)
        {
            var u = edge.Uniqueness;
            var d = edge.Depth;
            return u*u*u + d*d;
        }

        private double WeighCheckPlacement(InLogicCheck check)
        {
            //TODO
            return 1;
        }

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
                            if (_pools.Any(p => p.Any(c => c.Type == check.Type)))
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
                        foreach (var edgeOut in node.Outgoing.Where(e => explored.All(x => x.Edge != e) && pendingExploration.All(x => x.Edge != e)))
                        {
                            Debug.Log($"Adding to exploration: {edgeOut.SceneId}:{edgeOut.Name} from {edgeOut.Origin.Name} to {edgeOut.Destination.Name}");
                            pendingExploration.Push(new FrontierEdge(edgeOut, depth));
                        }
                    }
                }
                else
                {
                    if (!frontier.Contains(edge))
                    {
                        Debug.Log($"Cannot traverse {edge.Edge}; adding to frontier");
                        frontier.Add(edge);
                    }
                }
            }
        }

        private void BuildPools()
        {
            //TODO: Create pools based on config
            //For testing, we're going to start with everything currently supported
            var pool = BuildPool(CheckType.Ability, CheckType.Chip, CheckType.Item, CheckType.ChipSlot, CheckType.MapDisruptor, CheckType.FireRes, CheckType.WaterRes, CheckType.Bulblet, CheckType.PowerCell, CheckType.Coolant);
            _pools.Add(pool);
        }

        private CheckPool BuildPool(params CheckType[] checkTypes)
        {
            var pool = new CheckPool();
            pool.AddRange(_topology.Checks.Where(c => checkTypes.Contains(c.Type)));
            return pool;
        }

        private void AddState(string state)
        {
            _acquiredStates.Add(state);
            Debug.Log($"Added logic state as reachable: {state}");
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

        private sealed class FrontierEdge
        {
            public FrontierEdge(GraphEdge edge, int depth)
            {
                Edge = edge;
                Depth = depth;
            }

            public GraphEdge Edge { get; }

            public int Depth { get; }

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

            public override string ToString()
            {
                return $"D{Depth}:{Check.Name}";
            }
        }
    }
}
