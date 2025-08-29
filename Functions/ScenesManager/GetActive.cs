// FILE: Assets/Scripts/Functions/Scene/GetActive.cs
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;
using Helpers;

namespace Functions.ScenesManager
{
    /// <summary>
    /// { "type": "manage_scene", "params": { "action": "get_active" } }
    /// </summary>
    public static class GetActive
    {
        public static object Execute(JObject _)
        {
            return MainThreadDispatcher.Run(() =>
            {
                var s = SceneManager.GetActiveScene();
                if (!s.IsValid())
                    return Response.Error("No active scene.");

                string path = null;
                if (s.buildIndex >= 0 && s.buildIndex < SceneManager.sceneCountInBuildSettings)
                {
                    try { path = SceneUtility.GetScenePathByBuildIndex(s.buildIndex); }
                    catch { path = s.path; } // may be empty in player builds
                }
                else
                {
                    path = s.path; // may be empty in player builds
                }

                var data = new
                {
                    name = s.name,
                    buildIndex = s.buildIndex,
                    isLoaded = s.isLoaded,
                    rootCount = s.rootCount,
                    path,
                    isValid = s.IsValid()
                };

                return Response.Success("Active scene retrieved.", data);
            });
        }
    }
}
