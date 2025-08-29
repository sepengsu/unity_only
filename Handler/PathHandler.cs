using Newtonsoft.Json.Linq;
using UnityEngine;
using Functions.PathManager;
using Helpers;

namespace Handler
{
    public static class PathHandler
    {
        public static object Handle(JObject command)
        {
            string action = command["action"]?.ToString()?.ToLower();
            if (string.IsNullOrEmpty(action))
                return Error("Missing 'action' field in params");

            return action switch
            {
                "make_path" => MakePath.Execute(command),
                "make_target" => MakeTarget.Execute(command),
                "connect_targets" => ConnectTargets.Execute(command),
                "start_path" => StartPath.Execute(command),

                // 추가적인 액션 핸들러

                _ => Error($"Unknown action '{action}' in manage_path")
            };
        }

        private static object Error(string message) =>
            new { success = false, error = message };
    }
}
