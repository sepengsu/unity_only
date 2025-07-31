using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.GameObjectManager
{
    public static class Find
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string target = data["target"]?.ToString();
                string searchMethod = data["searchMethod"]?.ToString()?.ToLower() ?? "by_name";
                bool findAll = data["findAll"]?.ToObject<bool>() ?? false;
                bool searchInactive = data["searchInactive"]?.ToObject<bool>() ?? true;
                bool searchInChildren = data["searchInChildren"]?.ToObject<bool>() ?? false;

                if (string.IsNullOrEmpty(target))
                    return Response.Error("Missing 'target' parameter.");

                List<GameObject> results = new();

                GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(searchInactive);

                switch (searchMethod)
                {
                    case "by_name":
                        results = allObjects.Where(go => go.name == target).ToList();
                        break;

                    case "by_tag":
                        results = allObjects.Where(go => go.CompareTag(target)).ToList();
                        break;

                    case "by_layer":
                        if (int.TryParse(target, out int layerIndex))
                            results = allObjects.Where(go => go.layer == layerIndex).ToList();
                        else
                        {
                            int namedLayer = LayerMask.NameToLayer(target);
                            if (namedLayer != -1)
                                results = allObjects.Where(go => go.layer == namedLayer).ToList();
                        }
                        break;

                    case "by_component":
                        Type compType = Type.GetType("UnityEngine." + target + ", UnityEngine");
                        if (compType != null)
                            results = allObjects.Where(go => go.GetComponent(compType) != null).ToList();
                        break;

                    case "by_path":
                        foreach (var root in GetRootObjects())
                        {
                            Transform found = root.transform.Find(target);
                            if (found != null)
                                results.Add(found.gameObject);
                        }
                        break;

                    case "by_id":
                        if (int.TryParse(target, out int id))
                            results = allObjects.Where(go => go.GetInstanceID() == id).ToList();
                        break;

                    default:
                        return Response.Error($"Unknown searchMethod: '{searchMethod}'");
                }

                if (results.Count == 0)
                    return Response.Success("No matching GameObjects found.", new List<object>());

                if (!findAll && results.Count > 1)
                    results = new List<GameObject> { results[0] };

                var serialized = results.Select(go => Utils.GetGameObjectData(go)).ToList();
                return Response.Success($"Found {serialized.Count} GameObject(s).", serialized);
            }).Result;
        }

        private static List<GameObject> GetRootObjects()
        {
            var roots = new List<GameObject>();
            foreach (var go in GameObject.FindObjectsOfType<GameObject>(true))
            {
                if (go.transform.parent == null)
                    roots.Add(go);
            }
            return roots;
        }
    }
}
