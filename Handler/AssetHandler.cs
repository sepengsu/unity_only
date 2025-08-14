using Newtonsoft.Json.Linq;
using UnityEngine;
using Functions.AssetManager;
using Helpers;

namespace Handler
{
    public static class AssetHandler
    {
        public static object Handle(JObject command)
        {
            string action = command["action"]?.ToString()?.ToLower();
            if (string.IsNullOrEmpty(action))
                return Error("Missing 'action' field in params");

            return action switch
            {
                "search" => Search.Execute(command),
                "modify" => Modify.Execute(command),
                "get_info" => GetAssetInfo.Execute(command),


                // 추가적인 액션 핸들러

                _ => Error($"Unknown action '{action}' in manage_asset")
            };
        }

        private static object Error(string message) =>
            new { success = false, error = message };
    }
}
