using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Data;
using Parser; // CommandParser.HandleCommand

namespace Network
{
    public static class Interface
    {
        private static TcpListener _listener;
        private static bool _isRunning = false;
        private static readonly object _lock = new object();

        private static readonly int _port = DefaultServerConfig.unityPort;
        private static readonly int _bufferSize = DefaultServerConfig.bufferSize;
        private static readonly int _connTimeoutMs = (int)(DefaultServerConfig.connectionTimeout * 1000);

        public static event Func<string, Task<string>> OnCommandReceived;

        public static void StartServer()
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    DefaultServerConfig.Log($"[UnityMcpServer] Already running on port {_port}", DefaultServerConfig.LogLevel.Warning);
                    return;
                }

                if (IsPortInUse(IPAddress.Loopback, _port))
                {
                    DefaultServerConfig.Log($"[UnityMcpServer] Port {_port} already in use (another process or previous run).",
                        DefaultServerConfig.LogLevel.Error);
                    return;
                }

                try
                {
                    _listener = new TcpListener(IPAddress.Loopback, _port);

                    // 바인딩 전에 옵션
                    try { _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); } catch { }
                    try { _listener.Server.ExclusiveAddressUse = false; } catch { }

                    _listener.Start();
                    _isRunning = true;

                    DefaultServerConfig.Log($"[UnityMcpServer] Started on port {_port}", DefaultServerConfig.LogLevel.Info);

                    // 명령 핸들러 기본 등록
                    OnCommandReceived = (input) => Task.FromResult(CommandParser.HandleCommand(input));

                    _ = ListenLoop(); // fire & forget
                }
                catch (SocketException se)
                {
                    DefaultServerConfig.Log($"[UnityMcpServer] Failed to start (SocketError {se.SocketErrorCode}): {se.Message}",
                        DefaultServerConfig.LogLevel.Error);
                    SafeReset();
                }
                catch (Exception e)
                {
                    DefaultServerConfig.Log($"[UnityMcpServer] Failed to start: {e.Message}", DefaultServerConfig.LogLevel.Error);
                    SafeReset();
                }
            }
        }

        public static void StopServer()
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                try
                {
                    _isRunning = false;
                    _listener?.Stop();
                    _listener = null;
                    DefaultServerConfig.Log("[UnityMcpServer] Stopped.", DefaultServerConfig.LogLevel.Info);
                }
                catch (Exception e)
                {
                    DefaultServerConfig.Log($"[UnityMcpServer] Error stopping server: {e.Message}", DefaultServerConfig.LogLevel.Error);
                }
            }
        }

        private static async Task ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = HandleClientAsync(client); // 클라이언트별 비동기 처리
                }
                catch (ObjectDisposedException)
                {
                    // 정상 종료 경로
                }
                catch (Exception e)
                {
                    if (_isRunning)
                        DefaultServerConfig.Log($"[UnityMcpServer] Listener error: {e.Message}", DefaultServerConfig.LogLevel.Error);
                }
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                client.ReceiveTimeout = _connTimeoutMs;

                // 클라이언트별 버퍼(동시접속 안전)
                var buffer = new byte[_bufferSize];

                while (_isRunning)
                {
                    try
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                        if (bytesRead == 0) break;

                        string input = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                        if (input == "ping")
                        {
                            await Send(stream, "{\"status\":\"success\",\"result\":{\"message\":\"pong\"}}").ConfigureAwait(false);
                            continue;
                        }

                        if (OnCommandReceived != null)
                        {
                            string result = await OnCommandReceived.Invoke(input).ConfigureAwait(false);
                            await Send(stream, result).ConfigureAwait(false);
                        }
                        else
                        {
                            await Send(stream, "{\"status\":\"error\",\"error\":\"No handler registered\"}").ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        DefaultServerConfig.Log($"[UnityMcpServer] Client error: {e.Message}", DefaultServerConfig.LogLevel.Error);
                        break;
                    }
                }
            }
        }

        private static async Task Send(NetworkStream stream, string response)
        {
            response = (response ?? string.Empty).TrimEnd() + "\n"; // 구분자 보장
            byte[] data = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        }

        private static bool IsPortInUse(IPAddress ip, int port)
        {
            TcpListener probe = null;
            try
            {
                probe = new TcpListener(ip, port);
                try { probe.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); } catch { }
                try { probe.Server.ExclusiveAddressUse = false; } catch { }
                probe.Start();
                probe.Stop();
                return false; // 바인딩 성공 → 사용 중 아님
            }
            catch
            {
                return true; // 바인딩 실패 → 사용 중
            }
            finally
            {
                try { probe?.Stop(); } catch { }
            }
        }


        private static void SafeReset()
        {
            try { _listener?.Stop(); } catch { }
            _listener = null;
            _isRunning = false;
        }
    }
}
