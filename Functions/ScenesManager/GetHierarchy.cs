// FILE: Assets/Scripts/Functions/Scene/GetHierarchy.cs
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Helpers;

namespace Functions.ScenesManager
{
    /// <summary>
    /// { "type":"manage_scene", "params": { "action":"get_hierarchy" } }
    /// Returns a recursive GameObject tree of the active scene.
    /// </summary>
    public static class GetHierarchy
    {
        public static object Execute(JObject _)
        {
            return MainThreadDispatcher.Run(() =>
            {
                var s = SceneManager.GetActiveScene();
                if (!s.IsValid() || !s.isLoaded)
                    return Response.Error("No active scene.");

                var roots = s.GetRootGameObjects();
                var list = new List<object>(roots.Length);
                for (int i = 0; i < roots.Length; i++)
                    list.Add(Node(roots[i]));

                return Response.Success("Hierarchy retrieved.", list);
            });
        }

        private static object Node(GameObject go)
        {
            var t = go.transform;

            // Collect children first (depth-first)
            var children = new List<object>(t.childCount);
            for (int i = 0; i < t.childCount; i++)
                children.Add(Node(t.GetChild(i).gameObject));

            return new
            {
                name = go.name,
                tag = go.tag,
                layer = go.layer,
                activeSelf = go.activeSelf,
                transform = new
                {
                    position = new { x = t.localPosition.x, y = t.localPosition.y, z = t.localPosition.z },
                    rotationEuler = new { x = t.localRotation.eulerAngles.x, y = t.localRotation.eulerAngles.y, z = t.localRotation.eulerAngles.z },
                    scale = new { x = t.localScale.x, y = t.localScale.y, z = t.localScale.z }
                },
                children
            };
        }
    }
}
