// FILE: Assets/Scripts/Functions/Asset/GetAssetInfo.cs
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;

namespace Functions.AssetManager
{
    public static class GetAssetInfo
    {
        public static object Execute(JObject @params)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string rawPath = @params["path"]?.ToString();
                bool generatePreview = @params["generatePreview"]?.ToObject<bool?>() ?? false; // runtime: ignored

                if (string.IsNullOrWhiteSpace(rawPath))
                    return Response.Error("'path' is required for get_info.");

                // Normalize to Resources-relative path without extension
                string resourcePath = NormalizeResourcesPath(rawPath);

                // Load prefab from Resources (engine/runtime only)
                GameObject prefab = Resources.Load<GameObject>(resourcePath);
                if (prefab == null)
                    return Response.Error($"Resource not found at '{resourcePath}' (under a Resources folder).");

                var components = prefab.GetComponents<Component>()
                    .Select(c => c != null ? c.GetType().FullName : "MissingComponent")
                    .ToList();

                var info = new JObject
                {
                    ["name"] = prefab.name,
                    ["path"] = resourcePath,       // Resources-relative path (no extension)
                    ["type"] = "GameObject",
                    ["componentTypes"] = JArray.FromObject(components),
                };

                // Note: generatePreview is Editor-only (AssetPreview). Ignored at runtime.
                return Response.Success("Asset info retrieved.", info);
            });
        }

        private static string NormalizeResourcesPath(string input)
        {
            // unify slashes & trim
            string p = input.Replace("\\", "/").Trim().TrimStart('/');

            // strip extension if provided
            if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(0, p.Length - ".prefab".Length);

            // strip leading Resources roots
            const string assetsRes = "assets/resources/";
            const string resources = "resources/";
            string pl = p.ToLowerInvariant();
            if (pl.StartsWith(assetsRes))
                p = p.Substring(assetsRes.Length);
            else if (pl.StartsWith(resources))
                p = p.Substring(resources.Length);

            return p;
        }
    }
}
