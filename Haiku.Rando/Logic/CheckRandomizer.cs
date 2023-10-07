using Haiku.Rando.Topology;
using Haiku.Rando.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Haiku.Rando.Checks;
using UnityEngine;

namespace Haiku.Rando.Logic
{
    public sealed class CheckRandomizer
    {
        public GenerationSettings Settings { get; internal set; }
        public RandoTopology Topology { get; internal set; }
        public IReadOnlyDictionary<RandoCheck, IRandoItem> CheckMapping { get; internal set; }
        public int? StartScene { get; internal set; }
        public int StartSpareParts { get; internal set; }
        internal int? StartStation { get; set; }
        public IReadOnlyList<LogicLayer> Logic { get; internal set; }

        public static CheckRandomizer TryRandomize(RandoTopology topology, LogicEvaluator logic, GenerationSettings gs, Seed128 seed, int? startScene)
        {
            // CheckRandomizerBuilder contains a lot of data structures that are only needed during
            // randomization and can be discarded afterward; that is why it is a separate object.
            // CheckRandomizer only provides the public interface to the randomization process
            // and parameters.
            try
            {
                return new CheckRandomizerBuilder(topology, logic, gs, seed, startScene).Randomize();
            }
            catch (RandomizationException ex)
            {
                Debug.LogWarning(ex.Message);
                return null;
            }
        }

        internal void SetCheckMapping(RandoCheck location, IRandoItem item)
        {
            var dict = (InsertionOrderDictionary<RandoCheck, IRandoItem>)CheckMapping;
            dict[location] = item;
        }

        // exported for use by RandoMap
        public static Dictionary<(int, string), LogicSymbol> BossTransitions() => new()
        {
            {(19, "Right"), LogicSymbol.MiniBoss}, // Magnet
            {(27, "Right"), LogicSymbol.MiniBoss}, // Tire Mother
            {(201, "Left"), LogicSymbol.NeutronOrElectronBoss}, // Neutron
            {(49, "Left"), LogicSymbol.MiniBoss}, // TV
            {(161, "Right0"), LogicSymbol.MiniBoss}, // Big Drill
            {(137, "Left"), LogicSymbol.MiniBoss}, // Door
            {(144, "Left"), LogicSymbol.CreatorTrioBoss}, // Creators
            {(128, "Right1"), LogicSymbol.MiniBoss}, // Scuba Heads
            {(69, "Right0"), LogicSymbol.MiniBoss}, // Car Battery
            {(84, "Right"), LogicSymbol.NeutronOrElectronBoss}, // Electron
            {(184, "Right"), LogicSymbol.MiniBoss}, // Buzzsaw (also needs Magnet)
            {(212, "Left"), LogicSymbol.MiniBoss}, // Big Brother
            {(200, "Right"), LogicSymbol.ProtonBoss}, // Proton (also needs FireRes)
            {(98, "Right"), LogicSymbol.MiniBoss}, // Mischevious
            {(205, null), LogicSymbol.VirusBoss} // Virus
        };

        // This is the maximum number of checks that can fit in a Bitset64.
        // Any more than that will be left blank (not vanilla), by replacing with
        // a check for which AlreadyHasCheck returns true always.
        internal const int MaxFillerChecks = 64;
    }

    internal sealed class CheckRandomizerBuilder : ICheckRandoContext
    {
        // things that are needed post-randomization
        private readonly RandoTopology _topology;
        private readonly LogicEvaluator _logic;
        private readonly int? _startScene;
        private int? _startStation;
        private int _startScrap;
        private readonly List<(RandoCheck, IRandoItem)> _checkMapping = new();
        private bool _randomized;

        // things that are only needed during randomization
        private readonly ushort[] _acquiredSymbols = new ushort[NumLogicSymbols];
        private readonly ushort[] _chipSlotsUsed = new ushort[3];
        private readonly HashSet<RandoCheck> _startingPool = new();
        private readonly CheckPool _pool = new CheckPool();
        private readonly HashSet<RandoCheck> _visitedChecks = new();
        private readonly List<InLogicCheck> _checksToReplace = new();
        private readonly Xoroshiro128Plus _random;
        private readonly System.Diagnostics.Stopwatch _mainTimer = new();
        private readonly System.Diagnostics.Stopwatch _exploreTimer = new();
        private readonly System.Diagnostics.Stopwatch _updateFrontierTimer = new();

        private const int NumLogicSymbols = (int)LogicSymbol.False;

