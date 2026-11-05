// ============================================================
// PediatricVR — WebSocketServerManager.cs
//
// Runs a WebSocket server INSIDE the Quest app.
// Nurse connects from phone browser: ws://[QUEST-IP]:8080/control
//
// THREADING MODEL — critical to understand:
//   Background network threads enqueue raw JSON strings.
//   Update() drains queue on main thread and parses there.
//
// DEPENDENCY: none — built-in minimal WebSocket server implementation.
//
// GameObject setup:
//   Create empty GameObject "NetworkManager".
//   Attach WebSocketServerManager + CommandProcessor to it.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace PediatricVR
{
    public class WebSocketServerManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────
        public static WebSocketServerManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────
        [Header("Server Ports")]
        [Tooltip("WebSocket port — nurse panel connects here.")]
        [SerializeField] private int wsPort  = 8080;
        [Tooltip("HTTP port — serves the nurse panel HTML page.")]
        [SerializeField] private int httpPort = 8081;

        [Header("WebSocket Endpoint")]
        [SerializeField] private string wsEndpoint = "/control";

        // ── Public Properties ─────────────────────────────────
        public string ServerIP   { get; private set; } = "Detecting...";
        public int    WsPort     => wsPort;
        public int    HttpPort   => httpPort;
        public bool   IsListening => _listenerRunning;

        // ── Events (fired on main thread) ─────────────────────
        public event Action<NurseCommand> OnCommandReceived;
        public event Action<string>       OnClientConnected;
        public event Action               OnClientDisconnected;

        // ── Private ────────────────────────────────────────────
        private TcpListener _tcpListener;
        private Thread _listenerThread;
        private bool _listenerRunning;
        private readonly List<WebSocketClient> _clients = new List<WebSocketClient>();
        private SimpleHTTPServer _httpServer;

        // Thread-safe raw JSON queue — background → main thread
        private static readonly Queue<string>  _rawQueue  = new Queue<string>();
        private static readonly object         _queueLock = new object();

        // Track connected clients count
        private static int _connectedClients = 0;

        // ══════════════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ServerIP = GetLocalIPv4();
            StartWebSocketServer();
            StartHTTPServer();
        }

        /// <summary>
        /// Drain the raw JSON queue on the Unity main thread.
        /// JSON parsing and event firing happen here — Unity API safe.
        /// </summary>
        private void Update()
        {
            lock (_queueLock)
            {
                while (_rawQueue.Count > 0)
                {
                    string json = _rawQueue.Dequeue();
                    ProcessRawJson(json);
                }
            }
        }

        private void OnDestroy()
        {
            StopServers();
        }

        private void OnApplicationQuit()
        {
            StopServers();
        }

        // ══════════════════════════════════════════════════════
        // Server Startup / Shutdown
        // ══════════════════════════════════════════════════════

        private void StartWebSocketServer()
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, wsPort);
                _tcpListener.Start();
                _listenerRunning = true;
                _listenerThread = new Thread(WebSocketAcceptLoop) { IsBackground = true };
                _listenerThread.Start();
                Debug.Log($"[WSS] WebSocket server listening on ws://{ServerIP}:{wsPort}{wsEndpoint}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WSS] Failed to start WebSocket server: {ex.Message}\n" +
                               "Check that another process isn't using port " + wsPort);
            }
        }

        private void StartHTTPServer()
        {
            _httpServer = gameObject.AddComponent<SimpleHTTPServer>();
            _httpServer.StartServing(httpPort, ServerIP);
        }

        private void StopServers()
        {
            _listenerRunning = false;

            try { _tcpListener?.Stop(); }
            catch (Exception ex) { Debug.LogWarning($"[WSS] Error stopping WS listener: {ex.Message}"); }

            lock (_clients)
            {
                foreach (var client in _clients)
                {
                    client.Stop();
                }
                _clients.Clear();
            }

            try { _listenerThread?.Join(500); }
            catch { }
        }

        private void WebSocketAcceptLoop()
        {
            while (_listenerRunning)
            {
                try
                {
                    TcpClient tcpClient = _tcpListener.AcceptTcpClient();
                    var client = new WebSocketClient(tcpClient, wsEndpoint, this);
                    lock (_clients)
                    {
                        _clients.Add(client);
                    }
                    client.Start();
                }
                catch (SocketException)
                {
                    if (!_listenerRunning) break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WSS] Accept loop error: {ex.Message}");
                }
            }
        }

        private void RemoveClient(WebSocketClient client)
        {
            lock (_clients)
            {
                _clients.Remove(client);
            }
        }

        // ══════════════════════════════════════════════════════
        // Internal — called from WebSocket background threads
        // ══════════════════════════════════════════════════════

        internal static void EnqueueRawJson(string json)
        {
            lock (_queueLock)
            {
                _rawQueue.Enqueue(json);
            }
        }

        internal static void NotifyClientConnected(string endPoint)
        {
            lock (_queueLock)
            {
                _rawQueue.Enqueue($"__CONNECTED__{endPoint}");
                _connectedClients++;
            }
        }

        internal static void NotifyClientDisconnected()
        {
            lock (_queueLock)
            {
                _connectedClients = Mathf.Max(0, _connectedClients - 1);
                _rawQueue.Enqueue("__DISCONNECTED__");
            }
        }

        // ══════════════════════════════════════════════════════
        // Main-Thread Processing
        // ══════════════════════════════════════════════════════

        private void ProcessRawJson(string raw)
        {
            if (raw.StartsWith("__CONNECTED__"))
            {
                string ep = raw.Replace("__CONNECTED__", "");
                Debug.Log($"[WSS] Nurse client connected from {ep}");
                OnClientConnected?.Invoke(ep);
                return;
            }
            if (raw == "__DISCONNECTED__")
            {
                Debug.Log("[WSS] Nurse client disconnected.");
                OnClientDisconnected?.Invoke();
                return;
            }

            try
            {
                NurseCommand cmd = JsonUtility.FromJson<NurseCommand>(raw);
                if (cmd == null || string.IsNullOrEmpty(cmd.cmd))
                {
                    Debug.LogWarning($"[WSS] Received unparseable JSON: {raw}");
                    return;
                }
                OnCommandReceived?.Invoke(cmd);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WSS] JSON parse error: {ex.Message} | Raw: {raw}");
            }
        }

        // ══════════════════════════════════════════════════════
        // Network Utilities
        // ══════════════════════════════════════════════════════

        private static string GetLocalIPv4()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)socket.LocalEndPoint)?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private class WebSocketClient
        {
            private readonly TcpClient _tcpClient;
            private readonly string _endpoint;
            private readonly WebSocketServerManager _manager;
            private Thread _thread;
            private bool _connected;

            public WebSocketClient(TcpClient tcpClient, string endpoint, WebSocketServerManager manager)
            {
                _tcpClient = tcpClient;
                _endpoint = endpoint;
                _manager = manager;
            }

            public void Start()
            {
                _thread = new Thread(Run) { IsBackground = true };
                _thread.Start();
            }

            public void Stop()
            {
                try
                {
                    _tcpClient.Close();
                }
                catch { }
            }

            private void Run()
            {
                try
                {
                    using NetworkStream stream = _tcpClient.GetStream();
                    if (!PerformHandshake(stream)) return;

                    _connected = true;
                    WebSocketServerManager.NotifyClientConnected(_tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown");
                    SendText(stream, "{\"status\":\"connected\",\"app\":\"PediatricVR\",\"version\":\"1.0.0\"}");

                    while (_tcpClient.Connected)
                    {
                        string message = ReceiveText(stream);
                        if (message == null) break;

                        WebSocketServerManager.EnqueueRawJson(message);
                        SendText(stream, "{\"status\":\"received\"}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WSS] Client error: {ex.Message}");
                }
                finally
                {
                    if (_connected)
                    {
                        WebSocketServerManager.NotifyClientDisconnected();
                    }
                    _manager.RemoveClient(this);
                }
            }

            private bool PerformHandshake(NetworkStream stream)
            {
                string request = ReadHttpRequest(stream);
                if (string.IsNullOrEmpty(request)) return false;

                string[] lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0) return false;

                string[] requestParts = lines[0].Split(' ');
                if (requestParts.Length < 2 || requestParts[0] != "GET" || requestParts[1] != _endpoint)
                {
                    return false;
                }

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int colonIndex = line.IndexOf(':');
                    if (colonIndex <= 0) continue;
                    string name = line.Substring(0, colonIndex).Trim();
                    string value = line.Substring(colonIndex + 1).Trim();
                    headers[name] = value;
                }

                if (!headers.TryGetValue("Upgrade", out string upgrade) ||
                    !upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!headers.TryGetValue("Connection", out string connection) ||
                    !connection.ToLowerInvariant().Contains("upgrade"))
                {
                    return false;
                }

                if (!headers.TryGetValue("Sec-WebSocket-Key", out string secKey))
                {
                    return false;
                }

                string acceptKey = ComputeWebSocketAcceptKey(secKey);
                string response =
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

                byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                stream.Write(responseBytes, 0, responseBytes.Length);
                stream.Flush();
                return true;
            }

            private static string ReadHttpRequest(NetworkStream stream)
            {
                using var memory = new MemoryStream();
                byte[] buffer = new byte[1];

                while (true)
                {
                    int read = stream.Read(buffer, 0, 1);
                    if (read <= 0) return null;
                    memory.WriteByte(buffer[0]);

                    if (memory.Length >= 4)
                    {
                        byte[] tail = memory.ToArray();
                        int len = tail.Length;
                        if (tail[len - 4] == '\r' && tail[len - 3] == '\n' &&
                            tail[len - 2] == '\r' && tail[len - 1] == '\n')
                        {
                            break;
                        }
                    }

                    if (memory.Length > 16384) return null;
                }

                return Encoding.UTF8.GetString(memory.ToArray());
            }

            private static string ComputeWebSocketAcceptKey(string secKey)
            {
                const string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                string combined = secKey.Trim() + guid;
                byte[] hash;
                using (SHA1 sha1 = SHA1.Create())
                {
                    hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(combined));
                }
                return Convert.ToBase64String(hash);
            }

            private static string ReceiveText(NetworkStream stream)
            {
                int b1 = stream.ReadByte();
                if (b1 < 0) return null;
                int b2 = stream.ReadByte();
                if (b2 < 0) return null;

                int opcode = b1 & 0x0F;
                if (opcode == 0x8) return null;
                if (opcode != 0x1) return null;

                bool masked = (b2 & 0x80) != 0;
                int payloadLen = b2 & 0x7F;

                if (payloadLen == 126)
                {
                    byte[] extended = ReadExactly(stream, 2);
                    if (extended == null) return null;
                    payloadLen = (extended[0] << 8) | extended[1];
                }
                else if (payloadLen == 127)
                {
                    byte[] extended = ReadExactly(stream, 8);
                    if (extended == null) return null;
                    if (BitConverter.IsLittleEndian) Array.Reverse(extended);
                    ulong len = BitConverter.ToUInt64(extended, 0);
                    if (len > int.MaxValue) return null;
                    payloadLen = (int)len;
                }

                byte[] maskKey = masked ? ReadExactly(stream, 4) : null;
                if (masked && maskKey == null) return null;

                byte[] payloadData = ReadExactly(stream, payloadLen);
                if (payloadData == null) return null;

                if (masked)
                {
                    for (int i = 0; i < payloadData.Length; i++)
                    {
                        payloadData[i] ^= maskKey[i % 4];
                    }
                }

                return Encoding.UTF8.GetString(payloadData);
            }

            private static byte[] ReadExactly(Stream stream, int length)
            {
                byte[] buffer = new byte[length];
                int read = 0;
                while (read < length)
                {
                    int chunk = stream.Read(buffer, read, length - read);
                    if (chunk <= 0) return null;
                    read += chunk;
                }
                return buffer;
            }

            private static void SendText(NetworkStream stream, string message)
            {
                byte[] payload = Encoding.UTF8.GetBytes(message);
                using var memory = new MemoryStream();
                memory.WriteByte(0x81);

                if (payload.Length <= 125)
                {
                    memory.WriteByte((byte)payload.Length);
                }
                else if (payload.Length <= ushort.MaxValue)
                {
                    memory.WriteByte(126);
                    memory.WriteByte((byte)(payload.Length >> 8));
                    memory.WriteByte((byte)(payload.Length & 0xFF));
                }
                else
                {
                    memory.WriteByte(127);
                    byte[] lengthBytes = BitConverter.GetBytes((ulong)payload.Length);
                    if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
                    memory.Write(lengthBytes, 0, lengthBytes.Length);
                }

                memory.Write(payload, 0, payload.Length);
                byte[] frame = memory.ToArray();
                stream.Write(frame, 0, frame.Length);
                stream.Flush();
            }
        }
    }
}
