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
        private string _nickname;
        private string _roomName;
        private Net.TcpClient _client;
        private Net.NetworkStream _conn;
        private Timers.Timer _pingTimer;
        private MWMsg.MWMessagePacker _packer;
        private Threading.Thread _writeThread, _readThread;
        private SyncCollections.BlockingCollection<Action> _commandQueue;
        private ulong _uid;
        private bool _joinConfirmed;
        private int _playerId = -1;
        private int _randoId = -1;
        private SyncCollections.ConcurrentDictionary<string, RTopology.RandoCheck> _itemsByName;
        private SyncCollections.ConcurrentDictionary<string, RTopology.RandoCheck> _locationsByName;
        private Collections.List<RemoteItem> _remoteItems;
        private string[] _remoteNicknames;

        private struct RemoteItem
        {
            public int PlayerId;
            public string Name;
        }

        public static MWConnection Current { get; private set; }

        public static void Start()
        {
            Terminate();
            Current = new();
        }

        public static void Terminate()
        {
            if (Current != null)
            {
                Current.Dispose();
                Current = null;
            }
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
                                _pingTimer = new(PingInterval);
                                _pingTimer.Elapsed += (_, _) => Ping();
                                _pingTimer.AutoReset = true;
                                _pingTimer.Enabled = true;
                                Ready();
                            });
                            break;
                        case MWMsgDef.MWReadyConfirmMessage rcMsg:
                            _commandQueue.Add(() => Log($"MW: Joined the room {_roomName} with {rcMsg.Ready} players: {string.Join(", ", rcMsg.Names)}"));
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
                                SendCheckMapping(rp.Randomizer.CheckMapping);
                            });
                            break;
                        case MWMsgDef.MWResultMessage resultMsg:
                            if (_itemsByName == null)
                            {
                                Log("MW: received result too early");
                                break;
                            }
                            _commandQueue.Add(() =>
                            {
                                _playerId = resultMsg.PlayerId;
                                _randoId = resultMsg.RandoId;
                                _remoteNicknames = resultMsg.Nicknames;
                                if (!resultMsg.Placements.TryGetValue(SingularGroup, out var pairs))
                                {
                                    Log("MW: no placements for group {SingularGroup}");
                                    return;
                                }
                                foreach (var (itemName, locName) in pairs)
                                {
                                    if (!_locationsByName.TryGetValue(locName, out var loc))
                                    {
                                        Log($"MW: unknown location in rando result: {locName}");
                                        continue;
                                    }
                                    RTopology.RandoCheck c;
                                    var (pid, name) = ParseMWItemName(itemName);
                                    if (pid == -1)
                                    {
                                        if (!_itemsByName.TryGetValue(name, out c))
                                        {
                                            Log($"MW: unknown item in rando result: {name}");
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        _remoteItems = new();
                                        var i = _remoteItems.Count;
                                        _remoteItems.Add(new()
                                        {
                                            PlayerId = pid,
                                            Name = name
                                        });
                                        c = new(CType.Multiworld, 0, new(0, 0), i);
                                    }
                                    RandoPlugin.InvokeOnMainThread(rp =>
                                    {
                                        rp.Randomizer.SetCheckMapping(loc, c);
                                    });
                                }
                                Join();
                            });
                            break;
                        case MWMsgDef.MWJoinConfirmMessage:
                            _commandQueue.Add(() =>
                            {
                                _joinConfirmed = true;
                            });
                            RandoPlugin.InvokeOnMainThread(rp =>
                            {
                                rp.AltBeginRando = rp.GiveStartingState;
                                UE.Debug.Log("MW: joined and ready to start");
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

        private static string ExternalName(RTopology.RandoCheck check, int uniqueIndex)
        {
            var basename = LocalizationSystem.GetLocalizedValue(RChecks.UIDef.NameOf(check)).Replace(' ', '_');
            return $"{basename}_({uniqueIndex})";
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
            _commandQueue.Add(() =>
            {
                _serverAddr = serverAddr;
                _nickname = nickname;
                _roomName = roomName;
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

        private void Ready()
        {
            SendPacked(new MWMsgDef.MWReadyMessage()
            {
                SenderUid = _uid,
                Room = _roomName,
                Nickname = _nickname,
                ReadyMode = MWMsgDef.Mode.MultiWorld,
                ReadyMetadata = new (string, string)[0]
            });
        }

        public void Join()
        {
            _commandQueue.Add(() =>
            {
                if (_playerId == -1)
                {
                    Log("MW: trying to join too early");
                    return;
                }
                SendPacked(new MWMsgDef.MWJoinMessage()
                {
                    SenderUid = _uid,
                    DisplayName = _nickname,
                    PlayerId = _playerId,
                    RandoId = _randoId,
                    Mode = MWMsgDef.Mode.MultiWorld
                });
            });
        }

        private void SendCheckMapping(Collections.IReadOnlyDictionary<RTopology.RandoCheck, RTopology.RandoCheck> mapping)
        {
            var locationsByName = new SyncCollections.ConcurrentDictionary<string, RTopology.RandoCheck>();
            var itemsByName = new SyncCollections.ConcurrentDictionary<string, RTopology.RandoCheck>();
            var items = new Collections.List<(string, string)>();
            foreach (var entry in mapping)
            {
                if (IsDeletedCheck(entry.Value) || IsDuplicateShopCheck(entry.Key))
                {
                    continue;
                }
                var locName = ExternalName(entry.Key, items.Count);
                var itemName = ExternalName(entry.Value, items.Count);
                locationsByName[locName] = entry.Key;
                itemsByName[itemName] = entry.Value;
                items.Add((itemName, locName));
            }
            var itemArr = items.ToArray();
            var hash = Hash(mapping);
            _commandQueue.Add(() =>
            {
                _locationsByName = locationsByName;
                _itemsByName = itemsByName;
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