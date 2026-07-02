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
        const string AutocastKey = "autocast"; // gameplay-affecting setting — must match on both peers
        const string ArenaWKey = "aw", ArenaHKey = "ah"; // arena is screen-fit per device — joiner must adopt the host's
        const string VerKey = "ver";
        public const string GameVer = "1"; // bump whenever a build changes the sim — mixed versions would desync
        const int MaxPlayers = 2;
        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        static async Task EnsureSignedIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // Cleanly leave any previous session + shut down the old NGO connection before starting a new
        // one. Without this, stale state from an earlier co-op game blocks the next run from starting.
        static async Task TeardownPrevious()
        {
            if (Session != null)
            {
                try { await Session.LeaveAsync(); }
                catch (System.Exception e) { Debug.LogWarning("[MUTAGEN][net] leaving old session: " + e.Message); }
                Session = null;
            }
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.ShutdownInProgress))
            {
                nm.Shutdown();
                while (nm.ShutdownInProgress) await Task.Yield();
            }
            CoopSync.NewSession();
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

        /// <summary>Leave the current co-op session (fire-and-forget) — e.g. when returning to the menu.</summary>
        public static async void Leave()
        {
            try { await TeardownPrevious(); Status = ""; ReadyToStart = false; }
            catch (System.Exception e) { Debug.LogWarning("[MUTAGEN][net] leave: " + e.Message); }
        }

        /// <summary>Host a game. Returns the join code to share. Puts the game into co-op as player 0.</summary>
        public static async Task<string> HostAsync(Game game, string seed)
        {
            ReadyToStart = false;
            Status = "Signing in…";
            Debug.Log("[MUTAGEN][net] Host: signing in…");
            await EnsureSignedIn();
            EnsureNetworkManager();
            await TeardownPrevious();

            Status = "Creating game…";
            Debug.Log("[MUTAGEN][net] Host: creating session…");
            // Private: joinable by the share code only — invisible to Quick Match strangers.
            var options = BuildMatchOptions(game, seed, isPrivate: true);

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
            await TeardownPrevious();

            Status = "Joining…";
            Debug.Log($"[MUTAGEN][net] Join: joining by code {code}…");
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            IsHost = false;
            game.localIndex = 1;
            game.coop = true;

            if (!await AdoptHostProperties(game)) return; // version mismatch — refused (Status already set)
            ReadyToStart = true; // localIndex + seed set — safe for CoopSync to auto-start now
            Status = $"Joined · seed {game.seedText}";
            Debug.Log($"[MUTAGEN][net] Joined session {Session.Id}.   seed = {game.seedText}");
        }

        /// <summary>Find a random opponent. Joins an open public game if one exists; otherwise hosts one
        /// and waits. Determines host/joiner role from the resulting session.</summary>
        public static async Task QuickMatchAsync(Game game)
        {
            ReadyToStart = false;
            Status = "Signing in…";
            Debug.Log("[MUTAGEN][net] Quick match: signing in…");
            await EnsureSignedIn();
            EnsureNetworkManager();
            await TeardownPrevious();

            Status = "Finding an opponent…";
            Debug.Log("[MUTAGEN][net] Quick match: searching…");
            // Prepare host-side config up front — used if WE end up creating the session.
            string seed = ((uint)UnityEngine.Random.Range(1, int.MaxValue)).ToString();
            var options = BuildMatchOptions(game, seed, isPrivate: false); // public = discoverable by other quick-matchers
            var quick = new QuickJoinOptions
            {
                CreateSession = true,                        // nobody waiting? host an open game and wait
                Timeout = System.TimeSpan.FromSeconds(6),    // how long to search before becoming the host
                // Never match a different game version — mixed builds would instantly desync.
                Filters = new List<FilterOption> { new FilterOption(FilterField.StringIndex1, GameVer, FilterOperation.Equal) },
            };
            Session = await MultiplayerService.Instance.MatchmakeSessionAsync(quick, options);

            IsHost = Session.IsHost;
            game.coop = true;
            if (IsHost)
            {
                game.localIndex = 0;
                game.seedText = seed;
                ReadyToStart = true;
                Status = "Waiting for an opponent…";
                Debug.Log($"[MUTAGEN][net] Quick match: no open game found — hosting one.   seed = {seed}");
            }
            else
            {
                game.localIndex = 1;
                if (!await AdoptHostProperties(game)) return; // version mismatch — refused
                ReadyToStart = true;
                Status = "Match found!";
                Debug.Log($"[MUTAGEN][net] Quick match: joined {Session.Id}.   seed = {game.seedText}");
            }
        }

        // Session options carrying everything the joiner must copy from the host to stay in sync.
        static SessionOptions BuildMatchOptions(Game game, string seed, bool isPrivate)
        {
            game.autocast = game.ui != null && game.ui.GetAutocast(); // host's settings rule the match
            return new SessionOptions
            {
                MaxPlayers = MaxPlayers,
                IsPrivate = isPrivate,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    { SeedKey, new SessionProperty(seed, VisibilityPropertyOptions.Public) },
                    { AutocastKey, new SessionProperty(game.autocast ? "1" : "0", VisibilityPropertyOptions.Public) },
                    { ArenaWKey, new SessionProperty(game.w.ToString("R", Inv), VisibilityPropertyOptions.Public) },
                    { ArenaHKey, new SessionProperty(game.h.ToString("R", Inv), VisibilityPropertyOptions.Public) },
                    // Indexed so Quick Match can FILTER on it — mismatched versions are never even found.
                    { VerKey, new SessionProperty(GameVer, VisibilityPropertyOptions.Public, PropertyIndex.String1) }
                }
            }.WithRelayNetwork();
        }

        // Copy the host's match config (seed / settings / arena) onto this game. Refuses (leaves the
        // session) if the host runs a different game version — mixed versions are a guaranteed desync.
        static async Task<bool> AdoptHostProperties(Game game)
        {
            var props = Session?.Properties;
            if (props == null) return true;
            if (props.TryGetValue(VerKey, out var v) && v.Value != GameVer)
            {
                Status = "Version mismatch — someone needs to update the game";
                Debug.LogWarning($"[MUTAGEN][net] refused session: host ver {v.Value} vs ours {GameVer}");
                try { await Session.LeaveAsync(); } catch { }
                Session = null;
                return false;
            }
            if (props.TryGetValue(SeedKey, out var p)) game.seedText = p.Value;
            if (props.TryGetValue(AutocastKey, out var ac)) game.autocast = ac.Value == "1";
            if (props.TryGetValue(ArenaWKey, out var aw) && props.TryGetValue(ArenaHKey, out var ah) &&
                float.TryParse(aw.Value, System.Globalization.NumberStyles.Float, Inv, out float arenaW) &&
                float.TryParse(ah.Value, System.Globalization.NumberStyles.Float, Inv, out float arenaH))
                game.ApplyArena(arenaW, arenaH); // simulate the host's exact arena (screen-fit differs per device)
            return true;
        }
    }
}
