// FILE: Assets/Scripts/Functions/Scene/LoadAdditive.cs
using System;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;
using Helpers;

namespace Functions.ScenesManager
{
    /// <summary>
    /// { "type":"manage_scene", "params": { "action":"load_additive", "name":"SubScene" } }
    /// or
    /// { "type":"manage_scene", "params": { "action":"load_additive", "buildIndex": 2 } }
    /// Optional: { "setActive": true }
    /// </summary>
    public static class LoadAdditive
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string name = data["name"]?.ToString();
                int? buildIndex = data["buildIndex"]?.ToObject<int?>();
                bool setActive = data["setActive"]?.ToObject<bool?>() ?? false;

                try
                {
                    Scene target = default;

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var existing = SceneManager.GetSceneByName(name);
                        if (existing.IsValid() && existing.isLoaded)
                        {
                            if (setActive) SceneManager.SetActiveScene(existing);
                            return Response.Success($"Scene '{name}' already loaded additively.", SceneInfo(existing));
                        }

                        SceneManager.LoadScene(name, LoadSceneMode.Additive);
                        target = SceneManager.GetSceneByName(name);
                    }
                    else if (buildIndex.HasValue)
                    {
                        if (buildIndex.Value < 0 || buildIndex.Value >= SceneManager.sceneCountInBuildSettings)
                            return Response.Error("'buildIndex' is out of range.");

                        var existing = SceneManager.GetSceneByBuildIndex(buildIndex.Value);
                        if (existing.IsValid() && existing.isLoaded)
                        {
                            if (setActive) SceneManager.SetActiveScene(existing);
                            return Response.Success($"Scene index {buildIndex.Value} already loaded additively.", SceneInfo(existing));
                        }

                        SceneManager.LoadScene(buildIndex.Value, LoadSceneMode.Additive);
                        target = SceneManager.GetSceneByBuildIndex(buildIndex.Value);
                    }
                    else
                    {
                        return Response.Error("Either 'name' or 'buildIndex' is required.");
                    }

                    if (!target.IsValid())
                        return Response.Error("Loaded scene is not valid (check name/index).");

                    if (setActive)
                    {
                        if (!target.isLoaded)
                            return Response.Error("Scene loaded additively but not yet marked as loaded.");
                        SceneManager.SetActiveScene(target);
                    }

                    return Response.Success("Additive scene loaded.", SceneInfo(target));
                }
                catch (Exception e)
                {
                    return Response.Error($"Failed to load additive scene: {e.Message}");
                }
            });
        }

        private static object SceneInfo(Scene s)
        {
            string path = "";
            if (s.buildIndex >= 0 && s.buildIndex < SceneManager.sceneCountInBuildSettings)
            {
                try { path = SceneUtility.GetScenePathByBuildIndex(s.buildIndex); } catch { }
            }
            if (string.IsNullOrEmpty(path)) path = s.path; // may be empty in player builds

            return new
            {
                name = s.name,
                buildIndex = s.buildIndex,
                isLoaded = s.isLoaded,
                rootCount = s.rootCount,
                path
            };
        }
    }
}
