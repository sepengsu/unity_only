using UnityEngine;

namespace Data
{
    public static class DefaultServerConfig
    {
        public static readonly string unityHost = "localhost";
        public static readonly int unityPort = 6400;              // ✅ MCP에서 사용하는 포트 (고정)
        public static readonly float connectionTimeout = 15.0f;   // seconds
        public static readonly int bufferSize = 32768;            // bytes (32KB)
        public static readonly string logLevel = "INFO";
        public static readonly string logFormat = "%(asctime)s - %(name)s - %(levelname)s - %(message)s";
        public static readonly int maxRetries = 3;
        public static readonly float retryDelay = 1.0f;

        public enum LogLevel { Critical, Error, Warning, Info, Debug, None }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (level <= GetLogLevel())
                Debug.Log($"[{level}] {message}");
        }

        private static LogLevel GetLogLevel()
        {
            return logLevel.ToUpper() switch
            {
                "CRITICAL" => LogLevel.Critical,
                "ERROR" => LogLevel.Error,
                "WARNING" => LogLevel.Warning,
                "INFO" => LogLevel.Info,
                "DEBUG" => LogLevel.Debug,
                _ => LogLevel.Info
            };
        }
    }
}
