// FILE: Assets/Scripts/Functions/Asset/Modify.cs
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;
using AMUtils = Functions.AssetManager.Utils;
using GMUtils = Functions.GameObjectManager.Utils;

namespace Functions.AssetManager
{
    public static class Modify
    {
        public static object Execute(JObject @params)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string path = @params["path"]?.ToString();
                JObject properties = @params["properties"] as JObject;

                if (string.IsNullOrEmpty(path))
                    return Response.Error("'path' is required for modify.");
                if (properties == null || !properties.HasValues)
                    return Response.Error("'properties' are required for modify.");

                // Resources path sanitize (no extension; relative to any Resources/)
                string resourcePath = AMUtils.SanitizePath(path);

                // Optional existence check (runtime-friendly)
                if (!AMUtils.AssetExists(resourcePath))
                    return Response.Error($"Prefab not found at Resources path: '{resourcePath}'");

                // Load & instantiate
                GameObject prefab = Resources.Load<GameObject>(resourcePath);
                if (prefab == null)
                    return Response.Error($"Failed to load prefab at Resources/{resourcePath}");

                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                if (instance == null)
                    return Response.Error($"Failed to instantiate prefab: '{resourcePath}'");

                bool modified = false;

                // Apply component property changes
                foreach (var componentEntry in properties.Properties())
                {
                    string componentName = componentEntry.Name;

                    if (componentEntry.Value is not JObject componentProps)
                    {
                        Debug.LogWarning($"[Modify] Skipping '{componentName}': expected an object of property values.");
                        continue;
                    }

                    Component targetComponent = instance.GetComponent(componentName);
                    if (targetComponent == null)
                    {
                        Debug.LogWarning($"[Modify] Component '{componentName}' not found on '{instance.name}'.");
                        continue;
                    }

                    foreach (var prop in componentProps.Properties())
                    {
                        try
                        {
                            if (prop.Name.Contains("."))
                                GMUtils.SetNestedProperty(targetComponent, prop.Name, prop.Value); // e.g., material.color
                            else
                                GMUtils.SetProperty(targetComponent, prop.Name, prop.Value);

                            modified = true;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[Modify] Failed to set '{prop.Name}' on '{componentName}': {e.Message}");
                        }
                    }
                }

                string message = modified
                    ? $"GameObject '{instance.name}' modified successfully."
                    : $"No changes applied to GameObject '{instance.name}'.";

                return Response.Success(message, GMUtils.GetGameObjectData(instance));
            });
        }
    }
}
