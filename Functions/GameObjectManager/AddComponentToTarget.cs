using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.GameObjectManager
{
    public static class AddComponentToTarget
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
                JObject properties = null;

                // 방식 1: componentName + componentProperties
                if (data["componentName"] != null)
                {
                    typeName = data["componentName"]?.ToString();
                    properties = data["componentProperties"]?[typeName] as JObject;
                }
                // 방식 2: componentsToAdd 배열 (첫 번째만 사용)
                else if (data["componentsToAdd"] is JArray componentsToAdd && componentsToAdd.Count > 0)
                {
                    var compToken = componentsToAdd.First;
                    if (compToken.Type == JTokenType.String)
                        typeName = compToken.ToString();
                    else if (compToken is JObject compObj)
                    {
                        typeName = compObj["typeName"]?.ToString();
                        properties = compObj["properties"] as JObject;
                    }
                }

                if (string.IsNullOrEmpty(typeName))
                    return Response.Error("Component type name is required (via 'componentName' or 'componentsToAdd').");

                var result = Utils.AddComponentWithProperties(targetGo, typeName, properties);
                if (result is string errorMessage)
                    return Response.Error(errorMessage);

                return Response.Success(
                    $"Component '{typeName}' added to '{targetGo.name}'.",
                    Utils.GetGameObjectData(targetGo)
                );
            }).Result;
        }
    }
}
