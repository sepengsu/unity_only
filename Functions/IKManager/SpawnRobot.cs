using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;

namespace Functions.IKManager
{
    public static class SpawnRobot
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                try
                {
                    string prefabPath = data["prefab_path"]?.ToString() ?? data["prefabPath"]?.ToString();
                    string name       = data["name"]?.ToString() ?? "Robot01";
                    bool   ensureEnv  = data["ensureEnvFirst"]?.ToObject<bool>() ?? true;

                    if (string.IsNullOrWhiteSpace(prefabPath))
                        return Response.Error("[IK.SpawnRobot] Missing 'prefab_path'");

                    if (ensureEnv)
                    {
                        if (!G4AHelper.EnsureControllerPresent(out var src, out var msg))
                            return Response.Error(msg ?? "Failed to ensure G4A controller before spawn.");
                        Debug.Log($"[IK.SpawnRobot] Controller ensured via {src}");
                    }

                    var createPayload = new JObject
                    {
                        ["action"] = "create",
                        ["name"] = name,
                        ["prefab_path"] = prefabPath,
                        ["instantiateInactive"] = data["instantiateInactive"] ?? true,
                        ["position"] = data["position"] ?? new JArray(0,0,0),
                        ["rotation"] = data["rotation"] ?? new JArray(0,0,0),
                        ["scale"]    = data["scale"]    ?? new JArray(1,1,1),
                        ["parent"]   = data["parent"],
                        ["tag"]      = data["tag"],
                        ["layer"]    = data["layer"]
                    };

                    return Functions.GameObjectManager.Create.Execute(createPayload);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[IK.SpawnRobot] Exception: {ex}");
                    return Response.Error($"IK.SpawnRobot exception: {ex.Message}");
                }
            }).Result;
        }
    }
}
