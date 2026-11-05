// ============================================================
// PediatricVR — AgeProfileManager.cs
//
// Applies per-profile configuration when a session starts.
// Profile A (5–7): Fully passive. No child input. Slow, calm.
// Profile B (8–10): Head-orientation steering. Slightly faster.
//
// GameObject setup:
//   Attach to "GameManager" GameObject.
//   Assign UnderwaterController and HeadOrientationInput references.
// ============================================================

using UnityEngine;

namespace PediatricVR
{
    public class AgeProfileManager : MonoBehaviour
    {
        // ── Profile A Config ──────────────────────────────────
        [Header("Profile A — Ages 5–7 (Passive)")]
        [SerializeField] private float profileA_fishSpeed        = 0.25f;
        [SerializeField] private float profileA_ambientIntensity = 0.55f;
        [SerializeField] private float profileA_musicVolume      = 0.30f;
        [SerializeField] private float profileA_fadeInDuration   = 4f;   // slower, calmer fade

        // ── Profile B Config ──────────────────────────────────
        [Header("Profile B — Ages 8–10 (Light Interaction)")]
        [SerializeField] private float profileB_fishSpeed        = 0.55f;
        [SerializeField] private float profileB_ambientIntensity = 0.80f;
        [SerializeField] private float profileB_musicVolume      = 0.40f;
        [SerializeField] private float profileB_fadeInDuration   = 2.5f;

        // ── References ────────────────────────────────────────
        [Header("Scene References")]
        [SerializeField] private UnderwaterController underwaterController;
        [SerializeField] private HeadOrientationInput headOrientationInput;
        [SerializeField] private AmbientAudioManager  audioManager;
        [Tooltip("Sea turtle — enabled only for Profile B. Must be inactive by default in scene.")]
        [SerializeField] private GameObject           guidedCreature;

        // ══════════════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════════════

        public void ApplyProfile(AgeProfile profile)
        {
            switch (profile)
            {
                case AgeProfile.ProfileA:
                    ApplyProfileA();
                    break;

                case AgeProfile.ProfileB:
                    ApplyProfileB();
                    break;

                default:
                    Debug.LogWarning($"[AgeProfileManager] Unknown profile: {profile} — defaulting to A.");
                    ApplyProfileA();
                    break;
            }

            Debug.Log($"[AgeProfileManager] Profile {profile} applied.");
        }

        // ══════════════════════════════════════════════════════
        // Private
        // ══════════════════════════════════════════════════════

        private void ApplyProfileA()
        {
            if (underwaterController != null)
            {
                underwaterController.SetFishSpeed(profileA_fishSpeed);
                underwaterController.SetAmbientIntensity(profileA_ambientIntensity);
                underwaterController.SetFadeInDuration(profileA_fadeInDuration);
            }

            if (audioManager != null)
                audioManager.SetMusicVolume(profileA_musicVolume);

            if (headOrientationInput != null)
                headOrientationInput.enabled = false;

            // Profile A: passive — no guided creature (turtle)
            if (guidedCreature != null) guidedCreature.SetActive(false);
        }

        private void ApplyProfileB()
        {
            if (underwaterController != null)
            {
                underwaterController.SetFishSpeed(profileB_fishSpeed);
                underwaterController.SetAmbientIntensity(profileB_ambientIntensity);
                underwaterController.SetFadeInDuration(profileB_fadeInDuration);
            }

            if (audioManager != null)
                audioManager.SetMusicVolume(profileB_musicVolume);

            if (headOrientationInput != null)
                headOrientationInput.enabled = true;

            // Profile B: guided — show the sea turtle 4 m in front of the player
            if (guidedCreature != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 fwd = cam.transform.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                    fwd.Normalize();
                    guidedCreature.transform.position = cam.transform.position
                        + fwd * 4f
                        + Vector3.down * 0.5f;
                    guidedCreature.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
                }
                guidedCreature.transform.localScale = Vector3.one * 0.4f;
                guidedCreature.SetActive(true);
            }
        }

        /// <summary>
        /// Resets profile-specific state when returning to idle (called by GameManager).
        /// </summary>
        public void ResetProfile()
        {
            if (guidedCreature != null)      guidedCreature.SetActive(false);
            if (headOrientationInput != null) headOrientationInput.enabled = false;
        }
    }
}