        public CheckRandomizerBuilder(RandoTopology topology, LogicEvaluator logic, GenerationSettings gs, Seed128 seed, int? startScene)
        {
            _topology = topology;
            _logic = logic;
            _startScene = startScene;

            Settings = gs;
            _random = new Xoroshiro128Plus(seed.S0, seed.S1);
            _logic.Context = this;
        }

        public GenerationSettings Settings { get; }

        public CheckRandomizer Randomize()
        {
            if (_randomized)
            {
                throw new InvalidOperationException("Randomization already complete; to randomize again, please create a new instance of CheckRandomizerBuilder");
            }
            _randomized = true;

            SyncedRng.SequenceSeed = _random.NextULong();

            _mainTimer.Start();
            BuildPool();
            ArrangeChecks();
            _mainTimer.Stop();
            Debug.Log($"explore: {_exploreTimer.ElapsedMilliseconds} frontier: {_updateFrontierTimer.ElapsedMilliseconds} total: {_mainTimer.ElapsedMilliseconds}");
            return new()
            {
                Settings = Settings,
                Topology = _topology,
                CheckMapping = new InsertionOrderDictionary<RandoCheck, IRandoItem>(_checkMapping),
                StartScene = _startScene,
                StartStation = _startStation,
                StartSpareParts = _startScrap,
                Logic = _logic.Layers,
            };
        }

        private void ArrangeChecks()
        {
            //We're going to explore, keeping track of what we can reach and what frontier of edges are not yet passable
            var frontier = new List<FrontierEdge>();
            var explored = new HashSet<GraphEdge>();
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
                Explore(depth, frontier, explored);
                depth++;
                if (frontier.Count == 0)
                {
                    //If there's no more frontier, we've fully progressed
                    //All remaining checks in pools can be populated at will
                    Debug.Log($"Rando: Frontier has been fully explored, so placing remaining {_pool.Count} checks");
                    PlaceAllRemainingChecks();
                    break;
                }

                //Compute what checks we have available for each pool
                UpdateFrontierLogic(frontier, depth);

                //Determine possible frontier edges we want to unlock and score them to weight random selection
                var frontierEdges = frontier.Where(e => e.CanUnlock).ToList();
                if (frontierEdges.Count == 0)
                {
                    Debug.LogWarning("Frontier has run out of checks that can be unlocked");
                    PlaceAllRemainingChecks();
                    break;
                }
                //Randomly choose an edge we want to unlock
                var frontierSet = new WeightedSet<FrontierEdge>(frontierEdges, WeighFrontier);
                var nextEdge = frontierSet.PickItem(_random.NextDouble());
                Debug.Log($"Rando: Chose edge {nextEdge.Edge.SceneId}:{nextEdge.Edge.Name} for placing conditions");

                //Unlock the checks required for this edge
                foreach (var condition in nextEdge.MissingLogic)
                {
                    PlaceChecksToSatisfyCondition(condition);
                }
            }
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

        private void PlaceChecksToSatisfyCondition(LogicCondition condition)
        {
            Debug.Log($"Satisfying condition {condition}");
            //Find and weigh remaining check locations
            var candidates = new WeightedSet<InLogicCheck>(_checksToReplace, WeighCheckPlacement);

            //Choose from weighted distribution and replace each check in turn
            for (int i = 0; i < condition.Count; i++)
            {
                var match = _pool.FirstOrDefault(c => condition.Symbol == LogicEvaluator.SymbolForCheck(c));
                if (match == null)
                {
                    throw new RandomizationException($"Failed to locate check for symbol {condition.Symbol}; logic may not be solvable");
                }

                // Place extra chip slots as necessary to ensure that all chips that are required for progression
                // can be equipped simultaneously.
                // In practice, it may not always be necessary to do so due to repair stations allowing the player to switch
                // chips, but accounting for this is substantially more complex.
                if (match.Type == CheckType.Chip)
                {
                    var color = LogicEvaluator.ChipSlotColorSymbol(GameManager.instance.chip[match.CheckId].chipColor);
                    var j = (int)color - (int)LogicSymbol.RedChipSlot;
                    _chipSlotsUsed[j]++;
                    if (_chipSlotsUsed[j] > _acquiredSymbols[(int)color])
                    {
                        var slot = _pool.FirstOrDefault(c => LogicEvaluator.SymbolForCheck(c) == color);
                        if (slot == null)
                        {
                            throw new RandomizationException($"Ran out of {color}s while placing {match.Name}");
                        }
                        PlaceItem(candidates, slot);
                    }
                }

                PlaceItem(candidates, match);
            }
        }

