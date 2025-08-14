using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.GameObjectManager
{
    public static class RemoveComponentFromTarget
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

                string typeName = null;

                // 1. From componentName
                if (data["componentName"] != null)
                {
                    typeName = data["componentName"]?.ToString();
                }
                // 2. From componentsToRemove[0]
                else if (data["componentsToRemove"] is JArray removeArray && removeArray.Count > 0)
                {
                    typeName = removeArray.First?.ToString();
                }

                if (string.IsNullOrEmpty(typeName))
                    return Response.Error("Component type name is required (via 'componentName' or 'componentsToRemove').");

                // 3. Type resolve
                var type = Utils.FindType(typeName);
                if (type == null || !typeof(Component).IsAssignableFrom(type))
                    return Response.Error($"Invalid component type: '{typeName}'");

                if (type == typeof(Transform))
                    return Response.Error("Cannot remove the Transform component.");

                // 4. Get component and destroy
                var comp = targetGo.GetComponent(type);
                if (comp == null)
                    return Response.Error($"Component '{typeName}' not found on '{targetGo.name}'.");

                try
                {
                    UnityEngine.Object.Destroy(comp);
                    return Response.Success(
                        $"Component '{typeName}' removed from '{targetGo.name}'.",
                        Utils.GetGameObjectData(targetGo)
                    );
                }
                catch (Exception e)
                {
                    return Response.Error($"Failed to remove component '{typeName}': {e.Message}");
                }
            }).Result;
        }
    }
}
