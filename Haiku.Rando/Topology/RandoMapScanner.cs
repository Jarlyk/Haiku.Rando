using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using On.Rewired.UI.ControlMapper;
using Pathfinding;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Haiku.Rando.Topology
{
    public sealed class RandoMapScanner
    {
        private readonly Dictionary<int, RoomScene> _visitedScenes = new Dictionary<int, RoomScene>();
        private readonly Dictionary<string, TransitionNode> _transitionNodes = new Dictionary<string, TransitionNode>();
        private readonly List<IRandoNode> _allNodes = new List<IRandoNode>();
        private readonly List<IRandoEdge> _allEdges = new List<IRandoEdge>();

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
                    //We skip Train transitions, as those are handled as a special case
                    if (outTrans.Type == TransitionType.Train) continue;

                    var outSceneId = outTrans.SceneId1 == currentSceneId ? outTrans.SceneId2 : outTrans.SceneId1;
                    if (!_visitedScenes.ContainsKey(outSceneId) && !pendingScenes.Contains(outSceneId))
                    {
                        pendingScenes.Push(outSceneId);
                    }
                }
            }

            //Post-process transition edges to wire up references
            foreach (var trans in _transitionNodes.Values)
            {
                trans.Scene1 = _visitedScenes[trans.SceneId1];
                trans.Scene2 = _visitedScenes[trans.SceneId2];
            }

            var path = System.IO.Path.Combine(Assembly.GetExecutingAssembly().Location, "..\\Haiku.Rando");
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
        }

        public RoomScene AnalyzeRoom(int sceneId)
        {
            var scene = new RoomScene();
            scene.SceneId = sceneId;

            var transitions = new List<TransitionNode>();

            foreach (var exit in Object.FindObjectsOfType<CapsuleElevator>())
            {
                //NOTE: We implicitly assume only one elevator is allowed per room
                var trans = GetTransition(exit.pointName, TransitionType.CapsuleElevator, sceneId, exit.levelToLoad);
                SetPosition(trans, sceneId, exit.transform.position);
                SetAlias(trans, sceneId, $"{sceneId}E");
                transitions.Add(trans);
            }

            var edgeExits = Object.FindObjectsOfType<LoadNewLevel>();
            foreach (var exit in edgeExits)
            {
                var trans = GetTransition(exit.pointName, TransitionType.Standard, sceneId, exit.levelToLoad);
                SetPosition(trans, sceneId, exit.transform.position);
                transitions.Add(trans);
            }

            var doorExits = Object.FindObjectsOfType<EnterRoomTrigger>();
            for (var i = 0; i < doorExits.Length; i++)
            {
                var exit = doorExits[i];
                var trans = GetTransition(exit.pointName, TransitionType.Standard, sceneId, exit.levelToLoad);
                SetPosition(trans, sceneId, exit.transform.position);
                var doorAlias = doorExits.Length > 1 ? $"{sceneId}P{exit.extraInfo}" : $"{sceneId}P";
                SetAlias(trans, sceneId, doorAlias);
                transitions.Add(trans);
            }

            foreach (var exit in Object.FindObjectsOfType<EnterTrain>())
            {
                var trans = GetTransition("Train", TransitionType.Train, sceneId, SpecialScenes.Train);
                SetPosition(trans, sceneId, exit.transform.position);
                trans.Alias1 = "Train";
                trans.Alias2 = "Train";
                transitions.Add(trans);
            }

            //Create a node for any PlayerStart locations
            var startPoints = Object.FindObjectsOfType<PlayerStartPoint>();
            for (var i = 0; i < startPoints.Length; i++)
            {
                var start = startPoints[i];
                var trans = GetTransition(start.pointName, TransitionType.StartPoint, sceneId, sceneId);
                SetPosition(trans, sceneId, start.transform.position);
                var alias = startPoints.Length > 1 ? $"{sceneId}S{i}" : $"{sceneId}S";
                trans.Alias1 = alias;
                trans.Alias2 = alias;
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
                    var edgeOut = new InRoomEdge(node, otherNode);
                    edgeOut.SceneId = sceneId;
                    node.Outgoing.Add(edgeOut);
                    otherNode.Incoming.Add(edgeOut);
                    scene.Edges.Add(edgeOut);
                    _allEdges.Add(edgeOut);
                }

                //Currently we assume all nodes can reach all checks
                foreach (var check in checks)
                {
                    var edgeCheck = new InRoomEdge(node, check);
                    edgeCheck.SceneId = sceneId;
                    node.Outgoing.Add(edgeCheck);
                    check.Incoming.Add(edgeCheck);
                    scene.Edges.Add(edgeCheck);
                    _allEdges.Add(edgeCheck);
                }
            }

            scene.Nodes.AddRange(transitions);
            scene.Nodes.AddRange(checks);
            return scene;
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

        private List<RandoCheck> FindChecks(int sceneId)
        {
            var checks = new List<RandoCheck>();

            foreach (var pickup in Object.FindObjectsOfType<PickupWrench>())
            {
                var check = new RandoCheck(CheckType.Wrench, sceneId, pickup.transform.position, 0);
                checks.Add(check);
            }

            foreach (var pickup in Object.FindObjectsOfType<UnlockTutorial>())
            {
                var check = new RandoCheck(CheckType.Ability, sceneId, pickup.transform.position, pickup.abilityID);
                checks.Add(check);
            }

            foreach (var pickup in Object.FindObjectsOfType<PickupItem>())
            {
                CheckType type;
                int itemId;
                if (pickup.triggerChip)
                {
                    type = CheckType.Chip;
                    itemId = GameManager.instance.getChipNumber(pickup.chipIdentifier);
                }
                else if (pickup.triggerChipSlot)
                {
                    type = CheckType.ChipSlot;
                    itemId = pickup.chipSlotNumber;
                }
                else if (pickup.triggerCoolant)
                {
                    type = CheckType.Coolant;
                    itemId = 0;
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
                }
                var check = new RandoCheck(type, sceneId, pickup.transform.position, itemId) { SaveId = pickup.saveID };
                checks.Add(check);
            }

            foreach (var pickup in Object.FindObjectsOfType<Disruptor>())
            {
                var check = new RandoCheck(CheckType.MapDisruptor, sceneId, pickup.transform.position, pickup.disruptorID);
                checks.Add(check);
            }

            foreach (var door in Object.FindObjectsOfType<SwitchDoor>())
            {
                var check = new RandoCheck(CheckType.Lever, sceneId, door.switchCollider.transform.position, door.doorID);
                checks.Add(check);
            }

            foreach (var pickup in Object.FindObjectsOfType<PowerCell>())
            {
                var check = new RandoCheck(CheckType.PowerCell, sceneId, pickup.transform.position, pickup.saveID) { SaveId = pickup.saveID };
                checks.Add(check);
            }

            foreach (var e7Shop in Object.FindObjectsOfType<e7UpgradeShop>())
            {
                var fireCheck = new RandoCheck(CheckType.FireRes, sceneId, e7Shop.transform.position, 0);
                checks.Add(fireCheck);
                var waterCheck = new RandoCheck(CheckType.WaterRes, sceneId, e7Shop.transform.position, 0);
                checks.Add(waterCheck);
            }
            foreach (var shop in Object.FindObjectsOfType<ShopTrigger>())
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
                    else if (button.powercell)
                    {
                        type = CheckType.PowerCell;
                        itemId = button.saveID;
                    }
                    else
                    {
                        //Unknown type?
                        continue;
                    }

                    var check = new RandoCheck(type, sceneId, shop.transform.position, itemId)
                    {
                        SaveId = button.saveID, 
                        IsShopItem = true
                    };
                    checks.Add(check);
                }
            }

            //TODO: PinionBirdWhistle?
            //TODO: Parts monument detection
            //TODO: Lore check detection
            
            //TODO: Fight-gated checks; these might just fall out naturally due to just being inactive items?

            _allNodes.AddRange(checks);
            return checks;
        }
    }
}
