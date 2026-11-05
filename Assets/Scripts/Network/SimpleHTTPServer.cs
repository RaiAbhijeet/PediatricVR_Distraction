// ============================================================
// PediatricVR — SimpleHTTPServer.cs
//
// A minimal single-file HTTP server running inside the Quest app.
// Serves the nurse-panel.html to any browser on the hospital WiFi.
// Nurse opens: http://[QUEST-IP]:8081
//
// This avoids the need to pre-load the HTML on the nurse's phone.
// It serves exactly ONE file: StreamingAssets/WebUI/nurse-panel.html
//
// Added dynamically at runtime by WebSocketServerManager.StartHTTPServer().
// Do NOT attach this directly in Inspector.
// ============================================================

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace PediatricVR
{
    public class SimpleHTTPServer : MonoBehaviour
    {
        // ── Private ────────────────────────────────────────────
        private TcpListener _listener;
        private Thread       _listenerThread;
        private string       _htmlContent;
        private bool         _isRunning;
        private int          _port;

        // ══════════════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════════════

        public void StartServing(int port, string questIP)
        {
            _port = port;
            string htmlPath = Path.Combine(Application.streamingAssetsPath, "WebUI", "nurse-panel.html");
            StartCoroutine(LoadAndServe(htmlPath, questIP));
        }

        private IEnumerator LoadAndServe(string htmlPath, string questIP)
        {
            // UnityWebRequest handles Android StreamingAssets (jar:file:///android_asset/)
            // File.ReadAllText cannot read from inside the APK on Android.
            using UnityWebRequest req = UnityWebRequest.Get(htmlPath);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[HTTP] Failed to load nurse-panel.html: {req.error}\n" +
                               "Nurse panel will NOT be served. Ensure WebUI/ is in StreamingAssets.");
                yield break;
            }

            _htmlContent = req.downloadHandler.text.Replace("__QUEST_IP_PLACEHOLDER__", questIP);
            _isRunning = true;
            _listenerThread = new Thread(ListenLoop) { IsBackground = true, Name = "PediatricVR_HTTP" };
            _listenerThread.Start();
            Debug.Log($"[HTTP] Nurse panel served at http://{questIP}:{_port}");
        }

        // ══════════════════════════════════════════════════════
        // Background Thread — HTTP listener loop
        // ══════════════════════════════════════════════════════

        private void ListenLoop()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();

                while (_isRunning)
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client))
                    {
                        IsBackground = true,
                        Name = "PediatricVR_HTTP_Client"
                    };
                    clientThread.Start();
                }
            }
            catch (SocketException ex)
            {
                if (_isRunning)
                    Console.WriteLine($"[HTTP] Socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HTTP] Listener error: {ex.Message}");
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();

                // Read the HTTP request — we don't need to parse it, we always serve the same file
                byte[] requestBuffer = new byte[4096];
                int bytesRead = stream.Read(requestBuffer, 0, requestBuffer.Length);
                string request = Encoding.UTF8.GetString(requestBuffer, 0, bytesRead);

                // Respond with the HTML content regardless of path (single-page server)
                byte[] responseBody = Encoding.UTF8.GetBytes(_htmlContent);
                string header =
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/html; charset=utf-8\r\n" +
                    $"Content-Length: {responseBody.Length}\r\n" +
                    "Connection: close\r\n" +
                    "Cache-Control: no-cache\r\n" +
                    "\r\n";

                byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Write(responseBody, 0, responseBody.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HTTP] Client handler error: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        // ══════════════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════════════

        private void OnDestroy()
        {
            _isRunning = false;
            try { _listener?.Stop(); } catch { /* suppress during shutdown */ }
        }
    }
}
