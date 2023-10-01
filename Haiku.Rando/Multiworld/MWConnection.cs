using System;
using Net = System.Net.Sockets;
using Threading = System.Threading;
using Timers = System.Timers;
using Collections = System.Collections.Generic;
using SyncCollections = System.Collections.Concurrent;
using MWLib = MultiWorldLib;
using MWMsg = MultiWorldLib.Messaging;
using MWMsgDef = MultiWorldLib.Messaging.Definitions.Messages;
using UE = UnityEngine;
using RChecks = Haiku.Rando.Checks;
using RTopology = Haiku.Rando.Topology;
using CType = Haiku.Rando.Topology.CheckType;

namespace Haiku.Rando.Multiworld
{
    internal class MWConnection : IDisposable
    {
        private string _serverAddr;
        private Action _onConnectAction;
        private Net.TcpClient _client;
        private Net.NetworkStream _conn;
        private Timers.Timer _pingTimer;
        private MWMsg.MWMessagePacker _packer;
        private Threading.Thread _writeThread, _readThread;
        private SyncCollections.BlockingCollection<Action> _commandQueue;
        private ulong _uid;
        private bool _joinConfirmed;
        private Collections.List<MWMsg.MWMessage> _messagesHeldUntilJoin = new();

        public static MWConnection Current { get; private set; }

        public static void Start()
        {
            Terminate();
            Current = new();
        }

        public static void Join(string serverAddr, int playerId, int randoId, string nickname)
        {
            if (Current == null)
            {
                Current = new();
                Current.Connect(serverAddr, () => Current.Join(playerId, randoId, nickname));
            }
            else if (Current._serverAddr != serverAddr)
            {
                Current.Dispose();
                Current = new();
                Current.Connect(serverAddr, () => Current.Join(playerId, randoId, nickname));
            }
            else
            {
                Current.Join(playerId, randoId, nickname);
            }
        }

        public static void Terminate()
        {
            if (Current != null)
            {
                Current.Dispose();
                Current = null;
            }
        }

        internal static void SendItem(RemoteItem item)
        {
            item.State = RemoteItemState.Collected;
            if (Current == null)
            {
                UE.Debug.Log($"cannot send item {item.Name} to player {item.PlayerId} without connection");
                return;
            }
            Current.SendRemoteItem(item);
        }

        public static void NotifySaved()
        {
            Current?.DoNotifySaved();
        }

        public MWConnection()
        {
            _commandQueue = new(new SyncCollections.ConcurrentQueue<Action>());
            _writeThread = new(WriteLoop);
            _writeThread.Start();
            _packer = new(new MWLib.Binary.BinaryMWMessageEncoder());
        }

        // The group MUST be called exactly this, or the MW server will
        // not shuffle together our items with the other players'.
        private const string SingularGroup = "Main Item Group";

        private void WriteLoop()
        {
            while (true)
            {
                try
                {
                    var cmd = _commandQueue.Take();
                    cmd();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception err)
                {
                    Log(err.ToString());
                }
            }
        }

