// FILE: Assets/Scripts/Functions/Scene/LoadScene.cs
using System;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;
using Helpers;

namespace Functions.ScenesManager
{
    /// <summary>
    /// Unified scene loader (engine-only).
    /// JSON examples:
    /// { "type":"manage_scene", "params": { "action":"load", "name":"MainMenu" } }
    /// { "type":"manage_scene", "params": { "action":"load", "buildIndex":1 } }
    /// { "type":"manage_scene", "params": { "action":"load", "name":"HUD", "additive":true } }
    /// { "type":"manage_scene", "params": { "action":"load", "buildIndex":2, "additive":true, "setActive":true } }
    ///
    /// Params:
    /// - name?: string
    /// - buildIndex?: int
    /// - additive?: bool (default: false = Single)
    /// - setActive?: bool (default: false; only meaningful for additive)
    /// </summary>
    public static class LoadScene
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string name = data["name"]?.ToString();
                int? buildIndex = data["buildIndex"]?.ToObject<int?>();
                bool additive = data["additive"]?.ToObject<bool?>() ?? false;
                bool setActive = data["setActive"]?.ToObject<bool?>() ?? false;

                if (string.IsNullOrWhiteSpace(name) && !buildIndex.HasValue)
                    return Response.Error("Provide either 'name' or 'buildIndex'.");

                try
                {
                    if (additive)
                    {
                        // ---- Additive load path ----
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
                        else
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

                        if (!target.IsValid())
                            return Response.Error("Loaded scene is not valid (check name/index).");

                        if (setActive)
                        {
                            if (!target.isLoaded) return Response.Error("Loaded additively but not yet marked loaded.");
                            SceneManager.SetActiveScene(target);
                        }

                        return Response.Success("Additive scene loaded.", SceneInfo(target));
                    }
                    else
                    {
                        // ---- Single load path ----
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            SceneManager.LoadScene(name, LoadSceneMode.Single);
                        }
                        else
                        {
                            if (buildIndex.Value < 0 || buildIndex.Value >= SceneManager.sceneCountInBuildSettings)
                                return Response.Error("'buildIndex' is out of range.");

                            SceneManager.LoadScene(buildIndex.Value, LoadSceneMode.Single);
                        }

                        var active = SceneManager.GetActiveScene();
                        return Response.Success("Scene loaded.", new
                        {
                            name = active.name,
                            buildIndex = active.buildIndex,
                            isLoaded = active.isLoaded,
                            rootCount = active.rootCount,
                            path = PathOrEmpty(active)
                        });
                    }
                }
                catch (Exception e)
                {
                    return Response.Error($"Failed to load scene: {e.Message}");
                }
            });
        }

        private static object SceneInfo(Scene s) => new
        {
            name = s.name,
            buildIndex = s.buildIndex,
            isLoaded = s.isLoaded,
            rootCount = s.rootCount,
            path = PathOrEmpty(s)
        };

        private static string PathOrEmpty(Scene s)
        {
            if (s.buildIndex >= 0 && s.buildIndex < SceneManager.sceneCountInBuildSettings)
            {
                try { return SceneUtility.GetScenePathByBuildIndex(s.buildIndex); }
                catch { /* ignore */ }
            }
            return s.path; // may be empty in player builds
        }
    }
}
