using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Haiku.Rando.Topology;
using Haiku.Rando.Util;

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

        private void ArrangeChecks()
        {
            //We're going to explore, keeping track of what we can reach and what frontier of edges are not yet passable
            var reachableTransitions = new List<TransitionNode>();
            var availableChecks = new List<RandoCheck>();
            var frontier = new List<IRandoEdge>();
            var explored = new List<IRandoEdge>();

            //TODO: Populate our initial frontier based on our start point

            //We want to keep exploring and populating checks for as long as we have remaining checks in our pools
            while (_pools.Any(p => p.Count > 0))
            {
                //Explore the frontier, expanding our available checks
                Explore(reachableTransitions, availableChecks, frontier, explored);
                if (frontier.Count == 0)
                {
                    //If there's no more frontier, we've fully progressed
                    //All remaining checks in pools can be populated at will
                    //TODO: Populate checks
                    break;
                }

                //Compute what checks we have available for each pool
                var availableByPool = _pools.Select(p => availableChecks.Intersect(p).ToList()).ToList();

                //TODO: Determine possible frontier edges we want to unlock and score them to weight random selection
                //TODO: Choose an edge to unlock
                //TODO: Get the checks we need to place in order to unlock
                //TODO: Place the required checks
            }
        }

        private void Explore(List<TransitionNode> reachableTransitions, List<RandoCheck> availableChecks, List<IRandoEdge> frontier, List<IRandoEdge> explored)
        {
            var pendingExploration = new Stack<IRandoEdge>(frontier);
            while (pendingExploration.Count > 0)
            {
                var edge = pendingExploration.Pop();
                if (_logic.CanTraverse(edge))
                {
                    frontier.Remove(edge);
                    explored.Add(edge);
                    if (edge.Destination is RandoCheck check)
                    {
                        availableChecks.Add(check);
                    }
                    else if (edge.Destination is TransitionNode node)
                    {
                        reachableTransitions.Add(node);
                        foreach (var edgeOut in node.Outgoing.Where(e => !explored.Contains(e)))
                        {
                            pendingExploration.Push(edgeOut);
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
            var allChecks = _topology.Nodes.OfType<RandoCheck>();
            pool.AddRange(allChecks.Where(c => checkTypes.Contains(c.Type)));
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
    }
}