        private void ReadLoop()
        {
            while (true)
            {
                try
                {
                    var msg = _packer.Unpack(new MWMsg.MWPackedMessage(_conn));
                    switch (msg)
                    {
                        case MWMsgDef.MWConnectMessage connMsg:
                            _commandQueue.Add(() =>
                            {
                                _uid = connMsg.SenderUid;
                                Log($"MW: Connected to {connMsg.ServerName} as UID {_uid}");
                                RandoPlugin.InvokeOnMainThread(rp =>
                                {
                                    rp.ShowMWStatus($"Connected to {connMsg.ServerName}");
                                });
                                _pingTimer = new(PingInterval);
                                _pingTimer.Elapsed += (_, _) => Ping();
                                _pingTimer.AutoReset = true;
                                _pingTimer.Enabled = true;
                                _onConnectAction();
                            });
                            break;
                        case MWMsgDef.MWReadyConfirmMessage rcMsg:
                            _commandQueue.Add(() => Log($"MW: Joined the room with {rcMsg.Ready} players: {string.Join(", ", rcMsg.Names)}"));
                            RandoPlugin.InvokeOnMainThread(rp =>
                            {
                                rp.ShowMWStatus($"Joined to room with {string.Join(", ", rcMsg.Names)}");
                            });
                            break;
                        case MWMsgDef.MWPingMessage:
                            Log("MW: Received a server ping");
                            break;
                        case MWMsgDef.MWRequestRandoMessage:
                            Log("MW: Received request to generate our rando");
                            RandoPlugin.InvokeOnMainThread(rp =>
                            {
                                if (!(Settings.GetGenerationSettings() is {} gs))
                                {
                                    UE.Debug.Log("MW: rando requested, but not enabled on our side");
                                    return;
                                }
                                if (string.IsNullOrWhiteSpace(gs.Seed))
                                {
                                    gs.Seed = DateTime.Now.Ticks.ToString();
                                }
                                rp.ReloadTopology();
                                if (!rp.RetryRandomize(gs, out var _))
                                {
                                    UE.Debug.Log("MW: randomization failed");
                                    return;
                                }
                                SendCheckMapping(rp.Randomizer.CheckMapping, rp.Randomizer.Topology);
                            });
                            break;
                        case MWMsgDef.MWResultMessage resultMsg:
                            RandoPlugin.InvokeOnMainThread(rp =>
                            {
                                var save = rp.InitSaveData(rp.Randomizer.Settings);
                                save.MW = new()
                                {
                                    ServerAddr = _serverAddr,
                                    PlayerId = resultMsg.PlayerId,
                                    RandoId = resultMsg.RandoId,
                                    RemoteItems = new(),
                                    RemoteNicknames = resultMsg.Nicknames,
                                    PatchedPlacements = new()
                                };

                                if (!resultMsg.Placements.TryGetValue(SingularGroup, out var pairs))
                                {
                                    UE.Debug.Log("MW: no placements for group {SingularGroup}");
                                    return;
                                }

                                var allChecks = rp.Randomizer.Topology.Checks;

                                foreach (var (itemName, locName) in pairs)
                                {
                                    var locI = ParseLocalCheckName(locName);
                                    if (!(locI >= 0 && locI < allChecks.Count))
                                    {
                                        UE.Debug.Log($"MW: unknown location in rando result: {locName}");
                                        continue;
                                    }
                                    int itemI;
                                    var (pid, name) = ParseMWItemName(itemName);
                                    if (pid == -1 || pid == save.MW.PlayerId)
                                    {
                                        itemI = ParseLocalCheckName(name);
                                        if (itemI < 0)
                                        {
                                            Log($"MW: unknown local item in rando result: {name}");
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        var i = save.MW.RemoteItems.Count;
                                        save.MW.RemoteItems.Add(new()
                                        {
                                            PlayerId = pid,
                                            Name = name
                                        });
                                        UE.Debug.Log($"MW: Multiworld[{i}] = {name}");
                                        // must start numbering at -1 to avoid
                                        // confusion with local items
                                        itemI = -save.MW.RemoteItems.Count;
                                    }
                                    save.MW.PatchedPlacements.Add(new()
                                    {
                                        LocationIndex = locI,
                                        ItemIndex = itemI
                                    });
                                }
                                rp.ShowMWStatus("Ready to join");
                            });
                            break;
                        case MWMsgDef.MWJoinConfirmMessage:
                            _commandQueue.Add(() =>
                            {
                                _joinConfirmed = true;
                                foreach (var msg in _messagesHeldUntilJoin)
                                {
                                    SendPacked(msg);
                                }
                                _messagesHeldUntilJoin = null;
                            });
                            RandoPlugin.InvokeOnMainThread(rp =>
                            {
                                rp.ResendUnconfirmedItems();
                            });
                            Log("MW: joined");
                            break;
                        case MWMsgDef.MWDataSendConfirmMessage dsConfirmMsg:
                            if (dsConfirmMsg.Label != MWLib.Consts.MULTIWORLD_ITEM_MESSAGE_LABEL)
                            {
                                Log($"MW: data send confirmation has invalid label {dsConfirmMsg.Label}; content={dsConfirmMsg.Content} to={dsConfirmMsg.To}");
                                break;
                            }
                            RandoPlugin.InvokeOnMainThread(rp =>
                            {
                                if (rp.ConfirmRemoteCheck(dsConfirmMsg.Content, dsConfirmMsg.To))
                                {
                                    UE.Debug.Log($"MW: received confirmation of sent item {dsConfirmMsg.Content} for player {dsConfirmMsg.To}");
                                }
                                else
                                {
                                    UE.Debug.Log($"MW: received confirmation for unknown item {dsConfirmMsg.Content} for player {dsConfirmMsg.To}");
                                }
                            });
                            break;
                        case MWMsgDef.MWRequestCharmNotchCostsMessage:
                            RandoPlugin.InvokeOnMainThread(rp =>
                            {
                                var pid = rp.MWPlayerId();
                                _commandQueue.Add(() =>
                                {
                                    SendPacked(new MWMsgDef.MWAnnounceCharmNotchCostsMessage()
                                    {
                                        SenderUid = _uid,
                                        PlayerID = pid,
                                        Costs = new()
                                    });
                                });
                            });
                            break;
                        case MWMsgDef.MWAnnounceCharmNotchCostsMessage notchCostsMsg:
                            Log($"got notch costs for player {notchCostsMsg.PlayerID} but ignoring for now");
                            _commandQueue.Add(() =>
                            {
                                SendPacked(new MWMsgDef.MWConfirmCharmNotchCostsReceivedMessage()
                                {
                                    SenderUid = _uid,
                                    PlayerID = notchCostsMsg.PlayerID
                                });
                            });
                            break;
                        case MWMsgDef.MWDataReceiveMessage recvMsg:
                            if (recvMsg.Label != MWLib.Consts.MULTIWORLD_ITEM_MESSAGE_LABEL)
                            {
                                Log($"MW: received data with unknown label {recvMsg.Label}");
                                break;
                            }
                            var itemI = ParseLocalCheckName(recvMsg.Content);
                            RandoPlugin.InvokeOnMainThread(rp =>
                            {
                                var where = new LocationText()
                                {
                                    Where = recvMsg.From,
                                    ShowInCornerPopup = true
                                };
                                if (!rp.GiveCheck(itemI, where))
                                {
                                    UE.Debug.Log($"MW: received unknown item {recvMsg.Content}");
                                }
                            });
                            _commandQueue.Add(() =>
                            {
                                SendPacked(new MWMsgDef.MWDataReceiveConfirmMessage()
                                {
                                    SenderUid = _uid,
                                    Label = recvMsg.Label,
                                    Data = recvMsg.Content,
                                    From = recvMsg.From
                                });
                            });
                            break;
                        default:
                            Log($"MW: got a {msg.GetType().Name}");
                            break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception err)
                {
                    Log(err.ToString());
                }
            }
        }

        private void ReadMessages(Action<MWMsg.MWMessage> handler)
        {
            while (true)
            {
                try
                {
                    var msg = _packer.Unpack(new MWMsg.MWPackedMessage(_conn));
                    _commandQueue.Add(() => handler(msg));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception err)
                {
                    Log(err.ToString());
                }
            }
        }

        private static (int, string) ParseMWItemName(string name)
        {
            if (!name.StartsWith("MW(")) return (-1, name);
            var i = name.IndexOf(")_");
            if (i == -1) return (-1, name);
            if (int.TryParse(name.Substring(3, i - 3), out var n)) return (n, name.Substring(i + 2));
            return (-1, name);
        }

        private static int ParseLocalCheckName(string name)
        {
            if (!name.EndsWith(")")) return -1;
            var i = name.LastIndexOf('(');
            if (i == -1) return -1;
            if (int.TryParse(name.Substring(i + 1, name.Length - i - 2), out var n)) return n;
            return -1;
        }

        private static int Hash(Collections.IReadOnlyDictionary<RTopology.RandoCheck, RTopology.RandoCheck> mapping)
        {
            var h = 0;
            foreach (var entry in mapping)
            {
                h = AddToHash(h, entry.Key);
                h = AddToHash(h, entry.Value);
            }
            return h;
        }

        private static int AddToHash(int hash, RTopology.RandoCheck check)
        {
            hash = AddToHash(hash, (int)check.Type);
            hash = AddToHash(hash, check.CheckId);
            hash = AddToHash(hash, check.SaveId);
            return hash;
        }
        private static int AddToHash(int hash, int val) => hash * 97 + val;

        public void Connect(string serverAddr, string nickname, string roomName)
        {
            Connect(serverAddr, () => Ready(nickname, roomName));
        }

        public void Connect(string serverAddr, Action onConnect)
        {
            _commandQueue.Add(() =>
            {
                _serverAddr = serverAddr;
                _onConnectAction = onConnect;
                var i = _serverAddr.IndexOf(':');
                if (i != -1 && int.TryParse(_serverAddr.Substring(i + 1), out var port))
                {
                    _client = new(_serverAddr.Substring(0, i), port);
                }
                else
                {
                    _client = new(_serverAddr, MWLib.Consts.DEFAULT_PORT);
                }
                _conn = _client.GetStream();
                SendPacked(new MWMsgDef.MWConnectMessage());
                _readThread = new(ReadLoop);
                _readThread.Start();
            });
        }

        private void Ping()
        {
            _commandQueue.Add(() => SendPacked(new MWMsgDef.MWPingMessage() { SenderUid = _uid }));
        }

        private void DoNotifySaved()
        {
            _commandQueue.Add(() => SendPacked(new MWMsgDef.MWSaveMessage() { SenderUid = _uid }));
        }

        private void Ready(string nickname, string roomName)
        {
            SendPacked(new MWMsgDef.MWReadyMessage()
            {
                SenderUid = _uid,
                Room = roomName,
                Nickname = nickname,
                ReadyMode = MWMsgDef.Mode.MultiWorld,
                ReadyMetadata = new (string, string)[0]
            });
        }

        private void Join(int playerId, int randoId, string nickname)
        {
            _commandQueue.Add(() =>
            {
                if (playerId == -1)
                {
                    Log("MW: trying to join too early");
                    return;
                }
                _joinConfirmed = false;
                SendPacked(new MWMsgDef.MWJoinMessage()
                {
                    SenderUid = _uid,
                    DisplayName = nickname,
                    PlayerId = playerId,
                    RandoId = randoId,
                    Mode = MWMsgDef.Mode.MultiWorld
                });
            });
        }

        internal void StartRandomization()
        {
            _commandQueue.Add(() =>
            {
                SendPacked(new MWMsgDef.MWInitiateGameMessage()
                {
                    SenderUid = _uid,
                    Settings = """{"RandomizationAlgorithm": "Default"}"""
                });
            });
        }

        internal void SendRemoteItem(RemoteItem item)
        {
            var pid = item.PlayerId;
            var name = item.Name;

            _commandQueue.Add(() =>
            {
                var msg = new MWMsgDef.MWDataSendMessage()
                {
                    SenderUid = _uid,
                    Label = MWLib.Consts.MULTIWORLD_ITEM_MESSAGE_LABEL,
                    Content = name,
                    To = pid
                };
                if (_joinConfirmed)
                {
                    SendPacked(msg);
                }
                else
                {
                    _messagesHeldUntilJoin.Add(msg);
                }
            });
        }

        private void ReceiveCheck(RTopology.RandoCheck rc)
        {
            RandoPlugin.InvokeOnMainThread(() =>
            {
                RChecks.CheckManager.TriggerCheck(null, rc);
            });
        }

        private static string ExternalName(RTopology.RandoCheck check)
        {
            var basename = LocalizationSystem.GetLocalizedValue(RChecks.UIDef.NameOf(check)).Replace(' ', '_');
            return $"{basename}_({check.Index})";
        }

        private void SendCheckMapping(Collections.IReadOnlyDictionary<RTopology.RandoCheck, RTopology.RandoCheck> mapping, RTopology.RandoTopology topology)
        {
            var items = new Collections.List<(string, string)>();
            foreach (var entry in mapping)
            {
                if (IsDeletedCheck(entry.Value) || IsDuplicateShopCheck(entry.Key))
                {
                    continue;
                }
                var locName = ExternalName(entry.Key);
                var itemName = ExternalName(entry.Value);
                items.Add((itemName, locName));
            }
            var itemArr = items.ToArray();
            var hash = Hash(mapping);
            _commandQueue.Add(() =>
            {
                SendPacked(new MWMsgDef.MWRandoGeneratedMessage()
                {
                    SenderUid = _uid,
                    Items = new Collections.Dictionary<string, (string, string)[]>()
                    {
                        {SingularGroup, itemArr}
                    },
                    Seed = hash
                });
                Log($"MW: sent {items.Count} item placements to server");
            });
        }

        private static bool IsDeletedCheck(RTopology.RandoCheck check) =>
            check.Type == CType.Filler && check.CheckId > 900000;
        
        private static bool IsDuplicateShopCheck(RTopology.RandoCheck check) =>
            check.SceneId == SpecialScenes.AbandonedWastesStation && check.IsShopItem;

        private const double PingInterval = 5000;

        private void SendPacked(MWMsg.MWMessage msg)
        {
            var packed = _packer.Pack(msg);
            _conn.Write(packed.Buffer, 0, (int)packed.Length);
        }

        public void Dispose()
        {
            _commandQueue.Dispose();
            if (_pingTimer != null)
            {
                _pingTimer.Dispose();
            }
            if (_conn != null)
            {
                _conn.Dispose();
                _client.Dispose();
            }
        }

        private static void Log(string s)
        {
            RandoPlugin.InvokeOnMainThread(() => UE.Debug.Log(s));
        }
    }
}