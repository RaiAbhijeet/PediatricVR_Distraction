// ============================================================
// PediatricVR — AmbientAudioManager.cs
//
// Three-layer audio model:
//   1. Ambient ocean/water — constant background texture
//   2. Bubble layer — mid-frequency detail, spatial presence
//   3. Music layer — gentle melodic background, lowest volume
//
// All layers fade in/out together. Music volume is profile-adjustable.
// No 3D spatial audio needed — all sources are non-directional (2D).
//
// Audio clips to source (royalty-free):
//   freesound.org — search "underwater ambience", "ocean bubbles"
//   Pixabay — search "underwater", "ocean ambient"
//   All AudioSources should be set to Loop = true, Play On Awake = false.
//
// GameObject setup:
//   Create a "AudioManager" empty GameObject.
//   Attach this script.
//   Add three AudioSource components to the same GameObject.
//   Assign them in Inspector.
// ============================================================

using System.Collections;
using UnityEngine;

namespace PediatricVR
{
    public class AmbientAudioManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Audio Sources (set Loop=true, PlayOnAwake=false)")]
        [SerializeField] private AudioSource ambientOceanSource;
        [SerializeField] private AudioSource bubbleSource;
        [SerializeField] private AudioSource musicSource;

        [Header("Target Volumes (at full fade-in)")]
        [SerializeField, Range(0f, 1f)] private float ambientTargetVolume = 0.70f;
        [SerializeField, Range(0f, 1f)] private float bubbleTargetVolume  = 0.40f;
        [SerializeField, Range(0f, 1f)] private float musicTargetVolume   = 0.30f;

        [Header("Fade Timings")]
        [SerializeField] private float fadeInSeconds  = 3f;
        [SerializeField] private float fadeOutSeconds = 2f;

        // ── Private ────────────────────────────────────────────
        private Coroutine _fadeCo;

        // ══════════════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            // Ensure all sources start silent
            SetAllVolumes(0f);
        }

        // ══════════════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════════════

        public void PlayAmbient()
        {
            StartPlayback();
            TriggerFade(fadeIn: true);
        }

        public void StopAmbient()
        {
            TriggerFade(fadeIn: false, stopAfter: true);
        }

        /// <summary>
        /// Immediate silence — no fade. Called by GameManager.ResetToIdle().
        /// </summary>
        public void ForceStop()
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            SetAllVolumes(0f);
            StopAllSources();
        }

        /// <summary>
        /// Called by AgeProfileManager to adjust music level per profile.
        /// Can be called before PlayAmbient() — target volume is stored.
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            musicTargetVolume = Mathf.Clamp01(volume);
            // If currently playing, apply immediately
            if (musicSource != null && musicSource.isPlaying)
                musicSource.volume = musicTargetVolume;
        }

        // ══════════════════════════════════════════════════════
        // Private
        // ══════════════════════════════════════════════════════

        private void StartPlayback()
        {
            PlaySource(ambientOceanSource);
            PlaySource(bubbleSource);
            PlaySource(musicSource);
        }

        private void StopAllSources()
        {
            if (ambientOceanSource != null && ambientOceanSource.isPlaying) ambientOceanSource.Stop();
            if (bubbleSource       != null && bubbleSource.isPlaying)       bubbleSource.Stop();
            if (musicSource        != null && musicSource.isPlaying)        musicSource.Stop();
        }

        private void TriggerFade(bool fadeIn, bool stopAfter = false)
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(FadeRoutine(fadeIn, stopAfter));
        }

        private IEnumerator FadeRoutine(bool fadeIn, bool stopAfterFade)
        {
            float duration  = fadeIn ? fadeInSeconds : fadeOutSeconds;
            float startAlpha = fadeIn ? 0f : 1f;
            float endAlpha   = fadeIn ? 1f : 0f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Ease in/out for less jarring transitions
                float vol = Mathf.SmoothStep(startAlpha, endAlpha, t);
                SetAllVolumes(vol);
                yield return null;
            }

            SetAllVolumes(endAlpha);

            if (stopAfterFade) StopAllSources();
        }

        private void SetAllVolumes(float normalised)
        {
            if (ambientOceanSource != null) ambientOceanSource.volume = normalised * ambientTargetVolume;
            if (bubbleSource       != null) bubbleSource.volume       = normalised * bubbleTargetVolume;
            if (musicSource        != null) musicSource.volume        = normalised * musicTargetVolume;
        }

        private static void PlaySource(AudioSource src)
        {
            if (src != null && !src.isPlaying) src.Play();
        }
    }
}
