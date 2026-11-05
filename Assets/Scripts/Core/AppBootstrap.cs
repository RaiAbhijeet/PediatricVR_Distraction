// ============================================================
// PediatricVR — AppBootstrap.cs
// Runs on scene load. Validates that all singleton managers are
// alive and logs the nurse panel URL to console for debugging.
//
// GameObject setup:
//   Attach to the same "GameManager" GameObject.
//   Unity Script Execution Order: set AppBootstrap to run AFTER
//   GameManager (Project Settings → Script Execution Order → add -10).
// ============================================================

using UnityEngine;

namespace PediatricVR
{
    public class AppBootstrap : MonoBehaviour
    {
        [Header("Expected Singletons (leave empty — auto-validated)")]
        [SerializeField] private bool logStartupInfo = true;

        private void Start()
        {
            ValidateSingletons();

            if (logStartupInfo)
                LogStartupInfo();
        }

        private void ValidateSingletons()
        {
            if (GameManager.Instance == null)
                Debug.LogError("[Bootstrap] GameManager.Instance is null — ensure GameManager is in the scene and Awake() runs before Bootstrap.");

            if (WebSocketServerManager.Instance == null)
                Debug.LogError("[Bootstrap] WebSocketServerManager.Instance is null — ensure it is in the scene.");
        }

        private void LogStartupInfo()
        {
            var wsm = WebSocketServerManager.Instance;
            if (wsm == null) return;

            Debug.Log("═══════════════════════════════════════════════");
            Debug.Log("  PediatricVR — Application Started");
            Debug.Log($"  WebSocket endpoint : ws://{wsm.ServerIP}:{wsm.WsPort}/control");
            Debug.Log($"  Nurse panel URL    : http://{wsm.ServerIP}:{wsm.HttpPort}");
            Debug.Log("  Open the nurse panel URL in any mobile browser on the same WiFi.");
            Debug.Log("═══════════════════════════════════════════════");
        }
    }
}
