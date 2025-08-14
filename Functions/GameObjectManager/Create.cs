using System;
using System.IO;
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
                // -------- Inputs --------
                string name                = data["name"]?.ToString() ?? "NewObject";
                string primitiveType       = data["primitiveType"]?.ToString();
                string prefabPathRaw       = data["prefab_path"]?.ToString() ?? data["prefabPath"]?.ToString();
                string addressOverride     = data["addressKey"]?.ToString();
                bool   instantiateInactive = data["instantiateInactive"]?.ToObject<bool>() ?? false;

                // Normalize path slashes
                string prefabPath = string.IsNullOrEmpty(prefabPathRaw) ? null : prefabPathRaw.Replace("\\", "/");

                // -------- Option: explicit "addr:" prefix → treat as address key --------
                // e.g., "addr:IRB2400-10"
                if (string.IsNullOrEmpty(addressOverride) && !string.IsNullOrEmpty(prefabPath) && prefabPath.StartsWith("addr:", StringComparison.OrdinalIgnoreCase))
                {
                    addressOverride = prefabPath.Substring("addr:".Length);
                    prefabPath = null; // don't treat it as an asset path
                }

                // -------- Ensure G4A controller BEFORE instantiation (non-fatal here) --------
                // We try unconditionally; helper decides if it needs to act.
                if (!G4AHelper.EnsureControllerPresent(out var g4aSource, out var g4aMsg))
                {
                    if (!string.IsNullOrEmpty(g4aMsg))
                        Debug.LogWarning($"[Create] G4A controller ensure failed (non-fatal here): {g4aMsg}");
                }
                else
                {
                    Debug.Log($"[Create] G4A controller ensured via {g4aSource}");
                }

                GameObject go = null;

                // -------- Prefab instantiation --------
                if (!string.IsNullOrEmpty(prefabPath) || !string.IsNullOrEmpty(addressOverride))
                {
                    // 1) Resources
                    if (!string.IsNullOrEmpty(prefabPath) &&
                        prefabPath.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
                    {
                        string relative = prefabPath.Substring("Assets/Resources/".Length);
                        relative = Path.ChangeExtension(relative, null);
                        var prefab = Resources.Load<GameObject>(relative);
                        if (prefab != null)
                        {
                            go = UnityEngine.Object.Instantiate(prefab);
                            if (instantiateInactive) go.SetActive(false);
                            Debug.Log($"[Create] Loaded from Resources: {relative}");
                        }
                        else
                        {
                            Debug.LogWarning($"[Create] Resources not found: {relative}");
                        }
                    }
                    else
                    {
                        // 2) Addressables via registry or explicit address
                        PrefabRegistry.EnsureLoaded();

                        string addr = null;
                        string gid  = null;

                        if (!string.IsNullOrEmpty(addressOverride))
                        {
                            addr = addressOverride;
                        }
                        else if (!string.IsNullOrEmpty(prefabPath))
                        {
                            var item = PrefabRegistry.FindByAssetPath(prefabPath);
                            if (item == null)
                            {
                                PrefabRegistry.Reload();
                                item = PrefabRegistry.FindByAssetPath(prefabPath);
                            }
                            if (item != null)
                            {
                                addr = item.addressKey;
                                gid  = item.guid;
                            }
                            else
                            {
                                Debug.LogWarning($"[Create] prefab.json has no entry for assetPath: {prefabPath}");
                            }
                        }

                        if (!string.IsNullOrEmpty(addr) && go == null)
                            TryInstantiateByKey(addr, $"assetPath: {prefabPath} (addressKey)", instantiateInactive, out go);

                        if (!string.IsNullOrEmpty(gid) && go == null)
                            TryInstantiateByKey(gid, $"assetPath: {prefabPath} (guid fallback)", instantiateInactive, out go);

#if UNITY_EDITOR
                        // 3) Editor fallback: direct path
                        if (go == null && !string.IsNullOrEmpty(prefabPath))
                        {
                            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                            if (prefab != null)
                            {
                                go = UnityEngine.Object.Instantiate(prefab);
                                if (instantiateInactive) go.SetActive(false);
                                Debug.Log($"[Create] Editor fallback loaded prefab: {prefabPath}");
                            }
                        }
#endif

                        if (go == null)
                        {
                            Debug.LogError(
                                $"[Create] Instantiate FAILED. assetPath='{prefabPath}', " +
                                $"tried addressKey='{addr}', guid='{gid}'. " +
                                $"Check Addressables address, build, play mode script, and profile.");
                        }
                    }
                }

                // -------- Primitive fallback --------
                if (go == null && !string.IsNullOrEmpty(primitiveType)
                    && Enum.TryParse(primitiveType, true, out PrimitiveType type))
                {
                    go = GameObject.CreatePrimitive(type);
                    if (instantiateInactive) go.SetActive(false);
                }

                // -------- Default GameObject --------
                if (go == null)
                {
                    go = new GameObject(name);
                    if (instantiateInactive) go.SetActive(false);
                }

                go.name = name;

                // -------- Transform --------
                if (data["position"] is JArray posArr)
                    go.transform.localPosition = Vector3Helper.ParseVector3(posArr);
                if (data["rotation"] is JArray rotArr)
                    go.transform.localEulerAngles = Vector3Helper.ParseVector3(rotArr);
                if (data["scale"] is JArray scaleArr)
                    go.transform.localScale = Vector3Helper.ParseVector3(scaleArr);

                // -------- Parent --------
                if (data["parent"] != null && data["parent"].Type != JTokenType.Null)
                {
                    GameObject parentGo = null;
                    if (data["parent"].Type == JTokenType.Object)
                    {
                        var parentObj    = (JObject)data["parent"];
                        string target    = parentObj["target"]?.ToString();
                        string methodRaw = parentObj["searchMethod"]?.ToString();
                        string method    = string.IsNullOrEmpty(methodRaw) ? "by_name" : methodRaw.ToLower();
                        parentGo         = Utils.CustomFinder(target, method);
                    }
                    else
                    {
                        string parentName = data["parent"].ToString();
                        parentGo = GameObject.Find(parentName);
                    }

                    if (parentGo != null)
                        go.transform.SetParent(parentGo.transform, true);
                }

                // -------- Tag --------
                if (data["tag"] != null && data["tag"].Type != JTokenType.Null)
                {
                    string tagToSet = data["tag"].ToString();
                    try { go.tag = tagToSet; }
                    catch { Debug.LogWarning($"[Create] Tag '{tagToSet}' is not defined in this project."); }
                }

                // -------- Layer --------
                if (data["layer"] != null && data["layer"].Type != JTokenType.Null)
                {
                    int layer = LayerMask.NameToLayer(data["layer"].ToString());
                    if (layer != -1) go.layer = layer;
                }

                // -------- Components to add --------
                if (data["componentsToAdd"] is JArray comps)
                {
                    foreach (var comp in comps)
                    {
                        string typeName = null;
                        JObject props   = null;

                        if (comp.Type == JTokenType.String)
                            typeName = comp.ToString();
                        else if (comp is JObject compObj)
                        {
                            typeName = compObj["typeName"]?.ToString();
                            props    = compObj["properties"] as JObject;
                        }

                        if (!string.IsNullOrEmpty(typeName))
                            Utils.AddComponentWithProperties(go, typeName, props);
                    }
                }

                // -------- Color --------
                if (data["color"] is JArray colorArr)
                {
                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null && renderer.material != null)
                        renderer.material.color = Utils.ParseColor(colorArr);
                }

                // -------- Final active --------
                if (instantiateInactive && (data["setActive"] == null))
                {
                    go.SetActive(true);
                }
                else if (data["setActive"]?.Type == JTokenType.Boolean)
                {
                    bool isActive = data["setActive"].ToObject<bool>();
                    go.SetActive(isActive);
                }

                return Response.Success($"GameObject '{go.name}' created.", Utils.GetGameObjectData(go));
            }).Result;
        }

        private static bool TryInstantiateByKey(string key, string context, bool instantiateInactive, out GameObject go)
        {
            go = null;

            var locHandle = Addressables.LoadResourceLocationsAsync(key, typeof(GameObject));
            var locs = locHandle.WaitForCompletion();
            Addressables.Release(locHandle);

            if (locs == null || locs.Count == 0)
            {
                Debug.LogWarning($"[Create] No Addressables locations for key '{key}' ({context}).");
                return false;
            }

#if UNITY_EDITOR
            foreach (var l in locs)
                Debug.Log($"[Create] Location: key={l.PrimaryKey}, provider={l.ProviderId}, internalId={l.InternalId} ({context})");
#endif

            try
            {
                var instHandle = Addressables.InstantiateAsync(key);
                var _ = instHandle.WaitForCompletion();

                if (instHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    go = instHandle.Result;
                    if (instantiateInactive && go.activeSelf) go.SetActive(false);
                    return true;
                }
                Debug.LogWarning($"[Create] InstantiateAsync failed for '{key}' ({context}). OpEx: {instHandle.OperationException}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Create] InstantiateAsync threw for '{key}' ({context}). Exception: {ex}");
            }

            try
            {
                var loadHandle = Addressables.LoadAssetAsync<GameObject>(key);
                var prefab = loadHandle.WaitForCompletion();
                if (loadHandle.Status == AsyncOperationStatus.Succeeded && prefab != null)
                {
                    go = UnityEngine.Object.Instantiate(prefab);
                    if (instantiateInactive) go.SetActive(false);
                    Addressables.Release(loadHandle);
                    Debug.Log($"[Create] Fallback succeeded (LoadAsset→Instantiate) for '{key}' ({context}).");
                    return true;
                }
                Debug.LogWarning($"[Create] LoadAssetAsync failed for '{key}' ({context}). OpEx: {loadHandle.OperationException}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Create] LoadAssetAsync threw for '{key}' ({context}). Exception: {ex}");
            }

            return false;
        }
    }
}
