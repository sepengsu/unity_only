using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Helpers;
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.PathManager
{
    public static class StartPath
    {
        public static object Execute(JObject data)
        {
            try
            {
                return MainThreadDispatcher.Run(() =>
                {
                    var runId = $"RUN#{DateTime.Now:HHmmss.fff}-{Guid.NewGuid().ToString("N").Substring(0,6)}";
                    Debug.LogWarning($"[StartPath] {runId} ENTER Execute()");

                    // --- action/json ---
                    string action = data?["action"]?.ToString();
                    if (!string.Equals(action, "start_path", StringComparison.OrdinalIgnoreCase))
                        return Err(runId, "[StartPath] Invalid or missing action. Expected 'start_path'.");

                    string json = data?["json"]?.ToString();
                    if (string.IsNullOrWhiteSpace(json))
                        return Err(runId, "[StartPath] Missing 'json' (string).");

                    Debug.Log($"[StartPath] {runId} Raw JSON: {json}");

                    JObject payload;
                    try { payload = JObject.Parse(json); }
                    catch (Exception e) { return Err(runId, $"[StartPath] JSON parse error: {e.Message}"); }

                    string robotName = payload["robot"]?.ToString();
                    string pathName  = payload["path"]?.ToString();
                    bool?  loopOpt   = (payload["loop"] != null && payload["loop"].Type == JTokenType.Boolean)
                                        ? payload["loop"].ToObject<bool>() : (bool?)null;

                    Debug.Log($"[StartPath] {runId} Parsed -> robot:'{robotName}', path:'{pathName}', loop:{(loopOpt.HasValue ? loopOpt.Value.ToString() : "null")}");

                    if (string.IsNullOrWhiteSpace(robotName)) return Err(runId, "[StartPath] JSON must contain non-empty 'robot'.");
                    if (string.IsNullOrWhiteSpace(pathName))  return Err(runId, "[StartPath] JSON must contain non-empty 'path'.");

                    if (!Application.isPlaying)                 return Err(runId, "[StartPath] Not in Play mode.");
                    if (Mathf.Approximately(Time.timeScale, 0f)) return Err(runId, "[StartPath] Time.timeScale is 0.");

                    // --- find robot/path ---
                    GameObject robotGO = FindGOAllScenes(robotName);
                    if (robotGO == null) return Err(runId, $"[StartPath] Robot '{robotName}' not found.");

                    Transform pathTF = robotGO.transform.GetComponentsInChildren<Transform>(true)
                                            .FirstOrDefault(t => t.name == pathName);
                    if (pathTF == null) return Err(runId, $"[StartPath] Path '{pathName}' not found under '{robotName}'.");

                    Debug.Log($"[StartPath] {runId} Robot='{robotGO.name}', Path='{pathTF.name}'");

                    // --- IKPath type & component ---
                    var ikPathType = FindTypeBySimpleName("IKPath")
                                     ?? Type.GetType("game4automation.IKPath")
                                     ?? Type.GetType("realvirtual.IKPath");
                    if (ikPathType == null) return Err(runId, "[StartPath] IKPath type not found.");

                    var ikPathComp = pathTF.GetComponent(ikPathType);
                    if (ikPathComp == null) return Err(runId, $"[StartPath] IKPath component not found on '{pathName}'.");
                    Debug.Log($"[StartPath] {runId} IKPath component: {ikPathComp.GetType().FullName}");

                    // --- loop option (if provided only) ---
                    if (loopOpt.HasValue)
                    {
                        bool l1 = TryAssign(ikPathComp, "LoopPath", loopOpt.Value);
                        bool l2 = TryAssign(ikPathComp, "loopPath", loopOpt.Value);
                        bool l3 = TryAssign(ikPathComp, "Loop",     loopOpt.Value);
                        Debug.Log($"[StartPath] {runId} Loop set -> LoopPath:{l1}, loopPath:{l2}, Loop:{l3} ({loopOpt.Value})");
                    }

                    // --- target list: scan & log ---
                    IList pathList = GetPathList(ikPathComp, ikPathType);
                    if (pathList == null) return Err(runId, "[StartPath] IKPath.Path list not found or null.");
                    Debug.Log($"[StartPath] {runId} IKPath.Path count = {pathList.Count}");

                    for (int i = 0; i < pathList.Count; i++)
                    {
                        var c = pathList[i] as Component;
                        if (c == null) { Debug.LogWarning($"[StartPath] {runId}  - [{i}] <null>"); continue; }

                        // Reachable & WaitForSignal (best-effort)
                        bool reachable = GetBoolDyn(c, "Reachable");
                        bool wait      = GetBoolDyn(c, "WaitForSignal");

                        Debug.Log($"[StartPath] {runId}  - [{i}] {c.name} (Reachable:{reachable}, WaitForSignal:{wait})");
                    }

                    // --- press the 'Start Path' button: call startPath() ---
                    var startMi = ikPathType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                             .FirstOrDefault(m => m.GetParameters().Length == 0 &&
                                                                  string.Equals(m.Name, "startPath", StringComparison.OrdinalIgnoreCase));
                    if (startMi == null)
                    {
                        bool anyAssigned = TryAssign(ikPathComp, "StartPath", true)
                                         | TryAssign(ikPathComp, "startPath", true)
                                         | TryAssign(ikPathComp, "Start",     true);
                        Debug.Log($"[StartPath] {runId} startPath() missing; tried flags -> {anyAssigned}");
                        if (!anyAssigned) return Err(runId, "[StartPath] No startPath() method or start flag found on IKPath.");
                    }
                    else
                    {
                        try { startMi.Invoke(ikPathComp, null); Debug.Log($"[StartPath] {runId} IKPath.startPath() invoked."); }
                        catch (TargetInvocationException tie)
                        {
                            var inner = tie.InnerException;
                            return Err(runId, $"[StartPath] startPath() threw: {inner?.GetType().Name} - {inner?.Message}");
                        }
                    }

                    // --- attach watcher: auto-deactivate when finished ---
                    var watcher = pathTF.gameObject.AddComponent<PathRunWatcher>();
                    watcher.IkPathComp = (Component)ikPathComp;
                    watcher.IkPathType = ikPathType;
                    watcher.RunId      = runId;

                    string msg = $"Started IKPath on '{robotName}/{pathName}' ({runId}).";
                    var dataObj = Utils.GetGameObjectData(pathTF.gameObject);
                    Debug.LogWarning($"[StartPath] {runId} SUCCESS");
                    return Response.Success(msg, dataObj);
                }).Result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StartPath] OUTER EXCEPTION: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                return Response.Error($"[StartPath] Unhandled exception: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ---------------- Watcher (auto deactivate after done) ----------------
        class PathRunWatcher : MonoBehaviour
        {
            [NonSerialized] public Component IkPathComp;
            [NonSerialized] public Type      IkPathType;
            [NonSerialized] public string    RunId;

            void OnEnable()
            {
                if (IkPathComp == null || IkPathType == null) { enabled = false; return; }
                StartCoroutine(MonitorRoutine());
            }

            IEnumerator MonitorRoutine()
            {
                // 한 프레임 양보
                yield return null;

                // 대상 목록 한 번 더 로깅
                var list = GetPathList(IkPathComp, IkPathType);
                Debug.Log($"[PathRunWatcher] {RunId} monitor start. Path count={list?.Count ?? -1}");

                while (true)
                {
                    bool finished = GetBool(IkPathComp, IkPathType, "PathIsFinished");
                    bool active   = GetBool(IkPathComp, IkPathType, "PathIsActive");

                    if (finished || !active)
                        break;

                    yield return new WaitForSeconds(0.05f);
                }

                Debug.Log($"[PathRunWatcher] {RunId} finished/inactive detected → deactivate");

                // stop/reset (best-effort)
                TryInvoke(IkPathComp, IkPathType, "stopPath");
                TryInvoke(IkPathComp, IkPathType, "StopPath");
                TryInvoke(IkPathComp, IkPathType, "Stop");
                TryInvoke(IkPathComp, IkPathType, "Reset");

                // flags off
                TryAssign(IkPathComp, IkPathType, "StartPath", false);
                TryAssign(IkPathComp, IkPathType, "startPath", false);
                TryAssign(IkPathComp, IkPathType, "start",     false);
                TryAssign(IkPathComp, IkPathType, "LoopPath",  false);
                TryAssign(IkPathComp, IkPathType, "loopPath",  false);
                TryAssign(IkPathComp, IkPathType, "Loop",      false);

                // Active -> Never (fallback: bool false)
                if (!TrySetEnumByName(IkPathComp, IkPathType, "Active", "Never"))
                    TryAssign(IkPathComp, IkPathType, "Active", false);

                Debug.LogWarning($"[PathRunWatcher] {RunId} IKPath Active=Never (or false) set. Done.");
                Destroy(this);
            }
        }

        // ---------------- Helpers ----------------

        private static GameObject FindGOAllScenes(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                foreach (var root in sc.GetRootGameObjects())
                {
                    if (root.name == name) return root;
                    var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
                    if (t != null) return t.gameObject;
                }
            }
            return null;
        }

        private static Type FindTypeBySimpleName(string name)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = null;
                    try { t = asm.GetTypes().FirstOrDefault(x => x.Name == name); }
                    catch { }
                    if (t != null) return t;
                }
            }
            catch { }
            return null;
        }

        private static bool TryAssign(object comp, string fieldOrProp, object value)
        {
            if (comp == null) return false;
            var type = comp.GetType();
            var vtype = value?.GetType();

            var field = type.GetField(fieldOrProp, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && (vtype == null || field.FieldType.IsAssignableFrom(vtype)))
            { field.SetValue(comp, value); return true; }

            var prop = type.GetProperty(fieldOrProp, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite && (vtype == null || prop.PropertyType.IsAssignableFrom(vtype)))
            { prop.SetValue(comp, value, null); return true; }

            return false;
        }

        private static bool GetBool(object comp, Type t, string member)
        {
            if (comp == null) return false;

            var f = t.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(comp);

            var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanRead && p.PropertyType == typeof(bool)) return (bool)p.GetValue(comp, null);

            return false;
        }

        private static bool GetBoolDyn(Component c, string member)
        {
            if (c == null) return false;
            var t = c.GetType();

            var f = t.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(c);

            var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanRead && p.PropertyType == typeof(bool)) return (bool)p.GetValue(c, null);

            return false;
        }

        private static IList GetPathList(object ikPathComp, Type ikPathType)
        {
            try
            {
                IList list = null;
                var f = ikPathType.GetField("Path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) list = f.GetValue(ikPathComp) as IList;
                if (list == null)
                {
                    var p = ikPathType.GetProperty("Path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null) list = p.GetValue(ikPathComp) as IList;
                }
                return list;
            }
            catch { return null; }
        }

        private static bool TryInvoke(object comp, Type t, string method)
        {
            var m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (m == null) return false;
            try { m.Invoke(comp, null); return true; } catch { return false; }
        }

        private static bool TryAssign(object comp, Type t, string member, object value)
        {
            if (comp == null) return false;
            var vtype = value?.GetType();

            var field = t.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && (vtype == null || field.FieldType.IsAssignableFrom(vtype)))
            { field.SetValue(comp, value); return true; }

            var prop = t.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite && (vtype == null || prop.PropertyType.IsAssignableFrom(vtype)))
            { prop.SetValue(comp, value, null); return true; }

            return false;
        }

        private static bool TrySetEnumByName(object comp, Type t, string member, string enumName)
        {
            var f = t.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType.IsEnum)
            {
                foreach (var v in Enum.GetValues(f.FieldType))
                    if (string.Equals(v.ToString(), enumName, StringComparison.OrdinalIgnoreCase))
                    { f.SetValue(comp, v); return true; }
            }

            var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite && p.PropertyType.IsEnum)
            {
                foreach (var v in Enum.GetValues(p.PropertyType))
                    if (string.Equals(v.ToString(), enumName, StringComparison.OrdinalIgnoreCase))
                    { p.SetValue(comp, v, null); return true; }
            }
            return false;
        }

        private static object Err(string runId, string msg)
        {
            Debug.LogError($"{msg} ({runId})");
            return Response.Error($"{msg} ({runId})");
        }
    }
}
