using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Haiku.Rando.Topology;
using Haiku.Rando.Util;
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
        private readonly Xoroshiro128Plus _random;
        private bool _randomized;

        public CheckRandomizer(CheckRandomizerConfig config, RandoTopology topology, LogicEvaluator logic)
        {
            _config = config;
            _topology = topology;
            _logic = logic;
            _random = new Xoroshiro128Plus(_config.Seed);
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
            var reachableTransitions = new List<TransitionNode>();
            var availableChecks = new List<RandoCheck>();
            var frontier = new List<FrontierEdge>();
            var explored = new List<FrontierEdge>();

            //Populate our initial frontier based on our start point
            //TODO: Support other start locations besides standard wake
            var startTrans = _topology.Transitions.First(t => t.Name == $"{SpecialScenes.GameStart}Wake");
            reachableTransitions.Add(startTrans);
            frontier.AddRange(startTrans.Outgoing.Select(e => new FrontierEdge(e, 0)));

            //We want to keep exploring and populating checks for as long as we have remaining checks in our pools
            int depth = 1;
            while (_pools.Any(p => p.Count > 0))
            {
                //Explore the frontier, expanding our available checks
                Explore(depth, reachableTransitions, availableChecks, frontier, explored);
                depth++;
                if (frontier.Count == 0)
                {
                    //If there's no more frontier, we've fully progressed
                    //All remaining checks in pools can be populated at will
                    //TODO: Populate checks
                    break;
                }

                //Compute what checks we have available for each pool
                var availableByPool = _pools.Select(p => availableChecks.Intersect(p).ToList()).ToList();

                //Update current missing logic on the frontier
                foreach (var edge in frontier)
                {
                    edge.MissingLogic = _logic.GetMissingLogic(edge.Edge);
                    edge.BacktrackDepth = depth - edge.Depth;

                    //TODO: Need to take into account logic requirements in context of overlapping pools
                    var canUnlock = true;
                    foreach (var logic in edge.MissingLogic)
                    {
                        var matchingPools = availableByPool.Where(p => p.Any(c => _logic.MatchesState(c, logic.StateName))).ToList();
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

                //Determine possible frontier edges we want to unlock and score them to weight random selection
                var frontierSet = WeightedSet<FrontierEdge>.Build(frontier.Where(e => e.CanUnlock), WeighFrontier);
                if (frontierSet.Count == 0)
                {
                    Debug.LogWarning("Frontier has run out of checks that can be unlocked; logic may not be solvable");
                    return false;
                }

                //Randomly choose an edge we want to unlock
                var nextEdge = frontierSet.PickItem(_random.NextDouble());

                //Place required checks to satisfy the logic
                //TODO

                //TODO: Get the checks we need to place in order to unlock
                //TODO: Place the required checks
            }

            //Successfully arranged all checks
            return true;
        }

        private double WeighFrontier(FrontierEdge edge)
        {
            //TODO
            return 0;
        }

        private double WeighCheckPlacement(RandoCheck check)
        {
            //TODO
            return 0;
        }

        private void Explore(int depth, List<TransitionNode> reachableTransitions, List<RandoCheck> availableChecks, List<FrontierEdge> frontier, List<FrontierEdge> explored)
        {
            var pendingExploration = new Stack<FrontierEdge>(frontier);
            while (pendingExploration.Count > 0)
            {
                var edge = pendingExploration.Pop();
                if (_logic.CanTraverse(edge.Edge))
                {
                    frontier.Remove(edge);
                    explored.Add(edge);
                    if (edge.Edge.Destination is RandoCheck check)
                    {
                        availableChecks.Add(check);
                    }
                    else if (edge.Edge.Destination is TransitionNode node)
                    {
                        reachableTransitions.Add(node);
                        foreach (var edgeOut in node.Outgoing.Where(e => explored.All(x => x.Edge != e)))
                        {
                            pendingExploration.Push(new FrontierEdge(edgeOut, depth));
                        }
                    }
                }
                else
                {
                    if (!frontier.Contains(edge))
                        frontier.Add(edge);
                }
            }
        }

        private void BuildPools()
        {
            //TODO: Create pools based on config
            //For testing, we're going to start with just a simple pool
            var pool = BuildPool(CheckType.Ability, CheckType.Chip, CheckType.Item, CheckType.ChipSlot);
            _pools.Add(pool);
        }

        private CheckPool BuildPool(params CheckType[] checkTypes)
        {
            var pool = new CheckPool();
            pool.AddRange(_topology.Checks.Where(c => checkTypes.Contains(c.Type)));
            return pool;
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
    }
}
