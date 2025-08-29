using Newtonsoft.Json.Linq;
using UnityEngine;
using Functions.ScenesManager;
using Helpers;

namespace Handler
{
    public static class SceneHandler
    {
        public static object Handle(JObject command)
        {
            string action = command["action"]?.ToString()?.ToLower();
            if (string.IsNullOrEmpty(action))
                return Error("Missing 'action' field in params");

            return action switch
            {
                "get_active" => GetActive.Execute(command),
                "get_build_scenes" => GetBuildScenes.Execute(command),
                "load" => LoadScene.Execute(command),
                "load_additive" => LoadAdditive.Execute(command),
                "set_active" => SetActive.Execute(command),
                "unload" => Unload.Execute(command),
                "get_loaded_scenes" => GetLoadedScenes.Execute(command),
                "get_hierarchy" => GetHierarchy.Execute(command),

                // 추가적인 액션 핸들러

                _ => Error($"Unknown action '{action}' in manage_path")
            };
        }

        private static object Error(string message) =>
            new { success = false, error = message };
    }
}
