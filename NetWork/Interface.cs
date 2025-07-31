using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Data;
using Parser; // ✅ Parser.CommandParser 참조

namespace Network
{
    public static class Interface
    {
        private static TcpListener listener;
        private static bool isRunning = false;
        private static readonly int port = DefaultServerConfig.unityPort;
        private static readonly byte[] buffer = new byte[DefaultServerConfig.bufferSize];

        public static event Func<string, Task<string>> OnCommandReceived;

        public static void StartServer()
        {
            if (isRunning) return;

            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                isRunning = true;
                DefaultServerConfig.Log($"[UnityMcpServer] Started on port {port}", DefaultServerConfig.LogLevel.Info);

                // ✅ 명령 처리 핸들러 등록 (동기 → Task로 래핑)
                OnCommandReceived = (input) => Task.FromResult(CommandParser.HandleCommand(input));

                Task.Run(ListenLoop);
            }
            catch (Exception e)
            {
                DefaultServerConfig.Log($"[UnityMcpServer] Failed to start: {e.Message}", DefaultServerConfig.LogLevel.Error);
            }
        }

        public static void StopServer()
        {
            if (!isRunning) return;

            try
            {
                listener.Stop();
                listener = null;
                isRunning = false;
                DefaultServerConfig.Log("[UnityMcpServer] Stopped.", DefaultServerConfig.LogLevel.Info);
            }
            catch (Exception e)
            {
                DefaultServerConfig.Log($"[UnityMcpServer] Error stopping server: {e.Message}", DefaultServerConfig.LogLevel.Error);
            }
        }

        private static async Task ListenLoop()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
                catch (Exception e)
                {
                    if (isRunning)
                        DefaultServerConfig.Log($"[UnityMcpServer] Listener error: {e.Message}", DefaultServerConfig.LogLevel.Error);
                }
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                client.ReceiveTimeout = (int)(DefaultServerConfig.connectionTimeout * 1000);

                while (isRunning)
                {
                    try
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        string input = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                        if (input == "ping")
                        {
                            await Send(stream, "{\"status\":\"success\",\"result\":{\"message\":\"pong\"}}");
                            continue;
                        }

                        if (OnCommandReceived != null)
                        {
                            string result = await OnCommandReceived.Invoke(input);
                            await Send(stream, result);
                        }
                        else
                        {
                            await Send(stream, "{\"status\":\"error\",\"error\":\"No handler registered\"}");
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
            // ✅ 항상 개행 추가로 구분자 명확화
            response = response.Trim();
            if (!response.EndsWith("\n"))
                response += "\n";

            byte[] data = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(data, 0, data.Length);
        }
    }
}
