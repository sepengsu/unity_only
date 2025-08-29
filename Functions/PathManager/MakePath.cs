using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Helpers;
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.PathManager
{
    [DisallowMultipleComponent]
    public class PathMeta : MonoBehaviour
    {
        public string robotName;
        public string pathName;
        public bool startFlag;
        [TextArea] public string note = "This object only holds IKPath. No execution logic.";
    }

    public static class MakePath
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                // 0) action
                string action = data?["action"]?.ToString();
                if (!string.Equals(action, "make_path", StringComparison.OrdinalIgnoreCase))
                    return Response.Error("[MakePath] Invalid or missing action. Expected 'make_path'.");

                // 1) json
                string json = data?["json"]?.ToString();
                if (string.IsNullOrWhiteSpace(json))
                    return Response.Error("[MakePath] Missing 'json' (string).");

                JObject payload;
                try { payload = JObject.Parse(json); }
                catch (Exception e) { return Response.Error($"[MakePath] JSON parse error: {e.Message}"); }

                string robotName = payload["robot"]?.ToString();
                string pathName  = payload["name"]?.ToString();
                bool   startFlag = payload["start"]?.Type == JTokenType.Boolean && payload["start"]!.ToObject<bool>();

                if (string.IsNullOrWhiteSpace(robotName))
                    return Response.Error("[MakePath] JSON must contain non-empty 'robot' (robot GameObject name).");
                if (string.IsNullOrWhiteSpace(pathName))
                    return Response.Error("[MakePath] JSON must contain non-empty 'name' (Path GameObject name).");

                // 2) find robot
                GameObject robotGO = GameObject.Find(robotName);
                if (robotGO == null)
                {
                    var scene = SceneManager.GetActiveScene();
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        var t = root.transform.Find(robotName);
                        if (t != null) { robotGO = t.gameObject; break; }
                        var found = root.GetComponentsInChildren<Transform>(true)
                                        .FirstOrDefault(x => x.name == robotName);
                        if (found != null) { robotGO = found.gameObject; break; }
                    }
                }
                if (robotGO == null)
                    return Response.Error($"[MakePath] Robot '{robotName}' not found in scene.");

                // 3) create path GO
                string safePathName = string.IsNullOrWhiteSpace(pathName) ? "Path" : pathName.Trim();
                var pathGO = new GameObject(safePathName);
                pathGO.transform.SetParent(robotGO.transform, false);
                pathGO.transform.localPosition = Vector3.zero;
                pathGO.transform.localRotation = Quaternion.identity;
                pathGO.transform.localScale    = Vector3.one;

                var meta = pathGO.AddComponent<PathMeta>();
                meta.robotName = robotName;
                meta.pathName  = safePathName;
                meta.startFlag = startFlag;

                // 4) IKPath add
                var ikPathType = FindTypeBySimpleName("IKPath")
                                 ?? Type.GetType("game4automation.IKPath")
                                 ?? Type.GetType("realvirtual.IKPath");
                if (ikPathType == null || !typeof(Component).IsAssignableFrom(ikPathType))
                    return Response.Error("[MakePath] IKPath type not found. Ensure the package is imported.");

                var ikPathComp = pathGO.AddComponent(ikPathType);

                // 5) wire refs (inspector 기본과 동일하게)
                TryAssign(ikPathComp, "robot",     robotGO.transform);
                TryAssign(ikPathComp, "robotRoot", robotGO.transform);
                TryAssign(ikPathComp, "root",      robotGO.transform);

                // 중요: 타겟 루트는 pathGO 기준
                TryAssign(ikPathComp, "targetRoot",  pathGO.transform);
                TryAssign(ikPathComp, "TargetRoot",  pathGO.transform);
                TryAssign(ikPathComp, "targetsRoot", pathGO.transform);

                // 표시/옵션 (가능할 때만 적용)
                TrySetEnumByName(ikPathComp, "Active", "Always");
                TryAssign(ikPathComp, "DebugPath",   true);
                TryAssign(ikPathComp, "DrawPath",    true);
                TryAssign(ikPathComp, "DrawTargets", true);

                // SpeedOverride 타입이 float/double/int 중 무엇이든 대응
                bool speedSet = TryAssign(ikPathComp, "SpeedOverride", 1f)
                             || TryAssign(ikPathComp, "SpeedOverride", 1d)
                             || TryAssign(ikPathComp, "SpeedOverride", 1);

                // TCP 관련 기본값
                TryAssign(ikPathComp, "SetNewTCP", false);
                TryAssign(ikPathComp, "SetNewTcp", false);
                TryAssign(ikPathComp, "TCP", null);

                // 시작/루프 꺼두기
                TryAssign(ikPathComp, "autoStart", false);
                TryAssign(ikPathComp, "start",     false);
                TryAssign(ikPathComp, "StartPath", false);
                TryAssign(ikPathComp, "LoopPath",  false);
                TryAssign(ikPathComp, "Loop",      false);

                // Start conditions/On Path End None
                TryAssign(ikPathComp, "SignalStart",    null);
                TryAssign(ikPathComp, "SignalIsStarted",null);
                TryAssign(ikPathComp, "SignalEnded",    null);
                TryAssign(ikPathComp, "StartNextPath",  null);

                // 6) find RobotIK strictly by name
                Component robotIKComp = robotGO.GetComponentsInChildren<Component>(true)
                                        .FirstOrDefault(c => c != null && c.GetType().Name == "RobotIK");
                Type robotIKType = robotIKComp?.GetType();
                if (robotIKType == null || robotIKComp == null)
                    return Response.Error("[MakePath] RobotIK component not found under the robot. Place a RobotIK first.");

                // 7) wire RobotIK
                bool wired = TryAssignAny(ikPathComp, robotIKComp, "RobotIK", "robotIK");
                if (!wired)
                    return Response.Error("[MakePath] IKPath has no compatible 'RobotIK' field/property.");

                // 8) Axis sanity
                var axisField = robotIKType.GetField("Axis", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var axisArr   = axisField?.GetValue(robotIKComp) as Array;
                if (axisArr == null || axisArr.Length == 0)
                    return Response.Error("[MakePath] RobotIK.Axis is empty. Assign 6 Drive components (J1~J6) in RobotIK.");

                // 9) stabilize (존재하는 메서드만 호출)
                foreach (var name in new[] { "Reset", "Rebuild", "RebuildPath", "Refresh", "RefreshTargets", "UpdatePath", "UpdateTargets", "OnValidate" })
                {
                    var m = ikPathType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    if (m != null) { try { m.Invoke(ikPathComp, null); } catch { } }
                }

                string msg = $"Path '{safePathName}' created under '{robotName}', IKPath attached and wired to RobotIK.";
                var dataObj = Utils.GetGameObjectData(pathGO);
                return Response.Success(msg, dataObj);
            }).Result;
        }

        // ---------------- Helpers ----------------

        private static Type FindTypeBySimpleName(string name)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = null;
                    try { t = asm.GetTypes().FirstOrDefault(x => x.Name == name); }
                    catch { /* ignore */ }
                    if (t != null) return t;
                }
            }
            catch { /* ignore */ }
            return null;
        }

        // ✅ bool 반환으로 수정: 성공 시 true, 아니면 false
        private static bool TryAssign(object comp, string member, object value)
        {
            if (comp == null) return false;
            var type  = comp.GetType();
            var vtype = value?.GetType();

            // Field
            var field = type.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                if (vtype == null)
                {
                    if (!field.FieldType.IsValueType || Nullable.GetUnderlyingType(field.FieldType) != null)
                    { field.SetValue(comp, null); return true; }
                    return false;
                }
                if (field.FieldType.IsAssignableFrom(vtype))
                { field.SetValue(comp, value); return true; }
            }

            // Property
            var prop = type.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                if (vtype == null)
                {
                    if (!prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) != null)
                    { prop.SetValue(comp, null, null); return true; }
                    return false;
                }
                if (prop.PropertyType.IsAssignableFrom(vtype))
                { prop.SetValue(comp, value, null); return true; }
            }

            return false;
        }

        private static bool TryAssignAny(object target, object value, params string[] fieldOrPropNames)
        {
            if (target == null || value == null) return false;
            var t = target.GetType();
            var vtype = value.GetType();

            foreach (var name in fieldOrPropNames)
            {
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType.IsAssignableFrom(vtype))
                { f.SetValue(target, value); return true; }

                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanWrite && p.PropertyType.IsAssignableFrom(vtype))
                { p.SetValue(target, value, null); return true; }
            }
            return false;
        }

        private static bool TrySetEnumByName(object comp, string member, string enumName)
        {
            if (comp == null) return false;
            var t = comp.GetType();

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
    }
}
