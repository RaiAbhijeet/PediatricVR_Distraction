// ============================================================
// PediatricVR — GameManager.cs
// Central state machine. Single source of truth for AppState.
//
// GameObject setup:
//   Create an empty GameObject "GameManager" in your scene.
//   Attach this script + drag all [SerializeField] references in.
//   This is a singleton — survives scene loads (DontDestroyOnLoad).
// ============================================================

using System;
using System.Collections;
using UnityEngine;

namespace PediatricVR
{
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── State (read-only externally) ───────────────────────
        public AppState CurrentState   { get; private set; } = AppState.Idle;
        public AgeProfile CurrentProfile { get; private set; } = AgeProfile.ProfileA;

        // ── Events (other systems subscribe rather than polling) ─
        public event Action<AppState>   OnStateChanged;
        public event Action<AgeProfile> OnProfileChanged;

        // ── Inspector ─────────────────────────────────────────
        [Header("Session Config")]
        [Tooltip("Auto-stop after this many minutes. Prevents infinite sessions if nurse forgets.")]
        [SerializeField] private float maxSessionMinutes = 15f;

        [Header("Scene References — assign in Inspector")]
        [SerializeField] private IdleScreen         idleScreen;
        [SerializeField] private UnderwaterController underwaterController;
        [SerializeField] private AgeProfileManager  profileManager;
        [SerializeField] private AmbientAudioManager audioManager;

        // ── Private ────────────────────────────────────────────
        private Coroutine _sessionTimerCo;

        // ══════════════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ValidateReferences();
            TransitionToIdle();
        }

        // ══════════════════════════════════════════════════════
        // Public API — called by CommandProcessor
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Start a session with the given age profile.
        /// Safe to call from any state — will log and return if already active.
        /// </summary>
        public void StartSession(AgeProfile profile)
        {
            if (CurrentState == AppState.SessionActive)
            {
                Debug.LogWarning("[GameManager] StartSession called while session already active — ignored.");
                return;
            }

            CurrentProfile = profile;
            SetState(AppState.SessionActive);
            OnProfileChanged?.Invoke(profile);

            idleScreen.Hide();
            profileManager.ApplyProfile(profile);
            underwaterController.StartExperience(profile);
            audioManager.PlayAmbient();

            RestartSessionTimer();

            Debug.Log($"[GameManager] Session STARTED — Profile: {profile}");
        }

        /// <summary>
        /// End the active session gracefully. Fades out, then returns to idle.
        /// </summary>
        public void StopSession()
        {
            if (CurrentState != AppState.SessionActive)
            {
                Debug.LogWarning("[GameManager] StopSession called but no session is active — ignored.");
                return;
            }

            CancelSessionTimer();
            SetState(AppState.SessionComplete);

            underwaterController.StopExperience();
            audioManager.StopAmbient();

            StartCoroutine(ReturnToIdleAfter(0.5f));
            Debug.Log("[GameManager] Session STOPPED.");
        }

        /// <summary>
        /// Hard reset to idle — for nurse pressing Reset between patients.
        /// Immediately cancels everything without graceful fade.
        /// </summary>
        public void ResetToIdle()
        {
            CancelSessionTimer();
            underwaterController.ForceStop();
            audioManager.ForceStop();
            TransitionToIdle();
            Debug.Log("[GameManager] RESET to idle.");
        }

        // ══════════════════════════════════════════════════════
        // Private Helpers
        // ══════════════════════════════════════════════════════

        private void TransitionToIdle()
        {
            SetState(AppState.Idle);
            profileManager?.ResetProfile();         // deactivates GuidedCreature / head input
            underwaterController?.RevealIdleScreen();
            idleScreen?.Show();
        }

        private void SetState(AppState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        private void RestartSessionTimer()
        {
            if (_sessionTimerCo != null) StopCoroutine(_sessionTimerCo);
            _sessionTimerCo = StartCoroutine(SessionTimeoutRoutine());
        }

        private void CancelSessionTimer()
        {
            if (_sessionTimerCo != null)
            {
                StopCoroutine(_sessionTimerCo);
                _sessionTimerCo = null;
            }
        }

        private IEnumerator SessionTimeoutRoutine()
        {
            yield return new WaitForSeconds(maxSessionMinutes * 60f);
            Debug.Log($"[GameManager] Session auto-timeout after {maxSessionMinutes} min.");
            StopSession();
        }

        private IEnumerator ReturnToIdleAfter(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            TransitionToIdle();
        }

        private void ValidateReferences()
        {
            if (idleScreen == null)          Debug.LogError("[GameManager] idleScreen is not assigned.");
            if (underwaterController == null) Debug.LogError("[GameManager] underwaterController is not assigned.");
            if (profileManager == null)       Debug.LogError("[GameManager] profileManager is not assigned.");
            if (audioManager == null)         Debug.LogError("[GameManager] audioManager is not assigned.");
        }
    }
}
