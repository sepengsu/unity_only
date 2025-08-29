// FILE: Assets/Scripts/Functions/Scene/SetActive.cs
using System;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;
using Helpers;

namespace Functions.ScenesManager
{
    /// <summary>
    /// { "type":"manage_scene", "params": { "action":"set_active", "name":"SubScene" } }
    /// or
    /// { "type":"manage_scene", "params": { "action":"set_active", "buildIndex": 2 } }
    /// </summary>
    public static class SetActive
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string name = data["name"]?.ToString();
                int? buildIndex = data["buildIndex"]?.ToObject<int?>();

                try
                {
                    Scene target = default;

                    if (!string.IsNullOrWhiteSpace(name))
                        target = SceneManager.GetSceneByName(name);
                    else if (buildIndex.HasValue)
                        target = SceneManager.GetSceneByBuildIndex(buildIndex.Value);
                    else
                        return Response.Error("Either 'name' or 'buildIndex' is required.");

                    if (!target.IsValid() || !target.isLoaded)
                        return Response.Error("Target scene is not loaded.");

                    SceneManager.SetActiveScene(target);

                    return Response.Success("Active scene set.", new
                    {
                        name = target.name,
                        buildIndex = target.buildIndex,
                        isLoaded = target.isLoaded,
                        rootCount = target.rootCount
                    });
                }
                catch (Exception e)
                {
                    return Response.Error($"Failed to set active scene: {e.Message}");
                }
            });
        }
    }
}
