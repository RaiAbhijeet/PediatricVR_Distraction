// ============================================================
// PediatricVR — CommandProcessor.cs
//
// Single responsibility: translate NurseCommand objects (from
// WebSocketServerManager) into GameManager method calls.
//
// Keeping this separate means GameManager never imports network
// namespaces, and WebSocketServerManager never imports game logic.
//
// GameObject setup:
//   Attach to the "NetworkManager" GameObject alongside
//   WebSocketServerManager.
// ============================================================

using UnityEngine;

namespace PediatricVR
{
    [RequireComponent(typeof(WebSocketServerManager))]
    public class CommandProcessor : MonoBehaviour
    {
        private WebSocketServerManager _wsManager;

        // ══════════════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _wsManager = GetComponent<WebSocketServerManager>();
        }

        private void OnEnable()
        {
            _wsManager.OnCommandReceived    += HandleCommand;
            _wsManager.OnClientConnected    += HandleClientConnected;
            _wsManager.OnClientDisconnected += HandleClientDisconnected;
        }

        private void OnDisable()
        {
            _wsManager.OnCommandReceived    -= HandleCommand;
            _wsManager.OnClientConnected    -= HandleClientConnected;
            _wsManager.OnClientDisconnected -= HandleClientDisconnected;
        }

        // ══════════════════════════════════════════════════════
        // Command Routing
        // ══════════════════════════════════════════════════════

        private void HandleCommand(NurseCommand command)
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[CommandProcessor] GameManager.Instance is null — cannot process command.");
                return;
            }

            Debug.Log($"[CommandProcessor] Routing command: '{command.cmd}'" +
                      (string.IsNullOrEmpty(command.profile) ? "" : $" | profile: '{command.profile}'"));

            switch (command.cmd.Trim().ToLowerInvariant())
            {
                case "start":
                    AgeProfile profile = ParseProfile(command.profile);
                    GameManager.Instance.StartSession(profile);
                    break;

                case "stop":
                    GameManager.Instance.StopSession();
                    break;

                case "reset":
                    GameManager.Instance.ResetToIdle();
                    break;

                case "ping":
                    // Nurse panel heartbeat — no action needed, ack already sent by ControlBehavior
                    Debug.Log("[CommandProcessor] Ping received from nurse panel.");
                    break;

                default:
                    Debug.LogWarning($"[CommandProcessor] Unknown command: '{command.cmd}' — ignored.");
                    break;
            }
        }

        private void HandleClientConnected(string endPoint)
        {
            Debug.Log($"[CommandProcessor] Nurse panel connected from {endPoint}");
            // Could optionally notify IdleScreen to show "Nurse Connected" indicator
        }

        private void HandleClientDisconnected()
        {
            Debug.Log("[CommandProcessor] Nurse panel disconnected.");
            // If a session is active, do NOT auto-stop — nurse may have briefly lost WiFi
        }

        // ══════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════

        private static AgeProfile ParseProfile(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                Debug.LogWarning("[CommandProcessor] No profile specified in start command — defaulting to Profile A (5–7).");
                return AgeProfile.ProfileA;
            }

            return raw.Trim().ToUpperInvariant() switch
            {
                "A" => AgeProfile.ProfileA,
                "B" => AgeProfile.ProfileB,
                _   => AgeProfile.ProfileA // safe fallback
            };
        }
    }
}
