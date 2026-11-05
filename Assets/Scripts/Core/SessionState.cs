// ============================================================
// PediatricVR — SessionState.cs
// Shared enums and data transfer objects used across all systems.
// Keep this file dependency-free — no MonoBehaviour, no Unity imports.
// ============================================================

namespace PediatricVR
{
    public enum AppState
    {
        Idle,            // Headset on, waiting for nurse Start command
        SessionActive,   // Child is in the experience
        SessionComplete  // Session ended, fading back to idle
    }

    public enum AgeProfile
    {
        ProfileA = 0,  // Ages 5–7: fully passive, no child input needed
        ProfileB = 1   // Ages 8–10: head-orientation steering, light interaction
    }

    /// <summary>
    /// Matches the JSON sent by the nurse panel:
    /// { "cmd": "start", "profile": "A" }
    /// { "cmd": "stop" }
    /// { "cmd": "reset" }
    /// JsonUtility requires this to be [System.Serializable].
    /// </summary>
    [System.Serializable]
    public class NurseCommand
    {
        public string cmd;      // "start" | "stop" | "reset"
        public string profile;  // "A" | "B" — only used with "start"
    }

    /// <summary>
    /// Outbound acknowledgement sent back to the nurse panel over WebSocket.
    /// </summary>
    [System.Serializable]
    public class ServerAck
    {
        public string status;   // "ok" | "error"
        public string cmd;      // echoes the received command
        public string msg;      // optional detail
        public string state;    // current AppState name
    }
}
