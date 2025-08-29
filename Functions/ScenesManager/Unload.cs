// FILE: Assets/Scripts/Functions/Scene/Unload.cs
using System;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;
using Helpers;

namespace Functions.ScenesManager
{
    /// <summary>
    /// { "type":"manage_scene", "params": { "action":"unload", "name":"SubScene" } }
    /// or
    /// { "type":"manage_scene", "params": { "action":"unload", "buildIndex": 2 } }
    /// </summary>
    public static class Unload
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string name = data["name"]?.ToString();
                int? buildIndex = data["buildIndex"]?.ToObject<int?>();

                try
                {
                    Scene scene = default;

                    if (!string.IsNullOrWhiteSpace(name))
                        scene = SceneManager.GetSceneByName(name);
                    else if (buildIndex.HasValue)
                        scene = SceneManager.GetSceneByBuildIndex(buildIndex.Value);
                    else
                        return Response.Error("Either 'name' or 'buildIndex' is required.");

                    if (!scene.IsValid() || !scene.isLoaded)
                        return Response.Error("Scene is not loaded.");

                    var op = SceneManager.UnloadSceneAsync(scene);
                    return Response.Success("Unload started.", new { isDone = op?.isDone ?? false });
                }
                catch (Exception e)
                {
                    return Response.Error($"Failed to unload scene: {e.Message}");
                }
            });
        }
    }
}
