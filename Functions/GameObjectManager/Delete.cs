using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.GameObjectManager
{
    public static class Delete
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string target = data["target"]?.ToString();
                string searchMethod = data["searchMethod"]?.ToString()?.ToLower() ?? "by_name";
                bool findAll = data["findAll"]?.ToObject<bool>() ?? false;
                bool searchInactive = data["searchInactive"]?.ToObject<bool>() ?? true;

                if (string.IsNullOrEmpty(target))
                    return Response.Error("Missing 'target' parameter.");

                GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(searchInactive);
                List<GameObject> found = new();

                switch (searchMethod)
                {
                    case "by_name":
                        found = allObjects.Where(go => go.name == target).ToList();
                        break;

                    case "by_tag":
                        found = allObjects.Where(go => go.CompareTag(target)).ToList();
                        break;

                    case "by_layer":
                        if (int.TryParse(target, out int layerIndex))
                            found = allObjects.Where(go => go.layer == layerIndex).ToList();
                        else
                        {
                            int namedLayer = LayerMask.NameToLayer(target);
                            if (namedLayer != -1)
                                found = allObjects.Where(go => go.layer == namedLayer).ToList();
                        }
                        break;

                    case "by_component":
                        Type compType = Type.GetType("UnityEngine." + target + ", UnityEngine");
                        if (compType != null)
                            found = allObjects.Where(go => go.GetComponent(compType) != null).ToList();
                        break;

                    case "by_path":
                        foreach (var root in allObjects.Where(go => go.transform.parent == null))
                        {
                            Transform match = root.transform.Find(target);
                            if (match != null)
                                found.Add(match.gameObject);
                        }
                        break;

                    case "by_id":
                        if (int.TryParse(target, out int id))
                            found = allObjects.Where(go => go.GetInstanceID() == id).ToList();
                        break;

                    default:
                        return Response.Error($"Unknown searchMethod: '{searchMethod}'");
                }

                if (found.Count == 0)
                    return Response.Error($"No GameObject found for target '{target}'.");

                if (!findAll && found.Count > 1)
                    found = new List<GameObject> { found[0] };

                List<Dictionary<string, object>> deletedObjects = new();
                foreach (var go in found)
                {
                    deletedObjects.Add(new Dictionary<string, object>
                    {
                        { "name", go.name },
                        { "instanceID", go.GetInstanceID() }
                    });
                    UnityEngine.Object.Destroy(go);
                }

                string message = deletedObjects.Count == 1 && deletedObjects[0].TryGetValue("name", out var nameValue)
                    ? $"GameObject '{nameValue}' deleted."
                    : $"{deletedObjects.Count} GameObjects deleted.";

                return Response.Success(message, deletedObjects);
            }).Result;
        }
    }
}
