// ============================================================
// PediatricVR — IdleScreen.cs
//
// The in-headset idle/waiting screen. Shown when app boots and
// between patients. Displays:
//   - Nurse panel URL (nurse types this into phone browser)
//   - WebSocket connection status
//   - Server ready indicator
//
// GameObject setup:
//   Create a World Space Canvas attached to the XR camera rig.
//   (NOT Overlay — this is VR. World Space Canvas, ~2m in front.)
//   Attach IdleScreen to a child GameObject "IdlePanel".
//   Assign all TextMeshPro references in Inspector.
//
// Unity 6 note: Uses FindFirstObjectByType — not FindObjectOfType.
// ============================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PediatricVR
{
    public class IdleScreen : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Panel Root")]
        [SerializeField] private GameObject idlePanel;

        [Header("Text Elements — assign TextMeshPro components")]
        [SerializeField] private TextMeshProUGUI nurseUrlText;
        [SerializeField] private TextMeshProUGUI wsStatusText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private TextMeshProUGUI appVersionText;

        [Header("Status Indicator")]
        [SerializeField] private Image statusDot;
        [SerializeField] private Color colorReady      = new Color(0f, 1f, 0.5f);
        [SerializeField] private Color colorWaiting    = new Color(1f, 0.8f, 0f);
        [SerializeField] private Color colorError      = new Color(1f, 0.2f, 0.2f);

        [Header("Refresh")]
        [Tooltip("How often (seconds) to refresh server status text.")]
        [SerializeField] private float refreshInterval = 2f;

        // ── Private ────────────────────────────────────────────
        private WebSocketServerManager _wsm;
        private float _refreshTimer;

        // ══════════════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _wsm = FindFirstObjectByType<WebSocketServerManager>();
            LayoutElements();

            if (appVersionText != null)
                appVersionText.text = $"v{Application.version} — PediatricVR";

            if (instructionText != null)
                instructionText.text = "Nurse: open the URL below\nthen select age profile and press Start.";
        }

        // Arrange text elements vertically so they never overlap.
        // Canvas is W=1800 H=1000 (scale 0.001) — coordinate centre = (0,0).
        private void LayoutElements()
        {
            SetupText(instructionText,  y:  300f, w: 1600f, h:  90f, size: 32f);
            SetupText(nurseUrlText,     y:  120f, w: 1600f, h: 130f, size: 60f);
            SetupText(wsStatusText,     y:  -40f, w: 1500f, h:  70f, size: 38f);
            SetupText(appVersionText,   y: -400f, w:  800f, h:  55f, size: 26f);

            if (statusDot != null)
            {
                var rt = statusDot.rectTransform;
                Anchor(rt, new Vector2(0.5f, 0.5f));
                rt.anchoredPosition = new Vector2(-730f, -40f);
                rt.sizeDelta        = new Vector2(36f, 36f);
            }
        }

        private static void SetupText(TextMeshProUGUI tmp, float y, float w, float h, float size)
        {
            if (tmp == null) return;
            tmp.fontSize   = size;
            tmp.alignment  = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            var rt = tmp.rectTransform;
            Anchor(rt, new Vector2(0.5f, 0.5f));
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta        = new Vector2(w, h);
        }

        private static void Anchor(RectTransform rt, Vector2 anchor)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot     = new Vector2(0.5f, 0.5f);
        }

        private void Update()
        {
            if (idlePanel == null || !idlePanel.activeSelf) return;

            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= refreshInterval)
            {
                _refreshTimer = 0f;
                RefreshStatus();
            }
        }

        // ══════════════════════════════════════════════════════
        // Public API — called by GameManager
        // ══════════════════════════════════════════════════════

        public void Show()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = true;
            if (idlePanel != null) idlePanel.SetActive(true);
            RefreshStatus();
        }

        public void Hide()
        {
            if (idlePanel != null) idlePanel.SetActive(false);
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = false;
        }

        // ══════════════════════════════════════════════════════
        // Private
        // ══════════════════════════════════════════════════════

        private void RefreshStatus()
        {
            if (_wsm == null)
            {
                SetStatus("WebSocketServerManager not found", colorError);
                return;
            }

            string ip = _wsm.ServerIP;
            bool   serverUp = _wsm.IsListening;

            if (nurseUrlText != null)
                nurseUrlText.text = $"http://{ip}:{_wsm.HttpPort}";

            if (serverUp)
                SetStatus($"● Server ready  |  ws://{ip}:{_wsm.WsPort}/control", colorReady);
            else
                SetStatus("● Server starting…  check WiFi connection", colorWaiting);
        }

        private void SetStatus(string message, Color color)
        {
            if (wsStatusText != null)
            {
                wsStatusText.text  = message;
                wsStatusText.color = color;
            }
            if (statusDot != null)
                statusDot.color = color;
        }
    }
}
