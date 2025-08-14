using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;

namespace Functions.IKManager
{
    public static class PathRun
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                try
                {
                    string robotName = data["robot"]?.ToString();
                    string pathName  = data["pathName"]?.ToString();

                    if (string.IsNullOrWhiteSpace(robotName))
                        return Error("[IK.PathRun] Missing 'robot'");
                    if (string.IsNullOrWhiteSpace(pathName))
                        return Error("[IK.PathRun] Missing 'pathName'");

                    // find robot
                    GameObject robotGo = GameObject.Find(robotName);
                    if (robotGo == null)
                        robotGo = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == robotName);
                    if (robotGo == null)
                        return Error($"[IK.PathRun] Robot '{robotName}' not found.");

                    // find path
                    Transform pathTr = robotGo.transform.Find(pathName);
                    if (pathTr == null)
                        return Error($"[IK.PathRun] Path '{pathName}' not found under '{robotName}'.");

                    var pathGo = pathTr.gameObject;

                    // get IKPath component
                    Component ikPath =
                        pathGo.GetComponent("IKPath") ??
                        GetByTypeName(pathGo, "IKPath");
                    if (ikPath == null)
                        return Error("[IK.PathRun] IKPath component not found on path object.");

                    // Try common start methods
                    bool started = TryInvokeAny(ikPath,
                        "StartPath", "Run", "Play", "Execute", "Start");

                    if (!started)
                    {
                        // fallback: enable component or set a 'Active/Running' flag if exists
                        bool toggled = TrySetAny(ikPath, true, "Active", "IsActive", "Running", "IsRunning");
                        if (!toggled)
                            return Error("[IK.PathRun] Could not start IKPath (no known methods/properties).");
                    }

                    return Response.Success($"IK Path '{pathGo.name}' started.", new
                    {
                        robot = robotGo.name,
                        path  = pathGo.name,
                        started = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[IK.PathRun] Exception: {ex}");
                    return Error($"IK.PathRun exception: {ex.Message}");
                }
            }).Result;
        }

        // ------- helpers -------
        private static object Error(string message) =>
            new { success = false, error = message };

        private static Component GetByTypeName(GameObject go, string typeName)
        {
            var t = FindTypeByName(typeName);
            return (t != null) ? go.GetComponent(t) : null;
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
                catch { /* ignore */ }
            }
            return null;
        }

        private static bool TryInvokeAny(Component comp, params string[] methodNames)
        {
            var t = comp.GetType();
            foreach (var m in methodNames)
            {
                var mi = t.GetMethod(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (mi == null) continue;
                try { mi.Invoke(comp, null); return true; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[IK.PathRun] Invoke {t.Name}.{mi.Name} failed: {ex.Message}");
                }
            }
            return false;
        }

        private static bool TrySetAny(Component comp, object value, params string[] memberNames)
        {
            var t = comp.GetType();

            // properties
            foreach (var n in memberNames)
            {
                var pi = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi == null || !pi.CanWrite) continue;
                try { pi.SetValue(comp, ConvertValue(value, pi.PropertyType)); return true; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[IK.PathRun] Set property {t.Name}.{pi?.Name} failed: {ex.Message}");
                }
            }

            // fields
            foreach (var n in memberNames)
            {
                var fi = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi == null) continue;
                try { fi.SetValue(comp, ConvertValue(value, fi.FieldType)); return true; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[IK.PathRun] Set field {t.Name}.{fi?.Name} failed: {ex.Message}");
                }
            }

            return false;
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
            catch { /* ignore */ }

            try { return Convert.ChangeType(value, targetType); }
            catch { return value; }
        }
    }
}
