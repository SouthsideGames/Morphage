using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Spawns the Game on play in any scene — no manual scene wiring required.
    /// Remove this and add a Game component to a scene object if you prefer explicit setup.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindFirstObjectByType<Game>() != null) return;
            var go = new GameObject("MORPHAGE");
            go.AddComponent<Game>();
        }
    }
}
