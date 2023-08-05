using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Haiku.Rando.Checks;
using Haiku.Rando.Logic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Haiku.Rando.Topology
{
    public sealed class RandoMapScanner
    {
        private readonly Dictionary<int, RoomScene> _visitedScenes = new Dictionary<int, RoomScene>();
        private readonly Dictionary<string, TransitionNode> _transitionNodes = new Dictionary<string, TransitionNode>();
        private readonly List<IRandoNode> _allNodes = new List<IRandoNode>();
        private readonly List<GraphEdge> _allEdges = new List<GraphEdge>();

        public IEnumerator RunScan()
        {
            //Make player invulnerable to avoid issues as we transition around
            PlayerScript.instance.InvulnerableFor(100);
            PlayerScript.instance.FreezePlayerFor(100);
            PlayerScript.instance.coll.enabled = false;

            //Start scan in our current room
            var pendingScenes = new Stack<int>();
            var currentSceneId = SceneManager.GetActiveScene().buildIndex;
            pendingScenes.Push(currentSceneId);

            while (pendingScenes.Count > 0)
            {
                var sceneId = pendingScenes.Pop();
                if (sceneId != currentSceneId)
                {
                    PlayerScript.instance.startPoint = "";
                    PlayerScript.instance.transform.position = new Vector2(0f, 0f);
                    SceneManager.LoadScene(sceneId);
                    yield return new WaitForSeconds(0.1f);

                    currentSceneId = sceneId;
                }

                Debug.Log($"Adding scene {currentSceneId}");
                var scene = AnalyzeRoom(currentSceneId);
                _visitedScenes.Add(currentSceneId, scene);

                foreach (var outTrans in scene.Nodes.OfType<TransitionNode>())
                {
                    var outSceneId = outTrans.SceneId1 == currentSceneId ? outTrans.SceneId2 : outTrans.SceneId1;
                    if (!IsBossRushScene(outSceneId) && !_visitedScenes.ContainsKey(outSceneId) && !pendingScenes.Contains(outSceneId))
                    {
                        pendingScenes.Push(outSceneId);
                    }
                }
            }

            //Special case: Add train transitions to Train scene
            var trainScene = _visitedScenes[SpecialScenes.Train];
            var trainNodes = _allNodes.OfType<TransitionNode>().Where(n => n.SceneId2 == SpecialScenes.Train).ToList();
            foreach (var node in trainNodes)
            {
                foreach (var otherNode in trainScene.Nodes)
                {
                    var edge1 = new GraphEdge(SpecialScenes.Train, node, otherNode);
                    trainScene.Edges.Add(edge1);
                    _allEdges.Add(edge1);

                    if (otherNode is TransitionNode)
                    {
                        var edge2 = new GraphEdge(SpecialScenes.Train, otherNode, node);
                        trainScene.Edges.Add(edge2);
                        _allEdges.Add(edge2);
                    }
                }
            }
            trainScene.Nodes.AddRange(trainNodes);

            //Update aliases for room edge transitions now that all related scenes are available
            foreach (var node in _transitionNodes.Values.Where(n => n.Type == TransitionType.RoomEdge))
            {
                node.Alias1 = BuildAlias(node, node.SceneId1, node.SceneId2);
                node.Alias2 = BuildAlias(node, node.SceneId2, node.SceneId1);
            }

            //Do a pass through each scene and fix up any duplicated aliases (such as from room edge transitions)
            foreach (var scene in _visitedScenes.Values)
            {
                var byAlias = scene.Nodes.GroupBy(n => n.GetAlias(scene.SceneId));
                foreach (var group in byAlias)
                {
                    var items = group.OrderByDescending(g => g.GetPosition(scene.SceneId).y).ThenBy(g => g.GetPosition(scene.SceneId).x).ToList();
                    if (items.Count > 1)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            if (items[i] is TransitionNode node)
                            {
                                SetAlias(node, scene.SceneId, $"{group.Key}{i}");
                            }
                            else if (items[i] is RandoCheck check)
                            {
                                check.Alias = $"{group.Key}{i}";
                            }
                        }
                    }
                }
            }

            var path = System.IO.Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "Haiku.Rando");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            using (var nodesFile = File.Open(System.IO.Path.Combine(path, "nodes.txt"), FileMode.Create, FileAccess.ReadWrite))
            using (var writer = new StreamWriter(nodesFile))
            {
                foreach (var node in _allNodes)
                {
                    writer.WriteLine($"{node.Name}");
                }
            }

            using (var checksFile = File.Open(System.IO.Path.Combine(path, "checks.txt"), FileMode.Create, FileAccess.ReadWrite))
            using (var writer = new StreamWriter(checksFile))
            {
                writer.WriteLine("SceneId,Check");
                foreach (var check in _allNodes.OfType<RandoCheck>().OrderBy(c => c.SceneId).ThenBy(c => c.Type))
                {
                    writer.WriteLine($"{check.SceneId},{check.Name}");
                }
            }

            using (var edgesFile = File.Open(System.IO.Path.Combine(path, "edges.txt"), FileMode.Create, FileAccess.ReadWrite))
            using (var writer = new StreamWriter(edgesFile))
            {
                foreach (var edge in _allEdges)
                {
                    writer.WriteLine($"{edge.Name}");
                }
            }

            using (var topologyFile = File.Open(System.IO.Path.Combine(path, "HaikuTopology.json"), FileMode.Create, FileAccess.ReadWrite))
            using (var writer = new StreamWriter(topologyFile))
            {
                var topology = new RandoTopology(_visitedScenes, _allNodes, _allEdges);
                topology.Serialize(writer);
            }

            // Create skeleton of logic file with names of transitions/checks in each room
            using (var logicFile = File.Open(System.IO.Path.Combine(path, "LogicSkeleton.txt"), FileMode.Create, FileAccess.ReadWrite))
            using (var writer = new StreamWriter(logicFile))
            {
                writer.WriteLine("// This is a logic file for the Haiku Rando mod");
                writer.WriteLine("// Logic is specified per scene, where the passability is defined between transitions and checks in the room");
                writer.WriteLine("// Edge expressions are one-directional and written as From_To");
                writer.WriteLine("// Wildcard matching is supported, so for example, A_* means all edges from A to anywhere else in the room");
                writer.WriteLine("// Constrained wildcards are also possible, such as A_Lever*, indicating all edges from A to anything starting with 'Lever'");
                writer.WriteLine("//");
                writer.WriteLine("// For the actual logic expressions, they are written as a series of AND conditions using '+' to join them");
                writer.WriteLine("// For example, Ball+Bomb means that you need both the ball and bomb upgrade to travel along this edge");
                writer.WriteLine("// Supported ability names: Bomb,Dash,DoubleJump,Grapple,Heal,Ball,Blink,Magnet,FireRes,WaterRes,Light");
                writer.WriteLine("//");
                writer.WriteLine("// You can also add restrictions based on things like count of power cells found or a specific item");
                writer.WriteLine("// The supported countable/indexable entries are: Chip,Slot,PowerCell,Item,Disruptor,Lever,Coolant");
                writer.WriteLine("// To specify a minimum count, you can use an expression like 10#PowerCell to indicate at least 10 power cells");
                writer.WriteLine("// To specify a particular item, you can use an expression like Item[7] (this would be green skull)");
                writer.WriteLine("// If neither index nor count is specified, will try to match to the only instance of that check in the room; this is especially useful for Lever");
                writer.WriteLine("//");
                writer.WriteLine("// If this is the main logic file, the assumption is that all edges can be traversed unless restricted here");
                writer.WriteLine("// If this is an extension logic file for a skip category, it will only specify the edges that have an alternate traversal logic for the skips");

                foreach (var sceneId in _visitedScenes.Keys.OrderBy(k => k))
                {
                    var scene = _visitedScenes[sceneId];
                    
                    writer.WriteLine();
                    writer.WriteLine($"// Scene {sceneId}");

                    writer.Write("// Transitions:");
                    var transitions = scene.Nodes.OfType<TransitionNode>().ToList();
                    for (int i = 0; i < transitions.Count; i++)
                    {
                        if (i > 0) writer.Write(',');

                        writer.Write(' ');
                        writer.Write(transitions[i].GetAlias(sceneId));
                        writer.Write($" ({transitions[i].Name})");
                    }
                    writer.WriteLine();

                    writer.Write("// Checks:");
                    var checks = scene.Nodes.OfType<RandoCheck>().ToList();
                    for (int i = 0; i < checks.Count; i++)
                    {
                        if (i > 0) writer.Write(',');

                        writer.Write(' ');
                        writer.Write(checks[i].Alias);
                        writer.Write($" ({checks[i].Name})");
                    }
                    writer.WriteLine();

                    writer.WriteLine($"Scene {sceneId}");
                }
            }
        }

        private string BuildAlias(TransitionNode node, int origin, int destination)
        {
            //If there is no path back, we assume this is a one-way downward transition
            if (_visitedScenes.TryGetValue(destination, out var sDestination) && sDestination.Edges.All(e => e.Destination != node) && sDestination.Nodes.OfType<TransitionNode>().Count() > 1)
            {
                return "Down";
            }

            //Otherwise we assume either left or right based on transform scaling embedded in the ordering
            return node.SceneId1 == origin ? "Right" : "Left";
        }

        private static bool IsBossRushScene(int n) =>
            (n >= 235 && n <= 245) || (n >= 261 && n <= 268);

        public RoomScene AnalyzeRoom(int sceneId)
        {
            var scene = new RoomScene(sceneId);
            var transitions = new List<TransitionNode>();

            foreach (var exit in SceneUtils.FindObjectsOfType<CapsuleElevator>())
            {
                //NOTE: We implicitly assume only one elevator is allowed per room
                var trans = GetTransition(exit.pointName, TransitionType.CapsuleElevator, sceneId, exit.levelToLoad);
                SetPosition(trans, sceneId, exit.transform.position);
                SetAlias(trans, sceneId, "Elevator");
                transitions.Add(trans);
            }

            var edgeExits = SceneUtils.FindObjectsOfType<LoadNewLevel>();
            foreach (var exit in edgeExits)
            {
                if (IsBossRushScene(exit.levelToLoad))
                {
                    continue;
                }
                bool isRight = exit.transform.localScale.x >= 1f;
                var trans = GetTransition(BuildPointName(sceneId, exit), TransitionType.RoomEdge, 
                                          isRight ? sceneId : exit.levelToLoad, 
                                          isRight ? exit.levelToLoad : sceneId);
                SetPosition(trans, sceneId, exit.transform.position);
                transitions.Add(trans);
            }

            var doorExits = SceneUtils.FindObjectsOfType<EnterRoomTrigger>()
                .Where(t => !IsCorruptModeOnly(t.gameObject) && !IsBossRushScene(t.levelToLoad)).ToArray();
            foreach (var exit in doorExits)
            {
                var trans = GetTransition(BuildPointName(sceneId, exit), TransitionType.Door, sceneId, exit.levelToLoad);
                SetPosition(trans, sceneId, exit.transform.position);
                var doorAlias = doorExits.Length > 1 ? $"Door{exit.extraInfo}" : "Door";
                SetAlias(trans, sceneId, doorAlias);
                transitions.Add(trans);
            }

            foreach (var exit in SceneUtils.FindObjectsOfType<EnterTrain>())
            {
                var trans = GetTransition($"{sceneId}-Train", TransitionType.Train, sceneId, SpecialScenes.Train);
                SetPosition(trans, sceneId, exit.transform.position);
                trans.Alias1 = "Train";
                trans.Alias2 = $"Exit{sceneId}";
                transitions.Add(trans);
            }

            //Create a node for any PlayerStart locations (used for one-way transitions, typically)
            var startPoints = SceneUtils.FindObjectsOfType<PlayerStartPoint>();
            for (var i = 0; i < startPoints.Length; i++)
            {
                var start = startPoints[i];
                var trans = GetTransition($"{start.pointName ?? $"Start{i}"}", TransitionType.StartPoint, sceneId, sceneId);
                SetPosition(trans, sceneId, start.transform.position);
                var alias = startPoints.Length > 1 ? $"Start{i}" : "Start";
                trans.Alias1 = alias;
                trans.Alias2 = alias;
                transitions.Add(trans);
            }
            
            //Create special transition nodes for repair stations
            var repairStations = SceneUtils.FindObjectsOfType<ReplenishHealth>();
            foreach (var start in repairStations)
            {
                var trans = GetTransition($"{sceneId}Repair", TransitionType.RepairStation, sceneId, sceneId);
                SetPosition(trans, sceneId, start.transform.position);
                trans.Alias1 = "Repair";
                trans.Alias2 = "Repair";
                transitions.Add(trans);
            }

            //Special case for Haiku wake point
            if (sceneId == SpecialScenes.GameStart)
            {
                var trans = GetTransition($"{sceneId}Wake", TransitionType.HaikuWake, sceneId, sceneId);
                SetPosition(trans, sceneId, SceneUtils.FindObjectOfType<IntroSequence>().spawnPoint.position);
                trans.Alias1 = "Wake";
                trans.Alias2 = "Wake";
                transitions.Add(trans);
            }

            //Create nodes for checks in the room
            var checks = FindChecks(sceneId);


            //TODO: Perform reachability analysis
            //TODO: Create AStarPath, configure it and generate paths between nodes

            //Currently we just assume all nodes in the room can reach all other nodes in the room
            foreach (var node in transitions)
            {
                foreach (var otherNode in transitions.Where(n => n != node))
                {
                    var edgeOut = new GraphEdge(sceneId, node, otherNode);
                    scene.Edges.Add(edgeOut);
                    _allEdges.Add(edgeOut);
                }

                //Currently we assume all nodes can reach all checks
                foreach (var check in checks)
                {
                    var edgeCheck = new GraphEdge(sceneId, node, check);
                    scene.Edges.Add(edgeCheck);
                    _allEdges.Add(edgeCheck);
                }
            }

            scene.Nodes.AddRange(transitions);
            scene.Nodes.AddRange(checks);
            return scene;
        }

        private string BuildPointName(int sceneId, LoadNewLevel exit)
        {
            bool isRight = exit.transform.localScale.x >= 1f;
            return isRight ? $"{sceneId}-{exit.levelToLoad}{exit.extraInfo}" : $"{exit.levelToLoad}-{sceneId}{exit.extraInfo}";
        }

        private string BuildPointName(int sceneId, EnterRoomTrigger exit)
        {
            bool isRight = exit.transform.localScale.x >= 1f;
            return isRight ? $"{sceneId}-{exit.levelToLoad}{exit.extraInfo}" : $"{exit.levelToLoad}-{sceneId}{exit.extraInfo}";
        }

        private TransitionNode GetTransition(string name, TransitionType type, int sceneId1, int sceneId2)
        {
            if (!_transitionNodes.TryGetValue(name, out var node))
            {
                node = new TransitionNode(name, type, sceneId1, sceneId2);
                _transitionNodes.Add(name, node);
                _allNodes.Add(node);
                Debug.Log($"Created transition node {name}");
            }

            return node;
        }

        private void SetPosition(TransitionNode node, int sceneId, Vector3 position)
        {
            if (node.SceneId1 == sceneId)
            {
                node.Position1 = position;
            }
            else
            {
                node.Position2 = position;
            }
        }

        private void SetAlias(TransitionNode node, int sceneId, string alias)
        {
            if (node.SceneId1 == sceneId)
            {
                node.Alias1 = alias;
            }
            else
            {
                node.Alias2 = alias;
            }
        }

        // Unused, presumably leftover checks from development
        // and the Badge of Steel rewards from the boss rush.
        private static readonly HashSet<(int sceneId, CheckType type)> ignoredChecks = new()
        {
            (102, CheckType.Chip),
            (104, CheckType.Item),
            (132, CheckType.PowerCell),
            (249, CheckType.Item)
        };

        private List<RandoCheck> FindChecks(int sceneId)
        {
            var checks = new List<RandoCheck>();

            foreach (var pickup in SceneUtils.FindObjectsOfType<PickupWrench>())
            {
                var check = new RandoCheck(CheckType.Wrench, sceneId, pickup.transform.position, 0) { SaveId = pickup.saveID };
                check.Alias = "Wrench";
                checks.Add(check);
            }

            foreach (var pickup in SceneUtils.FindObjectsOfType<PickupBulb>())
            {
                var check = new RandoCheck(CheckType.Bulblet, sceneId, pickup.transform.position, 0);
                check.Alias = "Bulblet";
                checks.Add(check);
            }

            foreach (var lore in SceneUtils.FindObjectsOfType<DialogueTrigger>())
            {
                var checkId = CheckManager.LoreTabletText.FindIndex(t => t.SequenceEqual(lore.dialogue.sentences));
                if (checkId != -1)
                {
                    var check = new RandoCheck(CheckType.Lore, sceneId, lore.transform.position, checkId);
                    check.Alias = "Lore";
                    checks.Add(check);
                }
            }

            foreach (var lore in SceneUtils.FindObjectsOfType<MultipleDialogueTrigger>())
            {
                var sentences = lore.dialogueGroups.SelectMany(d => d.sentences).ToList();
                var checkId = CheckManager.LoreTabletText.FindIndex(t => t.SequenceEqual(sentences));
                if (checkId != -1)
                {
                    var check = new RandoCheck(CheckType.Lore, sceneId, lore.transform.position, checkId);
                    check.Alias = "Lore";
                    checks.Add(check);
                }
            }

            foreach (var pickup in SceneUtils.FindObjectsOfType<UnlockTutorial>())
            {
                var check = new RandoCheck(CheckType.Ability, sceneId, pickup.transform.position, pickup.abilityID);
                check.Alias = LogicEvaluator.GetAbilityStateName((AbilityId)pickup.abilityID);
                checks.Add(check);
            }

            var pickups = SceneUtils.FindObjectsOfType<PickupItem>().Where(p => !IsCorruptModeOnly(p.gameObject)).ToList();
            var chipCount = pickups.Count(p => p.triggerChip);
            var slotCount = pickups.Count(p => p.triggerChipSlot);
            var itemCount = pickups.Count(p => !p.triggerChip && !p.triggerChipSlot && !p.triggerCoolant && !p.triggerPin);
            foreach (var pickup in pickups)
            {
                CheckType type;
                int itemId;
                string alias;
                if (pickup.triggerChip)
                {
                    type = CheckType.Chip;
                    itemId = GameManager.instance.getChipNumber(pickup.chipIdentifier);
                    alias = chipCount == 1 ? "Chip" : null;
                }
                else if (pickup.triggerChipSlot)
                {
                    type = CheckType.ChipSlot;
                    itemId = pickup.chipSlotNumber;
                    alias = slotCount == 1 ? "Slot" : null;
                }
                else if (pickup.triggerCoolant)
                {
                    type = CheckType.Coolant;
                    itemId = 0;
                    alias = "Coolant";
                }
                else if (pickup.triggerPin)
                {
                    //We ignore pins
                    continue;
                }
                else
                {
                    type = CheckType.Item;
                    itemId = pickup.itemID;
                    alias = itemCount == 1 ? "Item" : null;
                }
                if (!ignoredChecks.Contains((sceneId, type)))
                {
                    var check = new RandoCheck(type, sceneId, pickup.transform.position, itemId) { SaveId = pickup.saveID };
                    check.Alias = alias ?? check.Name;
                    checks.Add(check);
                }
            }

            foreach (var pickup in SceneUtils.FindObjectsOfType<Disruptor>().Where(p => !IsCorruptModeOnly(p.gameObject)))
            {
                var check = new RandoCheck(CheckType.MapDisruptor, sceneId, pickup.transform.position, pickup.disruptorID);
                check.Alias = "Disruptor";
                checks.Add(check);
            }

            // There is a room in Old Arcadia that contains two SwitchDoors with the same
            // ID, because a single lever controls two doors.
            var seenLevers = new HashSet<int>();

            foreach (var door in SceneUtils.FindObjectsOfType<SwitchDoor>().Where(d => !IsCorruptModeOnly(d.gameObject)))
            {
                if (seenLevers.Contains(door.doorID))
                {
                    continue;
                }
                var collider = door.GetComponent<Collider2D>();
                if (!collider)
                {
                    Debug.LogWarning($"Unable to locate switch collider for Door {door.doorID}; ignoring");
                    continue;
                }
                seenLevers.Add(door.doorID);

                var pos = collider.transform.position;
                var check = new RandoCheck(CheckType.Lever, sceneId, new Vector2(pos.x, pos.y) + collider.offset, door.doorID);
                check.Alias = "Lever";
                checks.Add(check);
            }

            foreach (var bridge in SceneUtils.FindObjectsOfType<IncineratorBridgeSwitch>())
            {
                if (!IsCorruptModeOnly(bridge.gameObject))
                {
                    var collider = bridge.GetComponent<Collider2D>();
                    if (!collider)
                    {
                        Debug.LogWarning($"Unable to locate switch collider for Door {bridge.doorID}");
                        continue;
                    }
                    var pos = collider.transform.position;
                    var check = new RandoCheck(CheckType.Lever, sceneId, new Vector2(pos.x, pos.y) + collider.offset, bridge.doorID);
                    check.Alias = "Lever";
                    checks.Add(check);
                }
            }

            foreach (var door in SceneUtils.FindObjectsOfType<PistonDoorSwitch>())
            {
                if (!IsCorruptModeOnly(door.gameObject))
                {
                    var collider = door.GetComponent<Collider2D>();
                    if (!collider)
                    {
                        Debug.LogWarning($"Unable to locate switch collider for Door {door.pistonDoorScript.doorID}; ignoring");
                        continue;
                    }

                    var pos = collider.transform.position;
                    var check = new RandoCheck(CheckType.Lever, sceneId, new Vector2(pos.x, pos.y) + collider.offset, door.pistonDoorScript.doorID);
                    check.Alias = "Lever";
                    checks.Add(check);
                }
            }

            // The Steam Town scrap piles all have a pileID of 0, making them indistinguishable
            // from each other. This is likely a vanilla bug.
            if (sceneId != SpecialScenes.SteamTown)
            {
                foreach (var pile in SceneUtils.FindObjectsOfType<SmallMoneyPile>())
                {
                    if (!IsCorruptModeOnly(pile.gameObject))
                    {
                        var check = new RandoCheck(CheckType.MoneyPile, sceneId, pile.transform.position, pile.pileID) { SaveId = MoneyPileValue(pile) };
                        check.Alias = "MoneyPile";
                        checks.Add(check);
                    }
                }
            }
            

            var powerCells = SceneUtils.FindObjectsOfType<PowerCell>();
            for (var i = 0; i < powerCells.Length; i++)
            {
                if (!ignoredChecks.Contains((sceneId, CheckType.PowerCell)))
                {
                    var pickup = powerCells[i];
                    var check = new RandoCheck(CheckType.PowerCell, sceneId, pickup.transform.position, pickup.saveID)
                    {
                        SaveId = pickup.saveID,
                        Alias = powerCells.Length > 1 ? $"PowerCell{i}" : "PowerCell"
                    };
                    checks.Add(check);
                }
            }

            foreach (var pickup in SceneUtils.FindObjectsOfType<TrainTicket>())
            {
                var check = new RandoCheck(CheckType.TrainStation, sceneId, pickup.transform.position, pickup.saveID);
                check.Alias = "TrainStation";
                checks.Add(check);
            }

            foreach (var pickup in SceneUtils.FindObjectsOfType<FixClockAndTrain>())
            {
                var check = new RandoCheck(CheckType.Clock, sceneId, pickup.transform.position, 0);
                check.Alias = "Clock";
                checks.Add(check);
            }

            foreach (var e7Shop in SceneUtils.FindObjectsOfType<e7UpgradeShop>())
            {
                var fireCheck = new RandoCheck(CheckType.FireRes, sceneId, e7Shop.transform.position, 0);
                fireCheck.Alias = "FireRes";
                checks.Add(fireCheck);
                var waterCheck = new RandoCheck(CheckType.WaterRes, sceneId, e7Shop.transform.position, 0);
                waterCheck.Alias = "WaterRes";
                checks.Add(waterCheck);
            }

            foreach (var rusty in SceneUtils.FindObjectsOfType<Rusty>())
            {
                // Ignore duplicate Rusties
                if ((sceneId == 140 && rusty.bank) ||
                    (sceneId == 117 && rusty.vendor) ||
                    rusty.lastEncounter || rusty.isNote)
                {
                    continue;
                }
                var kind = -1;
                if (rusty.health)
                {
                    kind = (int)RustyType.Health;
                }
                else if (rusty.train)
                {
                    kind = (int)RustyType.Train;
                }
                else if (rusty.vendor)
                {
                    kind = (int)RustyType.Vendor;
                }
                else if (rusty.bank)
                {
                    kind = (int)RustyType.Bank;
                }
                else if (rusty.powercell)
                {
                    kind = (int)RustyType.PowerCell;
                }
                if (kind != -1)
                {
                    var check = new RandoCheck(CheckType.MapMarker, sceneId, rusty.transform.position, (int)kind);
                    check.Alias = "MapMarker";
                    checks.Add(check);
                }
                else
                {
                    Debug.Log("unknown Rusty kind");
                }
            }

            int shopItemIndex = 0;
            foreach (var shop in SceneUtils.FindObjectsOfType<ShopTrigger>())
            {
                foreach (var button in shop.shopItems.Select(i => i.GetComponent<IShopItem>()).OfType<ShopItemButton>())
                {
                    CheckType type;
                    int itemId;
                    if (button.chip)
                    {
                        type = CheckType.Chip;
                        itemId = GameManager.instance.getChipNumber(button.chipIdentifier);
                    }
                    else if (button.chipSlot)
                    {
                        type = CheckType.ChipSlot;
                        itemId = button.chipSlotID;
                    }
                    else if (button.marker)
                    {
                        //We ignore pins
                        continue;
                    }
                    else if (button.item)
                    {
                        type = CheckType.Item;
                        itemId = button.itemID;
                    }
                    else
                    {
                        //Unknown type?
                        continue;
                    }

                    var check = new RandoCheck(type, sceneId, shop.transform.position, itemId)
                    {
                        SaveId = button.saveID, 
                        IsShopItem = true,
                        Alias = $"ShopItem{shopItemIndex}"
                    };
                    shopItemIndex++;
                    checks.Add(check);
                }
            }

            //TODO: PinionBirdWhistle?
            //TODO: Parts monument detection
            
            //TODO: Fight-gated checks; these might just fall out naturally due to just being inactive items?

            _allNodes.AddRange(checks);
            return checks;
        }

        // Each hit on a money pile spawns two random coins, except for the last, which spawns four.
        // For valuation purposes, we suppose that all rolls yield the most valuable option possible.
        private static int MoneyPileValue(SmallMoneyPile p) =>
            2 * (p.health + 1) * p.currencies.Select(m => m.GetComponent<Money>().value).Max();

        private static bool IsCorruptModeOnly(GameObject gameObj)
        {
            var corruptEnable = gameObj.GetComponent<EnableIfCorruptMode>();
            return corruptEnable && corruptEnable.setActive;
        }
    }
}
