// FILE: Assets/Scripts/Functions/Asset/Search.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;

namespace Functions.AssetManager
{
    public static class Search
    {
        public static object Execute(JObject @params)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string searchPattern     = @params["searchPattern"]?.ToString() ?? "*";
                string extensionFilter   = @params["filterType"]?.ToString()?.ToLowerInvariant();
                string pathScope         = @params["path"]?.ToString();
                string filterDateAfterStr= @params["filterDateAfter"]?.ToString();
                string orderBy           = @params["orderBy"]?.ToString()?.ToLowerInvariant();
                int?   limit             = @params["limit"]?.ToObject<int?>();
                int?   skip              = @params["skip"]?.ToObject<int?>();
                int    pageSize          = @params["pageSize"]?.ToObject<int?>() ?? 50;
                int    pageNumber        = @params["pageNumber"]?.ToObject<int?>() ?? 1;

                DateTime? filterDateAfter = null;
                if (!string.IsNullOrEmpty(filterDateAfterStr) &&
                    DateTime.TryParse(filterDateAfterStr, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out DateTime parsedDate))
                {
                    filterDateAfter = parsedDate;
                }

                try
                {
                    var allResults = new List<JObject>();

                    // Runtime-only: use StreamingAssets/prefabs.json index
                    string jsonPath = Path.Combine(Application.streamingAssetsPath, "prefabs.json");
                    if (File.Exists(jsonPath))
                    {
                        try
                        {
                            var parsed = JObject.Parse(File.ReadAllText(jsonPath));
                            var items = parsed["items"] as JArray;

                            if (items != null)
                            {
                                foreach (var item in items.OfType<JObject>())
                                {
                                    string assetPath = item["assetPath"]?.ToString(); // e.g., "Assets/Resources/Prefabs/Foo.prefab"
                                    string name      = item["name"]?.ToString();
                                    string guid      = item["guid"]?.ToString();
                                    string lastModStr= item["lastModified"]?.ToString(); // optional

                                    if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(name))
                                        continue;

                                    // scope filter
                                    if (!string.IsNullOrWhiteSpace(pathScope) &&
                                        assetPath.IndexOf(pathScope, StringComparison.OrdinalIgnoreCase) < 0)
                                        continue;

                                    // type / extension filter
                                    string type = Path.GetExtension(assetPath)?.TrimStart('.').ToLowerInvariant();
                                    if (!string.IsNullOrEmpty(extensionFilter) && extensionFilter != type)
                                        continue;

                                    // name pattern filter
                                    string pat = NormalizePattern(searchPattern);
                                    if (!string.IsNullOrEmpty(pat) &&
                                        name.IndexOf(pat, StringComparison.OrdinalIgnoreCase) < 0)
                                        continue;

                                    // date filter (if lastModified present)
                                    if (filterDateAfter.HasValue && !string.IsNullOrEmpty(lastModStr) &&
                                        DateTime.TryParse(lastModStr, out var lm) && lm <= filterDateAfter.Value)
                                        continue;

                                    var resPath = assetPath.Replace("Assets/Resources/", "", StringComparison.OrdinalIgnoreCase);
                                    resPath = StripKnownExtensions(resPath);

                                    var resultObj = new JObject
                                    {
                                        ["source"]   = "prefabs.json",
                                        ["path"]     = resPath,        // Resources-relative (no extension)
                                        ["name"]     = name,
                                        ["fullPath"] = assetPath,
                                        ["guid"]     = guid
                                    };
                                    if (!string.IsNullOrEmpty(lastModStr)) resultObj["lastModified"] = lastModStr;

                                    // de-dup by path
                                    if (!allResults.Any(r => string.Equals(r["path"]?.ToString(), resPath, StringComparison.OrdinalIgnoreCase)))
                                        allResults.Add(resultObj);
                                }
                            }
                        }
                        catch (Exception jsonEx)
                        {
                            Debug.LogWarning($"[Search] Failed to parse prefabs.json: {jsonEx.Message}");
                        }
                    }

                    // Order
                    if (orderBy == "name")
                        allResults = allResults.OrderBy(r => r["name"]?.ToString()).ToList();
                    else if (orderBy == "date")
                        allResults = allResults.OrderByDescending(r =>
                        {
                            var s = r["lastModified"]?.ToString();
                            return DateTime.TryParse(s, out var dt) ? dt : DateTime.MinValue;
                        }).ToList();

                    int totalFound = allResults.Count;

                    // Paging / limit
                    if (skip.HasValue) allResults = allResults.Skip(skip.Value).ToList();
                    else if (pageNumber > 0 && pageSize > 0)
                        allResults = allResults.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

                    if (limit.HasValue) allResults = allResults.Take(limit.Value).ToList();

                    return Response.Success(
                        $"Found {totalFound} asset(s). Returned {allResults.Count}.",
                        new JObject
                        {
                            ["totalAssets"] = totalFound,
                            ["returned"]    = allResults.Count,
                            ["assets"]      = new JArray(allResults)
                        });
                }
                catch (Exception e)
                {
                    return Response.Error($"Runtime asset search failed: {e.Message}");
                }
            });
        }

        // -------- helpers --------

        private static string NormalizePattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || pattern == "*") return "";
            // very simple wildcard support: treat '*' as "contains"
            return pattern.Replace("*", "").Trim();
        }

        private static string StripKnownExtensions(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string p = path;
            foreach (var ext in new[] { ".prefab", ".mat", ".asset", ".fbx", ".png", ".jpg", ".jpeg", ".tga", ".wav", ".mp3" })
            {
                if (p.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    p = p.Substring(0, p.Length - ext.Length);
            }
            return p.Replace("\\", "/");
        }
    }
}
