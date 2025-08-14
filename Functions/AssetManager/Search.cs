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
            string searchPattern = @params["searchPattern"]?.ToString() ?? "*";
            string extensionFilter = @params["filterType"]?.ToString()?.ToLower();
            string pathScope = @params["path"]?.ToString();
            string filterDateAfterStr = @params["filterDateAfter"]?.ToString();
            string orderBy = @params["orderBy"]?.ToString()?.ToLower();
            int? limit = @params["limit"]?.ToObject<int?>();
            int? skip = @params["skip"]?.ToObject<int?>();
            int pageSize = @params["pageSize"]?.ToObject<int?>() ?? 50;
            int pageNumber = @params["pageNumber"]?.ToObject<int?>() ?? 1;

            DateTime? filterDateAfter = null;
            if (!string.IsNullOrEmpty(filterDateAfterStr))
            {
                if (DateTime.TryParse(
                        filterDateAfterStr,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out DateTime parsedDate))
                {
                    filterDateAfter = parsedDate;
                }
            }

            try
            {
                var allResults = new List<JObject>();

                // 1️⃣ Resources/ 디렉토리에서 검색
                string root = Path.Combine(Application.dataPath, "Resources");
                if (!string.IsNullOrEmpty(pathScope))
                    root = Path.Combine(root, pathScope.Replace("\\", "/"));

                if (Directory.Exists(root))
                {
                    var files = Directory.GetFiles(root, $"{searchPattern}.*", SearchOption.AllDirectories)
                        .Where(path =>
                            string.IsNullOrEmpty(extensionFilter) ||
                            Path.GetExtension(path).TrimStart('.').ToLower() == extensionFilter)
                        .Where(path =>
                            !filterDateAfter.HasValue ||
                            File.GetLastWriteTimeUtc(path) > filterDateAfter.Value);

                    foreach (var fullPath in files)
                    {
                        string relative = fullPath.Replace(Application.dataPath + "/Resources/", "")
                                                  .Replace("\\", "/")
                                                  .Replace(".prefab", "")
                                                  .Replace(".mat", "")
                                                  .Replace(".asset", "");

                        allResults.Add(new JObject
                        {
                            ["source"] = "resources",
                            ["path"] = relative,
                            ["name"] = Path.GetFileNameWithoutExtension(fullPath),
                            ["fullPath"] = fullPath,
                            ["lastModified"] = File.GetLastWriteTimeUtc(fullPath).ToString("o"),
                            ["sizeBytes"] = new FileInfo(fullPath).Length
                        });
                    }
                }

                // 2️⃣ StreamingAssets/prefabs.json 에서 가져오기
                string jsonPath = Path.Combine(Application.streamingAssetsPath, "prefabs.json");
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);

                    try
                    {
                        var parsed = JObject.Parse(json);
                        var items = parsed["items"] as JArray;

                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                string path = item["assetPath"]?.ToString();
                                string name = item["name"]?.ToString();
                                string guid = item["guid"]?.ToString();
                                string type = Path.GetExtension(path)?.TrimStart('.').ToLower();

                                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(name))
                                    continue;

                                if (!string.IsNullOrEmpty(extensionFilter) && extensionFilter != type)
                                    continue;

                                if (filterDateAfter.HasValue &&
                                    File.GetLastWriteTimeUtc(path) <= filterDateAfter.Value)
                                    continue;

                                var resultObj = new JObject
                                {
                                    ["source"] = "prefabs.json",
                                    ["path"] = path.Replace("Assets/Resources/", "").Replace(".prefab", ""),
                                    ["name"] = name,
                                    ["fullPath"] = path,
                                    ["guid"] = guid,
                                    ["type"] = type
                                };

                                // 중복 제거
                                if (!allResults.Any(r => r["path"]?.ToString() == resultObj["path"]?.ToString()))
                                    allResults.Add(resultObj);
                            }
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        Debug.LogWarning($"Failed to parse prefabs.json: {jsonEx.Message}");
                    }
                }

                // 3️⃣ 정렬
                if (orderBy == "name")
                    allResults = allResults.OrderBy(r => r["name"]?.ToString()).ToList();
                else if (orderBy == "date")
                    allResults = allResults.OrderByDescending(r =>
                        DateTime.TryParse(r["lastModified"]?.ToString(), out var dt) ? dt : DateTime.MinValue).ToList();

                int totalFound = allResults.Count;

                // 4️⃣ 페이징 / 제한
                if (skip.HasValue)
                    allResults = allResults.Skip(skip.Value).ToList();
                else if (pageNumber > 0 && pageSize > 0)
                    allResults = allResults.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

                if (limit.HasValue)
                    allResults = allResults.Take(limit.Value).ToList();

                return Response.Success(
                    $"Found {totalFound} asset(s). Returned {allResults.Count}.",
                    new JObject
                    {
                        ["totalAssets"] = totalFound,
                        ["returned"] = allResults.Count,
                        ["assets"] = new JArray(allResults)
                    });
            }
            catch (Exception e)
            {
                return Response.Error($"Runtime asset search failed: {e.Message}");
            }
        }
    }
}
