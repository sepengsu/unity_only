using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;

namespace Functions.IKManager
{
    public static class EnsureEnv
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                try
                {
                    if (G4AHelper.EnsureControllerPresent(out var source, out var msg))
                    {
                        return Response.Success("Environment ensured (Game4AutomationController present).",
                            new { source });
                    }
                    return Response.Error(msg ?? "Failed to ensure Game4AutomationController.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[IK.EnsureEnv] Exception: {ex}");
                    return Response.Error($"IK.EnsureEnv exception: {ex.Message}");
                }
            }).Result;
        }
    }
}
