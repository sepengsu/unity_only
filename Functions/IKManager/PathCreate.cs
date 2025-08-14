using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers; // MainThreadDispatcher, Utils
using Object = UnityEngine.Object;

namespace Functions.IKManager
{
    public static class PathCreate
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                try
                {
                    // --- read inputs ---
                    string robotName = data["robot"]?.ToString();
                    string pathName  = data["pathName"]?.ToString();

                    if (string.IsNullOrWhiteSpace(robotName))
                        return Error("[IK.PathCreate] Missing 'robot'");

                    if (string.IsNullOrWhiteSpace(pathName))
                        return Error("[IK.PathCreate] Missing 'pathName'");

                    // optional props
                    var props = data["props"] as JObject;

                    // --- find robot GO ---
                    GameObject robotGo = GameObject.Find(robotName);
                    if (robotGo == null)
                    {
                        // fallback: 씬 전체에서 이름으로 찾기(부분 일치 방지)
                        robotGo = Resources.FindObjectsOfTypeAll<GameObject>()
                                           .FirstOrDefault(g => g.name == robotName);
                    }
                    if (robotGo == null)
                        return Error($"[IK.PathCreate] Robot GameObject '{robotName}' not found.");

                    // --- ensure / get child path GO ---
                    Transform pathTr = robotGo.transform.Find(pathName);
                    GameObject pathGo;
                    if (pathTr == null)
                    {
                        pathGo = new GameObject(pathName);
                        pathGo.transform.SetParent(robotGo.transform, false);
                    }
                    else
                    {
                        pathGo = pathTr.gameObject;
                    }

                    // --- ensure IKPath component ---
                    var ikPathComp = EnsureComponentByName(pathGo, "IKPath");
                    if (ikPathComp == null)
                    {
                        Debug.LogWarning("[IK.PathCreate] Could not add/find component 'IKPath'. " +
                                         "Check if the class exists and is accessible.");
                    }

                    // --- apply properties if possible ---
                    if (props != null && ikPathComp != null)
                    {
                        // 속성 이름은 프로젝트에 따라 다를 수 있음 → 흔한 후보들을 순회 설정
                        TrySetProp(ikPathComp, props, "speedOverride", new[] { "SpeedOverride", "speedOverride", "Speed", "speed" });
                        TrySetProp(ikPathComp, props, "drawPath",      new[] { "DrawPath", "drawPath", "Draw", "draw" });
                        TrySetProp(ikPathComp, props, "loop",          new[] { "Loop", "loop", "IsLoop", "isLoop" });
                    }

                    // --- response ---
                    var result = new
                    {
                        robot = robotGo.name,
                        path  = pathGo.name,
                        pathInstanceID = pathGo.GetInstanceID(),
                        hasIKPath = ikPathComp != null
                    };

                    return Response.Success($"IK Path '{pathGo.name}' created/updated under '{robotGo.name}'.", result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[IK.PathCreate] Exception: {ex}");
                    return Error($"IK.PathCreate exception: {ex.Message}");
                }
            }).Result;
        }

        // ---------- helpers ----------

        private static object Error(string message) =>
            new { success = false, error = message };

        /// <summary>
        /// "typeName"으로 컴포넌트를 보장한다. (이미 있으면 그걸 반환)
        /// - 먼저 현재 GO에서 컴포넌트를 찾아보고
        /// - 없으면 AppDomain에서 타입을 찾아 AddComponent(Type).
        /// </summary>
        private static Component EnsureComponentByName(GameObject go, string typeName)
        {
            if (go == null || string.IsNullOrWhiteSpace(typeName)) return null;

            // 이미 붙었는지 먼저 확인
            var existing = go.GetComponent(typeName);
            if (existing != null) return existing;

            // 어셈블리 전역에서 타입 탐색
            var t = FindTypeByName(typeName);
            if (t == null)
            {
                Debug.LogWarning($"[IK.PathCreate] Type '{typeName}' not found in loaded assemblies.");
                return null;
            }

            try
            {
                return go.AddComponent(t);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IK.PathCreate] Failed to AddComponent('{typeName}'): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// AppDomain 내 모든 어셈블리에서 타입 이름으로 탐색.
        /// 단순 이름("IKPath")과 네임스페이스 포함 풀네임 모두 지원.
        /// </summary>
        private static Type FindTypeByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            // 1) 풀네임 시도
            var t = Type.GetType(name);
            if (t != null) return t;

            // 2) 단순 이름으로 전수 탐색
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetTypes().FirstOrDefault(x => x.Name == name || x.FullName == name);
                    if (t != null) return t;
                }
                catch { /* 일부 에셈블리는 GetTypes 실패할 수 있음 */ }
            }
            return null;
        }

        /// <summary>
        /// props[sourceKey]가 존재하면 comp의 후보 프로퍼티/필드(targetNames)에 대입 시도.
        /// 타입 변환(float/bool 등) 지원.
        /// </summary>
        private static void TrySetProp(Component comp, JObject props, string sourceKey, string[] targetNames)
        {
            if (comp == null || props == null) return;
            if (!props.TryGetValue(sourceKey, out var token)) return;

            object value = JTokenToClr(token);

            var t = comp.GetType();

            // 1) Property 우선
            foreach (var n in targetNames)
            {
                var pi = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi == null || !pi.CanWrite) continue;
                try
                {
                    var converted = ConvertValue(value, pi.PropertyType);
                    pi.SetValue(comp, converted);
                    Debug.Log($"[IK.PathCreate] Set property {t.Name}.{pi.Name} = {converted}");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[IK.PathCreate] Failed to set property {t.Name}.{pi?.Name}: {ex.Message}");
                }
            }

            // 2) Field
            foreach (var n in targetNames)
            {
                var fi = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi == null) continue;
                try
                {
                    var converted = ConvertValue(value, fi.FieldType);
                    fi.SetValue(comp, converted);
                    Debug.Log($"[IK.PathCreate] Set field {t.Name}.{fi.Name} = {converted}");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[IK.PathCreate] Failed to set field {t.Name}.{fi?.Name}: {ex.Message}");
                }
            }

            Debug.LogWarning($"[IK.PathCreate] Could not map '{sourceKey}' to any of [{string.Join(",", targetNames)}] on {t.Name}.");
        }

        private static object JTokenToClr(JToken tok)
        {
            return tok.Type switch
            {
                JTokenType.Integer => (int)tok,
                JTokenType.Float   => (float)tok,
                JTokenType.Boolean => (bool)tok,
                JTokenType.String  => (string)tok,
                _ => tok.ToObject<object>()
            };
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;

            var vt = value.GetType();
            if (targetType.IsAssignableFrom(vt)) return value;

            try
            {
                if (targetType == typeof(float))
                    return Convert.ToSingle(value);
                if (targetType == typeof(double))
                    return Convert.ToDouble(value);
                if (targetType == typeof(int))
                    return Convert.ToInt32(value);
                if (targetType == typeof(bool))
                    return Convert.ToBoolean(value);
                if (targetType == typeof(string))
                    return Convert.ToString(value);
            }
            catch { /* fallthrough */ }

            // 마지막 수단: ChangeType
            try { return Convert.ChangeType(value, targetType); }
            catch { return value; }
        }
    }
}
