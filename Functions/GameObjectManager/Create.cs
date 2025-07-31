using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.GameObjectManager
{
    public static class Create
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string name = data["name"]?.ToString() ?? "NewObject";
                string primitiveType = data["primitiveType"]?.ToString();
                string prefabPath = data["prefabPath"]?.ToString();
                GameObject go = null;

                
                // --- Addressables 프리팹 인스턴스화 (addr: 프리픽스 사용) ---
                if (!string.IsNullOrEmpty(prefabPath) && prefabPath.StartsWith("addr:"))
                {
                    string addressKey = prefabPath.Substring("addr:".Length);
                    var handle = UnityEngine.AddressableAssets.Addressables.InstantiateAsync(addressKey);
                    handle.WaitForCompletion(); // 동기 블로킹 로드 (에디터/런타임 호환)
                    if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    {
                        go = handle.Result;
                        go.name = name;
                    }
                    else
                    {
                        Debug.LogWarning($"Addressables prefab load failed: {addressKey}");
                    }
                }
                // --- Resources 폴더에서 프리팹 인스턴스화 ---
                else if (!string.IsNullOrEmpty(prefabPath))
                {
                    GameObject prefab = Resources.Load<GameObject>(prefabPath);
                    if (prefab != null)
                    {
                        go = UnityEngine.Object.Instantiate(prefab);
                        go.name = name;
                    }
                    else
                    {
                        Debug.LogWarning($"Prefab not found at Resources path: {prefabPath}");
                    }
                }

                // --- 프리미티브 생성 ---
                if (go == null && !string.IsNullOrEmpty(primitiveType)
                    && Enum.TryParse(primitiveType, true, out PrimitiveType type))
                {
                    go = GameObject.CreatePrimitive(type);
                    go.name = name;
                }

                // --- 기본 GameObject 생성 ---
                if (go == null)
                {
                    go = new GameObject(name);
                }

                // --- Transform 설정 ---
                if (data["position"] is JArray posArr)
                    go.transform.localPosition = Vector3Helper.ParseVector3(posArr);

                if (data["rotation"] is JArray rotArr)
                    go.transform.localEulerAngles = Vector3Helper.ParseVector3(rotArr);

                if (data["scale"] is JArray scaleArr)
                    go.transform.localScale = Vector3Helper.ParseVector3(scaleArr);

               // --- 부모 설정 (확장된 방식: by_name, by_path, by_id 등 지원) ---
                if (data["parent"] != null)
                {
                    GameObject parentGo = null;

                    if (data["parent"].Type == JTokenType.Object)
                    {
                        var parentObj = (JObject)data["parent"];
                        string parentTarget = parentObj["target"]?.ToString();
                        string searchMethod = parentObj["searchMethod"]?.ToString()?.ToLower() ?? "by_name";

                        parentGo = Utils.CustomFinder(parentTarget, searchMethod);
                    }
                    else // 기본 문자열 (이름)
                    {
                        string parentName = data["parent"].ToString();
                        parentGo = GameObject.Find(parentName);
                    }

                    if (parentGo != null)
                        go.transform.SetParent(parentGo.transform, true);
}


                // --- Tag 설정 ---
                if (data["tag"] != null)
                {
                    string tagToSet = data["tag"].ToString();
                    try
                    {
                        go.tag = tagToSet;
                    }
                    catch
                    {
                        Debug.LogWarning($"Tag '{tagToSet}' is not defined in this project.");
                    }
                }

                // --- Layer 설정 ---
                if (data["layer"] != null)
                {
                    int layer = LayerMask.NameToLayer(data["layer"].ToString());
                    if (layer != -1)
                        go.layer = layer;
                }

                // --- Active 상태 설정 ---
                if (data["setActive"]?.Type == JTokenType.Boolean)
                {
                    bool isActive = data["setActive"].ToObject<bool>();
                    go.SetActive(isActive);
                }

                // --- Component 추가 ---
                if (data["componentsToAdd"] is JArray comps)
                {
                    foreach (var comp in comps)
                    {
                        string typeName = null;
                        JObject props = null;

                        if (comp.Type == JTokenType.String)
                        {
                            typeName = comp.ToString();
                        }
                        else if (comp is JObject compObj)
                        {
                            typeName = compObj["typeName"]?.ToString();
                            props = compObj["properties"] as JObject;
                        }

                        if (!string.IsNullOrEmpty(typeName))
                            Utils.AddComponentWithProperties(go, typeName, props);
                    }
                }

                // --- 색상 설정 (Renderer.material.color) ---
                if (data["color"] is JArray colorArr)
                {
                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null && renderer.material != null)
                        renderer.material.color = Utils.ParseColor(colorArr);
                }

                // ✅ 성공 응답
                return Response.Success($"GameObject '{go.name}' created.", Utils.GetGameObjectData(go));
            }).Result;
        }
    }
}