        private void PlaceItem(WeightedSet<InLogicCheck> candidates, RandoCheck newItem)
        {
            if (candidates.Count == 0)
            {
                throw new RandomizationException($"Ran out of locations to place {newItem.Name}");
            }
            var original = candidates.RemoveItem(_random.NextDouble());
            ApplyProximityPenalty(original.Check, 3);
            SetCheckMapping(original.Check, newItem);
            _pool.Remove(newItem);
            Acquire(newItem);
            Debug.Log($"Replaced check {original.Check.Name} with {newItem.Name}");
        }

        private void SetCheckMapping(RandoCheck original, IRandoItem newItem)
        {
            _checkMapping.Add((original, newItem));
            // Duplicate a subset of train shop checks onto the Abandoned Wastes pre-train shop.
            if (original.IsShopItem && original.SceneId == SpecialScenes.Train)
            {
                var outsideLocation = _topology.Scenes[SpecialScenes.AbandonedWastesStation].Nodes.OfType<RandoCheck>()
                    .Where(c => c.IsShopItem && c.Type == original.Type && c.CheckId == original.CheckId)
                    .FirstOrDefault();
                if (outsideLocation != null)
                {
                    _checkMapping.Add((outsideLocation, newItem));
                    Debug.Log($"Replaced duplicate check {outsideLocation.Name} with {newItem}");
                }
            }
        }

        private void UpdateFrontierLogic(List<FrontierEdge> frontier, int depth)
        {
            _updateFrontierTimer.Start();
            //Update current missing logic on the frontier
            foreach (var edge in frontier)
            {
                edge.MissingLogic = _logic.GetMissingLogic(edge.Edge, _random);
                edge.BacktrackDepth = depth - edge.Depth;

                //TODO: Need to take into account logic requirements in context of overlapping pools
                var canUnlock = true;
                foreach (var logic in edge.MissingLogic)
                {
                    var availCount = _pool
                                        .Count(c => LogicEvaluator.SymbolForCheck(c) == logic.Symbol);
                    if (availCount < logic.Count)
                    {
                        canUnlock = false;
                        break;
                    }
                }

                edge.CanUnlock = canUnlock;
            }

            //Compute total of each missing logic symbol
            // False may appear in the logic, so we need to reserve space for it.
            var logicTotals = new ushort[NumLogicSymbols + 1];
            foreach (var logic in frontier.SelectMany(e => e.MissingLogic))
            {
                logicTotals[(int)logic.Symbol] += (ushort)logic.Count;
            }

            //Compute uniqueness
            foreach (var edge in frontier)
            {
                double p = 1;
                foreach (var logic in edge.MissingLogic)
                {
                    p *= logic.Count/(double)logicTotals[(int)logic.Symbol];
                }
                edge.Uniqueness = 1 - p;
            }
            _updateFrontierTimer.Stop();
        }

        private void PlaceAllRemainingChecks()
        {
            if (_pool.Count > _checksToReplace.Count)
            {
                throw new RandomizationException($"insufficient locations to place all remaining checks; {_checksToReplace.Count} existing, {_pool.Count} needed");
            }

            UniformShuffle(_checksToReplace);

            for (var i = 0; i < _pool.Count; i++)
            {
                Debug.Log($"Remaining checks, replaced {_checksToReplace[i]} with {_pool[i]}");
                SetCheckMapping(_checksToReplace[i].Check, _pool[i]);
            }

            for (var i = _pool.Count; i < _checksToReplace.Count; i++)
            {
                var j = i - _pool.Count;
                IRandoItem filler;
                if (j < CheckRandomizer.MaxFillerChecks)
                {
                    // Zero is not appropriate value for the index, as there is
                    // an actual check at index 0 in the topology.
                    filler = new FillerItem(i - _pool.Count) { Index = _topology.Checks.Count + j };
                }
                else
                {
                    Debug.Log("Out of filler checks. Will leave placement blank.");
                    filler = BlankItem.Instance;
                }
                Debug.Log($"Remaining checks, replaced {_checksToReplace[i]} with {filler}");
                SetCheckMapping(_checksToReplace[i].Check, filler);
            }

            _pool.Clear();
            _checksToReplace.Clear();
        }

        private void UniformShuffle(List<InLogicCheck> checks)
        {
            for (var i = 0; i < checks.Count; i++)
            {
                var j = _random.NextRange(i, checks.Count);
                var c = checks[i];
                checks[i] = checks[j];
                checks[j] = c;
            }
        }

