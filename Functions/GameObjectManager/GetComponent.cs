using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.GameObjectManager
{
    public static class GetComponent{
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

                try
                {
                    Component[] components = targetGo.GetComponents<Component>();
                    var componentList = components
                        .Select(Utils.GetComponentData)
                        .Where(c => c != null)
                        .ToList();

                    return Response.Success(
                        $"Found {componentList.Count} components on '{targetGo.name}'.",
                        componentList
                    );
                }
                catch (Exception e)
                {
                    return Response.Error($"Error retrieving components from '{targetGo.name}': {e.Message}");
                }
            }).Result;
        }
    }
}
