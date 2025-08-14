using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;

namespace Functions.IKManager
{
    public static class PathAddTarget
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                try
                {
                    string robotName  = data["robot"]?.ToString();
                    string pathName   = data["pathName"]?.ToString();
                    var    targetObj  = data["target"] as JObject;

                    if (string.IsNullOrWhiteSpace(robotName)) return Response.Error("[IK.PathAddTarget] Missing 'robot'");
                    if (string.IsNullOrWhiteSpace(pathName))  return Response.Error("[IK.PathAddTarget] Missing 'pathName'");
                    if (targetObj == null)                    return Response.Error("[IK.PathAddTarget] Missing 'target' object");

                    string targetName = targetObj["name"]?.ToString() ?? "Target";
                    string mode       = (targetObj["mode"]?.ToString() ?? "PTP").ToUpperInvariant();
                    if (mode != "PTP" && mode != "LIN")
                        return Response.Error($"[IK.PathAddTarget] Invalid mode '{mode}'. Use 'PTP' or 'LIN'.");

                    Vector3 tcp   = ParseV3(targetObj["tcp"] as JArray, Vector3.zero);
                    Vector3 euler = ParseV3(targetObj["euler"] as JArray, Vector3.zero);

                    float[] axisCorr = null;
                    if (targetObj["axisCorrection"] is JArray ac)
                        axisCorr = ac.Select(t => (float)t).ToArray();
                    bool? turnCorr = targetObj["turnCorrection"]?.ToObject<bool?>();

                    GameObject robotGo = GameObject.Find(robotName)
                        ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == robotName);
                    if (robotGo == null) return Response.Error($"[IK.PathAddTarget] Robot '{robotName}' not found.");

                    Transform pathTr = robotGo.transform.Find(pathName);
                    GameObject pathGo = pathTr ? pathTr.gameObject : new GameObject(pathName);
                    if (!pathTr) pathGo.transform.SetParent(robotGo.transform, false);

                    var targetGo = new GameObject(targetName);
                    targetGo.transform.SetParent(pathGo.transform, false);
                    targetGo.transform.localPosition = tcp;
                    targetGo.transform.localEulerAngles = euler;

                    var targetComp =
                        EnsureComponentByName(targetGo, "Target") ??
                        EnsureComponentByName(targetGo, "IKTarget") ??
                        EnsureComponentByName(targetGo, "RobotTarget");

                    if (targetComp != null)
                    {
                        bool isLinear = mode == "LIN";
                        TrySetProp(targetComp, isLinear, "Linear", "IsLinear", "linear", "isLinear", "UseLinear", "useLinear");
                        TrySetProp(targetComp, tcp,   "TCP", "Position", "TargetPosition", "tcp", "position");
                        TrySetProp(targetComp, euler, "RotationEuler", "Euler", "TargetEuler", "rotationEuler", "euler");
                        if (axisCorr != null)
                            TrySetProp(targetComp, axisCorr, "AxisCorrection", "axisCorrection", "AxisCorr");
                        if (turnCorr.HasValue)
                            TrySetProp(targetComp, turnCorr.Value, "TurnCorrection", "turnCorrection", "UseTurnCorrection");
                    }

                    return Response.Success($"IK Target '{targetGo.name}' added to '{pathGo.name}'.", new
                    {
                        robot = robotGo.name,
                        path  = pathGo.name,
                        target = targetGo.name,
                        targetInstanceID = targetGo.GetInstanceID(),
                        linear = (mode == "LIN")
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[IK.PathAddTarget] Exception: {ex}");
                    return Response.Error($"IK.PathAddTarget exception: {ex.Message}");
                }
            }).Result;
        }

        private static Vector3 ParseV3(JArray arr, Vector3 fallback)
        {
            if (arr == null || arr.Count == 0) return fallback;
            float x = (arr.Count > 0) ? (float)arr[0] : 0f;
            float y = (arr.Count > 1) ? (float)arr[1] : 0f;
            float z = (arr.Count > 2) ? (float)arr[2] : 0f;
            return new Vector3(x, y, z);
        }

        private static Component EnsureComponentByName(GameObject go, string typeName)
        {
            if (go == null || string.IsNullOrWhiteSpace(typeName)) return null;
            var existing = go.GetComponent(typeName);
            if (existing != null) return existing;

            var t = FindTypeByName(typeName);
            if (t == null) return null;

            try { return go.AddComponent(t); }
            catch { return null; }
        }

        private static Type FindTypeByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var t = Type.GetType(name);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetTypes().FirstOrDefault(x => x.Name == name || x.FullName == name);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        private static void TrySetProp(Component comp, object value, params string[] candidateNames)
        {
            if (comp == null || candidateNames == null || candidateNames.Length == 0) return;
            var t = comp.GetType();

            foreach (var n in candidateNames)
            {
                var pi = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi == null || !pi.CanWrite) continue;
                try { pi.SetValue(comp, ConvertValue(value, pi.PropertyType)); return; }
                catch { }
            }
            foreach (var n in candidateNames)
            {
                var fi = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi == null) continue;
                try { fi.SetValue(comp, ConvertValue(value, fi.FieldType)); return; }
                catch { }
            }
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            var vt = value.GetType();
            if (targetType.IsAssignableFrom(vt)) return value;

            try
            {
                if (targetType == typeof(bool))   return Convert.ToBoolean(value);
                if (targetType == typeof(float))  return Convert.ToSingle(value);
                if (targetType == typeof(double)) return Convert.ToDouble(value);
                if (targetType == typeof(int))    return Convert.ToInt32(value);
                if (targetType == typeof(string)) return Convert.ToString(value);
            }
            catch { }
            try { return Convert.ChangeType(value, targetType); } catch { return value; }
        }
    }
}