        private static double WeighFrontier(FrontierEdge edge)
        {
            var u = edge.Uniqueness;
            var d = edge.BacktrackDepth;
            var c = edge.MissingLogic.Count;
            return (100*u*u*u + d*d)/(c + 1);
        }

        private static double WeighCheckPlacement(InLogicCheck check)
        {
            //We prefer placement deeper in logic
            var d = check.Depth;

            //We prefer not to place progression in shops, especially when we're at low depth
            //This is intended to bias against placements that require farming money to progress
            var shopPenalty = check.Check.IsShopItem ? 10/check.Depth : 1;

            return (double)(d*d*d)/(1 + check.ProximityPenalty + shopPenalty);
        }

        private void Explore(int depth, List<FrontierEdge> frontier, HashSet<GraphEdge> explored)
        {
            _exploreTimer.Start();
            var pendingExploration = new Stack<FrontierEdge>(frontier);
            while (pendingExploration.Count > 0)
            {
                var edge = pendingExploration.Pop();
                var edgeLogic = _logic.GetAllLogic(edge.Edge);
                if (edgeLogic.Count == 1 && 
                    edgeLogic[0].Conditions.Count == 1 && 
                    edgeLogic[0].Conditions[0].Symbol == LogicSymbol.False)
                {
                    //Edges with only false logic aren't worth considering
                    continue;
                }

                if (_logic.CanTraverse(edge.Edge))
                {
                    frontier.Remove(edge);
                    explored.Add(edge.Edge);
                    if (edge.Edge.Destination is RandoCheck check)
                    {
                        if (!_visitedChecks.Contains(check))
                        {
                            if (_startingPool.Contains(check))
                            {
                                Debug.Log($"Found check {check} at depth {depth} from edge {edge.Edge}; will replace from pool");
                                _checksToReplace.Add(new InLogicCheck(check, depth));
                            }
                            else if (!IsAbandonedWastesShopItem(check))
                            {
                                Debug.Log($"Found check {check} at depth {depth} from edge {edge.Edge}; leaving as vanilla");
                                Acquire(check);
                            }
                            _visitedChecks.Add(check);
                        }
                    }
                    else if (edge.Edge.Destination is TransitionNode node)
                    {
                        MarkBossTransition(node.SceneId1, node.Alias1);
                        MarkBossTransition(node.SceneId2, node.Alias2);
                        foreach (var e in node.Outgoing)
                        {
                            var shouldExplore = !explored.Contains(e) &&
                                pendingExploration.All(x => x.Edge != e) &&
                                frontier.All(x => x.Edge != e);
                            if (shouldExplore)
                            {
                                pendingExploration.Push(new FrontierEdge(e, depth));
                            }
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
            _exploreTimer.Stop();
        }

        private void Acquire(RandoCheck c)
        {
            _acquiredSymbols[(int)LogicEvaluator.SymbolForCheck(c)]++;
        }

        private void MarkBossTransition(int sceneId, string alias)
        {
            var k = (sceneId, alias);
            if (_bossTransitions.TryGetValue(k, out var sym))
            {
                Debug.Log($"Got boss {sym} from {sceneId}:{alias}");
                _acquiredSymbols[(int)sym]++;
                // Each boss should be counted only once, so that logic relying
                // on their count functions as intended.
                _bossTransitions.Remove(k);
            }
        }

        private readonly Dictionary<(int, string), LogicSymbol> _bossTransitions = CheckRandomizer.BossTransitions();

        private void ApplyProximityPenalty(RandoCheck origin, int startPenalty)
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
                        var ctr = _checksToReplace.FirstOrDefault(c => c.Check == check);
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
            if (Settings.Contains(Pool.PowerCells)) AddToPool(CheckType.PowerCell);
            if (Settings.Contains(Pool.Coolant)) AddToPool(CheckType.Coolant);
            if (Settings.Contains(Pool.Sealants)) AddToPool(CheckType.FireRes);
            if (Settings.Contains(Pool.Sealants)) AddToPool(CheckType.WaterRes);
            if (Settings.Contains(Pool.Lore)) AddToPool(CheckType.Lore);
            if (Settings.Contains(Pool.MapMarkers)) AddToPool(CheckType.MapMarker);
            if (Settings.Contains(Pool.Clock)) AddToPool(CheckType.Clock);
            if (Settings.Contains(Pool.ScrapShrines))
            {
                var piles = ConsolidateMoneyPiles();
                _pool.AddRange(piles);
                // Make all piles not in the consolidated set disappear.
                foreach (var c in _topology.Checks)
                {
                    if (c.Type == CheckType.MoneyPile && !piles.Contains(c))
                    {
                        _checkMapping.Add((c, BlankItem.Instance));
                    }
                }
            };
            if (Settings.Contains(Pool.Levers)) AddToPool(CheckType.Lever);

            // Starting pool contains all the checks we're going to replace eventually.
            // The checks in the Abandoned Wastes shop are all duplicates of those in the train, and become inaccessible once
            // the train is unlocked.
            _pool.RemoveAll(IsAbandonedWastesShopItem);
            foreach (var c in _pool)
            {
                _startingPool.Add(c);
            }

            // Add the initial chip slots the player starts with
            _acquiredSymbols[(int)LogicSymbol.RedChipSlot]++;
            _acquiredSymbols[(int)LogicSymbol.GreenChipSlot]++;
            _acquiredSymbols[(int)LogicSymbol.BlueChipSlot]++;

            //We remove a few checks from the source pool based on special starting conditions
            if (Settings.Contains(StartingItemSet.Wrench))
            {
                _pool.RemoveAll(c => c.Type == CheckType.Wrench ||
                                    (c.Type == CheckType.Item && c.CheckId == (int)ItemId.Wrench));
                _acquiredSymbols[(int)LogicSymbol.Wrench]++;
            }
            else
            {
                // If the Wrench is randomized, we want to leave only one copy of it around.
                // If it is not, we want to leave zero copies of it in the pool.
                _pool.RemoveAll(c => c.Type == CheckType.Item && c.CheckId == (int)ItemId.Wrench);
            }
            if (Settings.Contains(StartingItemSet.Whistle))
            {
                _pool.RemoveAll(c => c.Type == CheckType.Item && c.CheckId == (int)ItemId.Whistle);
            }
            if (Settings.Contains(StartingItemSet.Maps))
            {
                _pool.RemoveAll(c => c.Type == CheckType.MapDisruptor);
            }
            if (Settings.Contains(StartingItemSet.SpareParts))
            {
                var scrapRNG = _random.Clone();
                _startScrap = scrapRNG.NextRange(300, 501);
            }
            if (Settings.TrainLoverMode)
            {
                _pool.RemoveAll(c => c.Type == CheckType.Clock);
                var stations = GameManager.instance.trainStations;
                if (Settings.RandomStartLocation)
                {
                    var stationRNG = _random.Clone();
                    _startStation = stationRNG.NextRange(0, stations.Length);
                }
                else
                {
                    _startStation = 0;
                }
                _acquiredSymbols[(int)LogicSymbol.AbandonedWastesTrainStation  + (int)_startStation]++;
                _acquiredSymbols[(int)LogicSymbol.Clock]++;
            }
        }

        private static bool IsAbandonedWastesShopItem(RandoCheck c) => c.IsShopItem && c.SceneId == SpecialScenes.AbandonedWastesStation;

        private void AddToPool(CheckType checkType)
        {
            _pool.AddRange(_topology.Checks.Where(c => c.Type == checkType));
        }

        private List<RandoCheck> ConsolidateMoneyPiles()
        {
            var rooms = _topology.Checks.Where(c => c.Type == CheckType.MoneyPile).GroupBy(c => c.SceneId);
            var selectedPiles = new List<RandoCheck>();
            foreach (var pileSet in rooms)
            {
                List<List<RandoCheck>> bags, oldBags;
                bags = new();
                foreach (var p in pileSet)
                {
                    bags.Add(new() { p });
                }
                do
                {
                    oldBags = bags;
                    foreach (var b1 in oldBags)
                    {
                        foreach (var b2 in oldBags)
                        {
                            if (b1 != b2 && AreBagsAdjacent(b1, b2))
                            {
                                b1.AddRange(b2);
                                b2.Clear();
                            }
                        }
                    }
                    bags = oldBags.Where(b => b.Count > 0).ToList();
                }
                while (bags.Count < oldBags.Count);
                foreach (var bag in bags)
                {
                    bag[0].SaveId = bag.Select(p => p.SaveId).Sum();
                    selectedPiles.Add(bag[0]);
                }
            }
            
            return selectedPiles;
        }

        private static bool AreBagsAdjacent(List<RandoCheck> b1, List<RandoCheck> b2)
        {
            foreach (var p1 in b1)
            {
                foreach (var p2 in b2)
                {
                    if ((p1.Position - p2.Position).sqrMagnitude < 36)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public int GetCount(LogicSymbol s) => _acquiredSymbols[(int)s];

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
