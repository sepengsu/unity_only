// FILE: Assets/Scripts/Functions/GameObject/Find.cs
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
                // ✅ target 필수
                string query = data["target"]?.ToString();
                if (string.IsNullOrWhiteSpace(query))
                    return Response.Error("Missing 'target' parameter.");

                string searchMethod   = data["searchMethod"]?.ToString()?.ToLowerInvariant() ?? "by_name";
                bool findAll          = data["findAll"]?.ToObject<bool?>()          ?? false;
                bool searchInactive   = data["searchInactive"]?.ToObject<bool?>()    ?? true;
                bool searchInChildren = data["searchInChildren"]?.ToObject<bool?>()  ?? false;

                // 검색 풀 구성: 자식 포함 여부에 따라 전체/루트만
                IEnumerable<GameObject> pool = searchInChildren
                    ? GameObject.FindObjectsOfType<GameObject>(searchInactive)
                    : GetRootObjects(searchInactive);

                List<GameObject> results = new();

                switch (searchMethod)
                {
                    case "by_name":
                        results = pool.Where(go => go.name == query).ToList();
                        break;

                    case "by_tag":
                        results = pool.Where(go => SafeCompareTag(go, query)).ToList();
                        break;

                    case "by_layer":
                        if (int.TryParse(query, out int layerIndex))
                            results = pool.Where(go => go.layer == layerIndex).ToList();
                        else
                        {
                            int namedLayer = LayerMask.NameToLayer(query);
                            if (namedLayer != -1)
                                results = pool.Where(go => go.layer == namedLayer).ToList();
                        }
                        break;

                    case "by_component":
                        Type compType = ResolveType(query);
                        if (compType != null)
                            results = pool.Where(go => go.GetComponent(compType) != null).ToList();
                        break;

                    case "by_path":
                        results = new List<GameObject>();
                        foreach (var root in GetRootObjects(searchInactive))
                        {
                            var found = root.transform.Find(query);
                            if (found != null) results.Add(found.gameObject);
                        }
                        break;

                    case "by_id":
                        if (int.TryParse(query, out int id))
                            results = pool.Where(go => go.GetInstanceID() == id).ToList();
                        break;

                    default:
                        return Response.Error($"Unknown searchMethod: '{searchMethod}'");
                }

                if (results.Count == 0)
                    return Response.Success("No matching GameObjects found.", new List<object>());

                if (!findAll && results.Count > 1)
                    results = new List<GameObject> { results[0] };

                var serialized = results.Select(Utils.GetGameObjectData).ToList();
                return Response.Success($"Found {serialized.Count} GameObject(s).", serialized);
            });
        }

        private static bool SafeCompareTag(GameObject go, string tag)
        {
            try { return go.CompareTag(tag); }
            catch { return false; } // 정의되지 않은 태그 예외 방지
        }

        private static Type ResolveType(string nameOrFullName)
        {
            if (string.IsNullOrWhiteSpace(nameOrFullName)) return null;

            // 1) 풀네임 시도
            var t = Type.GetType(nameOrFullName);
            if (t != null) return t;

            // 2) UnityEngine.* 시도
            t = Type.GetType("UnityEngine." + nameOrFullName + ", UnityEngine");
            if (t != null) return t;

            // 3) 모든 어셈블리에서 탐색 (사용자 스크립트 포함)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(nameOrFullName);
                    if (t != null) return t;

                    t = asm.GetTypes().FirstOrDefault(tp => tp.Name == nameOrFullName);
                    if (t != null) return t;
                }
                catch { /* reflection 이슈 무시 */ }
            }
            return null;
        }

        private static List<GameObject> GetRootObjects(bool includeInactive)
        {
            var all = GameObject.FindObjectsOfType<GameObject>(includeInactive);
            var roots = new List<GameObject>();
            foreach (var go in all)
                if (go.transform.parent == null)
                    roots.Add(go);
            return roots;
        }
    }
}
