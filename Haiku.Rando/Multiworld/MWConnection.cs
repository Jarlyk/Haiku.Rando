using System;
using Net = System.Net.Sockets;
using Threading = System.Threading;
using SyncCollections = System.Collections.Concurrent;
using MWLib = MultiWorldLib;
using MWMsg = MultiWorldLib.Messaging;
using MWMsgDef = MultiWorldLib.Messaging.Definitions.Messages;
using UE = UnityEngine;

namespace Haiku.Rando.Multiworld
{
    internal class MWConnection : IDisposable
    {
        private string _serverAddr;
        private Net.TcpClient _client;
        private Net.NetworkStream _conn;
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
                    if (msg is MWMsgDef.MWConnectMessage connMsg)
                    {
                        _uid = connMsg.SenderUid;
                        Log($"Connected to {connMsg.ServerName} as UID {_uid}");
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

        public void Connect(string serverAddr)
        {
            _commandQueue.Add(() =>
            {
                _serverAddr = serverAddr;
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
                var packed = _packer.Pack(new MWMsgDef.MWConnectMessage());
                _conn.WriteAsync(packed.Buffer, 0, (int)packed.Length);
                _readThread = new(ReadLoop);
                _readThread.Start();
            });
        }

        public void Dispose()
        {
            _commandQueue.Dispose();
            if (_conn != null)
            {
                _conn.Dispose();
                _client.Dispose();
            }
        }

        private static void Log(string s)
        {
            RandoPlugin.MainThreadCallbacks.Enqueue(() => UE.Debug.Log(s));
        }
    }
}