// FILE: Assets/Scripts/Functions/Scene/GetLoadedScenes.cs
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;
using Helpers;

namespace Functions.ScenesManager
{
    /// <summary>
    /// { "type":"manage_scene", "params": { "action":"get_loaded_scenes" } }
    /// </summary>
    public static class GetLoadedScenes
    {
        public static object Execute(JObject _)
        {
            return MainThreadDispatcher.Run(() =>
            {
                int count = SceneManager.sceneCount;
                var list = new List<object>(count);

                for (int i = 0; i < count; i++)
                {
                    var s = SceneManager.GetSceneAt(i);

                    // Resolve path (may be empty in player builds)
                    string path = "";
                    if (s.buildIndex >= 0 && s.buildIndex < SceneManager.sceneCountInBuildSettings)
                    {
                        try { path = SceneUtility.GetScenePathByBuildIndex(s.buildIndex); } catch { /* ignore */ }
                    }
                    if (string.IsNullOrEmpty(path)) path = s.path;

                    list.Add(new
                    {
                        name = s.name,
                        buildIndex = s.buildIndex,
                        isLoaded = s.isLoaded,
                        rootCount = s.rootCount,
                        path
                    });
                }

                return Response.Success("Loaded scenes retrieved.", list);
            });
        }
    }
}
