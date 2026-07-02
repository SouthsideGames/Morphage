using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Mutagen.Net
{
    /// <summary>
    /// Stage 1 co-op networking: sign in, then either HOST (create a session and hand back a share
    /// code) or JOIN (by code). The host generates the run seed and stores it on the session; the
    /// joiner reads it back — so both devices agree on the seed before the run starts. Host is
    /// player 0, joiner is player 1. Per-tick input sync (lockstep) is the next stage.
    /// </summary>
    public static class CoopNet
    {
        public static ISession Session { get; private set; }
        public static bool IsHost { get; private set; }
        public static string Status { get; private set; } = "";
        // Gates the auto-start: only true once localIndex + seed are fully configured, so the run can't
        // start mid-connect with a stale (default) localIndex — which would swap the two heroes.
        public static bool ReadyToStart { get; private set; }

        /// <summary>True once the two peers are actually connected (not just in the lobby).</summary>
        public static bool Connected
        {
            get
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsListening) return false;
                // Host is its own client, so require a REMOTE peer for the host; a pure client just needs to be connected.
                return nm.IsHost ? nm.ConnectedClientsIds.Count > 1 : nm.IsConnectedClient;
            }
        }

        const string SeedKey = "seed";
        const int MaxPlayers = 2;

        static async Task EnsureSignedIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // The Sessions relay integration drives NGO's NetworkManager.Singleton (it starts host/client
        // for us). We use NO NetworkObjects — NGO is only the connection + messaging pipe — so a bare
        // NetworkManager + UnityTransport is all it needs. Singleton self-assigns when the component enables.
        static void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return;
            var go = new GameObject("NetworkManager");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var transport = go.AddComponent<UnityTransport>();
            transport.MaxPacketQueueSize = 512; // headroom for the per-tick input stream (default 128)
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            go.AddComponent<CoopSync>(); // Stage 2: exchanges input packets once connected
        }

        /// <summary>Host a game. Returns the join code to share. Puts the game into co-op as player 0.</summary>
        public static async Task<string> HostAsync(Game game, string seed)
        {
            ReadyToStart = false;
            Status = "Signing in…";
            Debug.Log("[MUTAGEN][net] Host: signing in…");
            await EnsureSignedIn();
            EnsureNetworkManager();

            Status = "Creating game…";
            Debug.Log("[MUTAGEN][net] Host: creating session…");
            var options = new SessionOptions
            {
                MaxPlayers = MaxPlayers,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    { SeedKey, new SessionProperty(seed, VisibilityPropertyOptions.Public) }
                }
            }.WithRelayNetwork();

            Session = await MultiplayerService.Instance.CreateSessionAsync(options);
            IsHost = true;
            game.localIndex = 0;
            game.coop = true;
            game.seedText = seed;
            ReadyToStart = true; // fully configured — safe for CoopSync to auto-start now
            Status = $"Hosting · code {Session.Code}";
            Debug.Log($"[MUTAGEN][net] Hosting. Share code = {Session.Code}   seed = {seed}");
            return Session.Code;
        }

        /// <summary>Join a game by its share code. Reads the host's seed. Puts the game into co-op as player 1.</summary>
        public static async Task JoinAsync(Game game, string code)
        {
            ReadyToStart = false;
            Status = "Signing in…";
            Debug.Log("[MUTAGEN][net] Join: signing in…");
            await EnsureSignedIn();
            EnsureNetworkManager();

            Status = "Joining…";
            Debug.Log($"[MUTAGEN][net] Join: joining by code {code}…");
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            IsHost = false;
            game.localIndex = 1;
            game.coop = true;

            string seed = "";
            if (Session.Properties != null && Session.Properties.TryGetValue(SeedKey, out var p)) seed = p.Value;
            game.seedText = seed;
            ReadyToStart = true; // localIndex + seed set — safe for CoopSync to auto-start now
            Status = $"Joined · seed {seed}";
            Debug.Log($"[MUTAGEN][net] Joined session {Session.Id}.   seed = {seed}");
        }
    }
}
