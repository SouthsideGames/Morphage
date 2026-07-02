using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Mutagen.Net
{
    /// <summary>Key state components exchanged with each checksum, so a desync log can say WHICH part diverged.</summary>
    public struct StateSnap
    {
        public uint rng; public int wave, enemies, projectiles;
        public float p0hp, p0xp, p1hp, p1xp; public int p0lvl, p1lvl;

        public static string Diff(StateSnap a, StateSnap b)
        {
            var sb = new System.Text.StringBuilder();
            void D(string name, object l, object r) { if (!l.Equals(r)) sb.AppendLine($"  {name}: local {l} vs peer {r}"); }
            D("rng state", a.rng, b.rng);
            D("wave", a.wave, b.wave);
            D("enemy count", a.enemies, b.enemies);
            D("projectile count", a.projectiles, b.projectiles);
            D("p0 hp", a.p0hp, b.p0hp); D("p0 xp", a.p0xp, b.p0xp); D("p0 level", a.p0lvl, b.p0lvl);
            D("p1 hp", a.p1hp, b.p1hp); D("p1 xp", a.p1xp, b.p1xp); D("p1 level", a.p1lvl, b.p1lvl);
            return sb.Length > 0 ? sb.ToString() : "  core fields match — divergence is in entity positions only";
        }
    }

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
        static readonly Dictionary<int, (ulong hash, StateSnap snap)> _remoteChecksums = new(); // partner state hashes + parts, keyed by tick
        static int _minAcceptTick;                                     // ignore inputs older than this (already consumed)

        void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || nm.CustomMessagingManager == null) return;

            if (!_callbacksHooked) // once per NetworkManager (it persists across sessions)
            {
                nm.OnClientConnectedCallback += id => Debug.Log($"[MUTAGEN][net] client connected: {id}");
                nm.OnClientDisconnectCallback += id =>
                {
                    Debug.LogWarning($"[MUTAGEN][net] client disconnected: {id}");
                    EnsureGame()?.OnPartnerLeft(); // mid-run drop → end the run (no-op unless co-op is playing)
                };
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
        public static bool TryGetRemoteChecksum(int tick, out ulong hash, out StateSnap snap)
        {
            if (_remoteChecksums.TryGetValue(tick, out var v)) { hash = v.hash; snap = v.snap; return true; }
            hash = 0; snap = default; return false;
        }
        public static void RemoveRemoteChecksum(int tick) => _remoteChecksums.Remove(tick);

        public static void SendChecksum(int tick, ulong hash, StateSnap s)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            using var w = new FastBufferWriter(64, Allocator.Temp);
            w.WriteValueSafe(tick);
            w.WriteValueSafe(hash);
            w.WriteValueSafe(s.rng); w.WriteValueSafe(s.wave); w.WriteValueSafe(s.enemies); w.WriteValueSafe(s.projectiles);
            w.WriteValueSafe(s.p0hp); w.WriteValueSafe(s.p0xp); w.WriteValueSafe(s.p0lvl);
            w.WriteValueSafe(s.p1hp); w.WriteValueSafe(s.p1xp); w.WriteValueSafe(s.p1lvl);
            if (nm.IsHost) nm.CustomMessagingManager.SendNamedMessageToAll(ChkMsg, w);   // ReliableSequenced (default)
            else nm.CustomMessagingManager.SendNamedMessage(ChkMsg, NetworkManager.ServerClientId, w);
        }

        void OnChecksum(ulong sender, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && sender == nm.LocalClientId) return; // ignore our own broadcast (host)
            reader.ReadValueSafe(out int tick);
            reader.ReadValueSafe(out ulong hash);
            var s = new StateSnap();
            reader.ReadValueSafe(out s.rng); reader.ReadValueSafe(out s.wave); reader.ReadValueSafe(out s.enemies); reader.ReadValueSafe(out s.projectiles);
            reader.ReadValueSafe(out s.p0hp); reader.ReadValueSafe(out s.p0xp); reader.ReadValueSafe(out s.p0lvl);
            reader.ReadValueSafe(out s.p1hp); reader.ReadValueSafe(out s.p1xp); reader.ReadValueSafe(out s.p1lvl);
            _remoteChecksums[tick] = (hash, s);
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
