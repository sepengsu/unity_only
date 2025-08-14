using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.GameObjectManager
{
    public static class SetComponentPropertyOnTarget
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string target = data["target"]?.ToString();
                string searchMethod = data["searchMethod"]?.ToString()?.ToLower() ?? "by_name";

                if (string.IsNullOrEmpty(target))
                    return Response.Error("Missing 'target' parameter.");

                GameObject targetGo = Utils.CustomFinder(target, searchMethod);
                if (targetGo == null)
                    return Response.Error($"GameObject '{target}' not found using method '{searchMethod}'.");

                string compName = data["componentName"]?.ToString();
                if (string.IsNullOrEmpty(compName))
                    return Response.Error("'componentName' parameter is required.");

                JObject propertiesToSet = null;
                if (data["componentProperties"] is JObject props)
                {
                    // Allow both flat or nested structure
                    propertiesToSet = props[compName] as JObject ?? props;
                }

                if (propertiesToSet == null || !propertiesToSet.HasValues)
                    return Response.Error("'componentProperties' for the specified component is missing or empty.");

                // Find component
                var component = targetGo.GetComponent(compName);
                if (component == null)
                    return Response.Error($"Component '{compName}' not found on '{targetGo.name}'.");

                // Apply each property
                foreach (var prop in propertiesToSet.Properties())
                {
                    try
                    {
                        Utils.SetProperty(component, prop.Name, prop.Value);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to set property '{prop.Name}' on '{compName}': {ex.Message}");
                        return Response.Error($"Failed to set property '{prop.Name}' on component '{compName}': {ex.Message}");
                    }
                }

                return Response.Success(
                    $"Properties updated for component '{compName}' on '{targetGo.name}'.",
                    Utils.GetGameObjectData(targetGo)
                );
            }).Result;
        }
    }
}
