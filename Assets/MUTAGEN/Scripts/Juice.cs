using UnityEngine;
using MoreMountains.Feedbacks;

namespace Mutagen
{
    /// <summary>
    /// Feel (MMFeedbacks) integration layer. Built entirely in code to fit the bootstrap-from-code
    /// architecture. Pass 1: screen shake routed through Feel's MMCameraShaker (the camera is static,
    /// so Feel owns all camera motion — no conflict with game code).
    /// Game.Shake(amount) -> Juice.Shake(amount).
    /// </summary>
    public class Juice : MonoBehaviour
    {
        MMF_Player _shakePlayer;
        MMF_CameraShake _camShake;

        public static Juice Create(Transform parent, Camera cam)
        {
            // The camera must carry an MMCameraShaker (auto-adds the required MMWiggle) to receive shakes.
            if (cam != null && cam.GetComponent<MMCameraShaker>() == null)
                cam.gameObject.AddComponent<MMCameraShaker>();

            var go = new GameObject("Juice");
            go.transform.SetParent(parent, false);
            var j = go.AddComponent<Juice>();

            j._shakePlayer = go.AddComponent<MMF_Player>();
            j._camShake = j._shakePlayer.AddFeedback(typeof(MMF_CameraShake)) as MMF_CameraShake;
            j._shakePlayer.Initialization();
            return j;
        }

        /// <summary>amount mirrors the prototype's shake magnitudes (~4 light hit … 16 boss).</summary>
        public void Shake(float amount)
        {
            if (_camShake == null) return;
            float amp = amount * 0.7f;                 // world units (arena is ~960 wide)
            float dur = 0.12f + amount * 0.012f;
            _camShake.CameraShakeProperties = new MMCameraShakeProperties(dur, amp, 35f);
            _shakePlayer.PlayFeedbacks();
        }
    }
}
