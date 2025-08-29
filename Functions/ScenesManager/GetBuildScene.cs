// FILE: Assets/Scripts/Functions/Scene/GetBuildScenes.cs
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using Helpers;

namespace Functions.ScenesManager
{
    /// <summary>
    /// { "type": "manage_scene", "params": { "action": "get_build_scenes" } }
    /// </summary>
    public static class GetBuildScenes
    {
        public static object Execute(JObject _)
        {
            return MainThreadDispatcher.Run(() =>
            {
                int count = SceneManager.sceneCountInBuildSettings;
                var items = new List<object>(count);

                for (int i = 0; i < count; i++)
                {
                    string path = SceneUtility.GetScenePathByBuildIndex(i);
                    string name = string.IsNullOrEmpty(path)
                        ? $"index_{i}"
                        : Path.GetFileNameWithoutExtension(path);

                    items.Add(new { index = i, path, name });
                }

                return Response.Success("Build scenes retrieved.", items);
            });
        }
    }
}
