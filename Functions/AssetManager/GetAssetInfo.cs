using System;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Linq;
using UnityEngine;
using Helpers;

namespace Functions.AssetManager
{
    public static class GetAssetInfo
    {
        public static object Execute(JObject @params)
        {
            string path = @params["path"]?.ToString();
            bool generatePreview = @params["generatePreview"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for get_info.");

            string fullPath = Path.Combine(Application.dataPath, "Resources", path) + ".prefab";
            if (!File.Exists(fullPath))
                return Response.Error($"Asset not found at path: {fullPath}");

            try
            {
                // Try to load the object
                string resourcePath = path.Replace("\\", "/").Replace(".prefab", "");
                GameObject asset = Resources.Load<GameObject>(resourcePath);
                if (asset == null)
                    return Response.Error($"Failed to load asset at Resources/{resourcePath}");

                var components = asset.GetComponents<Component>()
                    .Select(c => c.GetType().FullName)
                    .ToList();

                var info = new JObject
                {
                    ["name"] = asset.name,
                    ["path"] = path,
                    ["type"] = "GameObject",
                    ["componentTypes"] = JArray.FromObject(components),
                    ["lastModified"] = File.GetLastWriteTimeUtc(fullPath).ToString("o"),
                    ["sizeBytes"] = new FileInfo(fullPath).Length
                };

                return Response.Success("Asset info retrieved.", info);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting info for asset '{path}': {e.Message}");
            }
        }
    }
}
