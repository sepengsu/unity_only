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
            string path = @params["path"]?.ToString();
            JObject properties = @params["properties"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for modify.");
            if (properties == null || !properties.HasValues)
                return Response.Error("'properties' are required for modify.");

            // ✅ 리소스 경로 정리 및 prefab 존재 여부 확인 (Asset 관련은 A에서 처리)
            string resourcePath = AMUtils.SanitizePath(path);
            if (!AMUtils.AssetExists(resourcePath))
                return Response.Error($"Prefab not found at Resources path: '{resourcePath}'");

            // ✅ 프리팹 로드 및 인스턴스화 (GameObject 관련은 G에서 처리)
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            if (instance == null)
                return Response.Error($"Failed to instantiate prefab: '{resourcePath}'");

            bool modified = false;

            // ✅ 컴포넌트 순회 (G에서 컴포넌트 찾고 속성 세팅)
            foreach (var componentEntry in properties.Properties())
            {
                string componentName = componentEntry.Name;

                if (componentEntry.Value is not JObject componentProps)
                {
                    Debug.LogWarning($"Skipping '{componentName}': expected object of property values.");
                    continue;
                }

                Component targetComponent = instance.GetComponent(componentName);
                if (targetComponent == null)
                {
                    Debug.LogWarning($"Component '{componentName}' not found on GameObject '{instance.name}'.");
                    continue;
                }

                foreach (var prop in componentProps.Properties())
                {
                    try
                    {
                        if (prop.Name.Contains("."))
                        {
                            // 중첩 속성 처리 (material.color 등)
                            GMUtils.SetNestedProperty(targetComponent, prop.Name, prop.Value);
                        }
                        else
                        {
                            // 단순 속성 처리
                            GMUtils.SetProperty(targetComponent, prop.Name, prop.Value);
                        }

                        modified = true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Modify] Failed to set '{prop.Name}' on '{componentName}': {e.Message}");
                    }
                }
            }

            // ✅ 결과 반환
            string message = modified
                ? $"GameObject '{instance.name}' modified successfully."
                : $"No changes applied to GameObject '{instance.name}'.";

            return Response.Success(message, GMUtils.GetGameObjectData(instance));
        }
    }
}
