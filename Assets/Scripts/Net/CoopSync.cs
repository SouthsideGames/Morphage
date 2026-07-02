using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Mutagen.Net
{
    /// <summary>
    /// Stage 2 · strict lockstep transport. Carries per-tick input packets between the two peers with
    /// redundancy: each unreliable packet repeats the last several ticks' inputs, so a dropped packet is
    /// recovered by the next one. Inputs are stored by tick number; the sim (Game.Tick) executes tick T
    /// only once it holds both players' input for T. Lives on the NetworkManager object.
    /// </summary>
    public class CoopSync : MonoBehaviour
    {
        const string InputMsg = "mtick";
        const string ChkMsg = "mchk";
        bool _registered, _started, _callbacksHooked;
        Game _game;

        static readonly Dictionary<int, TickInput> _remoteBuf = new(); // partner inputs, keyed by tick
        static readonly Dictionary<int, ulong> _remoteChecksums = new(); // partner state hashes, keyed by tick
        static int _minAcceptTick;                                     // ignore inputs older than this (already consumed)

        void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || nm.CustomMessagingManager == null) return;

            if (!_callbacksHooked) // once per NetworkManager (it persists across sessions)
            {
                nm.OnClientConnectedCallback += id => Debug.Log($"[MUTAGEN][net] client connected: {id}");
                nm.OnClientDisconnectCallback += id => Debug.LogWarning($"[MUTAGEN][net] client disconnected: {id}");
                _callbacksHooked = true;
            }
            if (!_registered) // once per SESSION — the messaging manager is rebuilt on every new connection
            {
                nm.CustomMessagingManager.RegisterNamedMessageHandler(InputMsg, OnInput);
                nm.CustomMessagingManager.RegisterNamedMessageHandler(ChkMsg, OnChecksum);
                _registered = true;
                Debug.Log("[MUTAGEN][net] channels registered");
            }

            // A host is its own client (IsConnectedClient true immediately) — wait for a REMOTE peer.
            bool connected = nm.IsHost ? nm.ConnectedClientsIds.Count > 1 : nm.IsConnectedClient;
            if (connected && CoopNet.ReadyToStart && !_started)
            {
                _started = true;
                EnsureGame()?.StartCoopRun();
                Debug.Log("[MUTAGEN][net] co-op run started");
            }
        }

        /// <summary>Re-arm for a fresh host/join: clears buffers and lets the auto-start fire again.</summary>
        public static void NewSession()
        {
            ResetBuffer();
            var s = FindFirstObjectByType<CoopSync>();
            if (s != null) { s._started = false; s._registered = false; }
        }

        // ---- remote input buffer (read by Game.Tick) ----
        public static void ResetBuffer() { _remoteBuf.Clear(); _remoteChecksums.Clear(); _minAcceptTick = 0; }
        public static bool TryGetRemote(int tick, out TickInput t) => _remoteBuf.TryGetValue(tick, out t);
        public static void RemoveRemote(int tick) { _remoteBuf.Remove(tick); if (tick + 1 > _minAcceptTick) _minAcceptTick = tick + 1; }

        // ---- desync checksums (reliable; infrequent) ----
        public static bool TryGetRemoteChecksum(int tick, out ulong hash) => _remoteChecksums.TryGetValue(tick, out hash);
        public static void RemoveRemoteChecksum(int tick) => _remoteChecksums.Remove(tick);

        public static void SendChecksum(int tick, ulong hash)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            using var w = new FastBufferWriter(16, Allocator.Temp);
            w.WriteValueSafe(tick);
            w.WriteValueSafe(hash);
            if (nm.IsHost) nm.CustomMessagingManager.SendNamedMessageToAll(ChkMsg, w);   // ReliableSequenced (default)
            else nm.CustomMessagingManager.SendNamedMessage(ChkMsg, NetworkManager.ServerClientId, w);
        }

        void OnChecksum(ulong sender, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && sender == nm.LocalClientId) return; // ignore our own broadcast (host)
            reader.ReadValueSafe(out int tick);
            reader.ReadValueSafe(out ulong hash);
            _remoteChecksums[tick] = hash;
        }

        // ---- send a redundant batch of recent local inputs (unreliable; redundancy covers drops) ----
        public static void SendBatch(List<TickInput> batch)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || batch.Count == 0) return;
            using var w = new FastBufferWriter(2 + batch.Count * 8, Allocator.Temp);
            w.WriteValueSafe((byte)batch.Count);
            for (int i = 0; i < batch.Count; i++)
            {
                var t = batch[i];
                w.WriteValueSafe(t.tick);
                w.WriteValueSafe(t.moveX);
                w.WriteValueSafe(t.moveY);
                w.WriteValueSafe(t.bits);
                w.WriteValueSafe(t.draft);
            }
            if (nm.IsHost) nm.CustomMessagingManager.SendNamedMessageToAll(InputMsg, w, NetworkDelivery.Unreliable);
            else nm.CustomMessagingManager.SendNamedMessage(InputMsg, NetworkManager.ServerClientId, w, NetworkDelivery.Unreliable);
        }

        void OnInput(ulong sender, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && sender == nm.LocalClientId) return; // ignore our own broadcast (host)
            reader.ReadValueSafe(out byte count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int tick);
                reader.ReadValueSafe(out sbyte mx);
                reader.ReadValueSafe(out sbyte my);
                reader.ReadValueSafe(out byte bits);
                reader.ReadValueSafe(out byte draft);
                if (tick >= _minAcceptTick && !_remoteBuf.ContainsKey(tick))
                    _remoteBuf[tick] = new TickInput { tick = tick, moveX = mx, moveY = my, bits = bits, draft = draft };
            }
        }

        Game EnsureGame() => _game != null ? _game : (_game = FindFirstObjectByType<Game>());
    }
}
