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
            Log("Started read loop");
            while (true)
            {
                try
                {
                    var msg = _packer.Unpack(new MWMsg.MWPackedMessage(_conn));
                    switch (msg)
                    {
                        case MWMsgDef.MWConnectMessage connMsg:
                            _uid = connMsg.SenderUid;
                            Log($"MW: Connected to {connMsg.ServerName} as UID {_uid}");
                            _pingTimer = new(PingInterval);
                            _pingTimer.Elapsed += (_, _) => Ping();
                            _pingTimer.AutoReset = true;
                            _pingTimer.Enabled = true;
                            Ready();
                            break;
                        case MWMsgDef.MWReadyConfirmMessage rcMsg:
                            Log($"MW: Joined the room {_roomName} with {rcMsg.Ready} players: {string.Join(", ", rcMsg.Names)}");
                            break;
                        case MWMsgDef.MWPingMessage:
                            Log("Received a server ping");
                            break;
                        case MWMsgDef.MWRequestRandoMessage:
                            Log("Received request to generate our rando");
                            RandoPlugin.InvokeOnMainThread(rp =>
                            {
                                UE.Debug.Log("MW: randomizing in main thread");
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
                        default:
                            Log($"got a {msg.GetType().Name}");
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
            _commandQueue.Add(() => SendPacked(new MWMsgDef.MWReadyMessage()
            {
                SenderUid = _uid,
                Room = _roomName,
                Nickname = _nickname,
                ReadyMode = MWMsgDef.Mode.MultiWorld
            }));
        }

        private void SendCheckMapping(Collections.IReadOnlyDictionary<RTopology.RandoCheck, RTopology.RandoCheck> mapping)
        {
            var items = new Collections.List<(string, string)>();
            foreach (var entry in mapping)
            {
                // TODO: exclude duplicate shop locations
                if (entry.Value.Type == CType.Filler && entry.Value.CheckId > 900000)
                {
                    continue;
                }
                var locName = ExternalName(entry.Key, items.Count);
                var itemName = ExternalName(entry.Value, items.Count);
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
                        // The group MUST be called exactly this, or the MW server will
                        // not shuffle together our items with the other players'.
                        {"Main Item Group", itemArr}
                    },
                    Seed = hash
                });
                Log($"MW: sent {items.Count} item placements to server");
            });
        }

        private const double PingInterval = 5000;

        private void SendPacked(MWMsg.MWMessage msg)
        {
            var packed = _packer.Pack(msg);
            _conn.Write(packed.Buffer, 0, (int)packed.Length);
        }

        public void Dispose()
        {
            _commandQueue.Dispose();
            _pingTimer.Dispose();
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