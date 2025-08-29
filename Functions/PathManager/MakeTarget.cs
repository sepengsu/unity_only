using System;
using System.Linq;
using System.Reflection;
using System.Collections;            // IList
using System.Collections.Generic;    // List<>
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Helpers;

using Utils = Functions.GameObjectManager.Utils;

namespace Functions.PathManager
{
    public static class MakeTarget
    {
        public static object Execute(JObject data)
        {
            try
            {
                return MainThreadDispatcher.Run(() =>
                {
                    try
                    {
                        // --- 0) action / json ---
                        string action = data?["action"]?.ToString();
                        if (!string.Equals(action, "make_target", StringComparison.OrdinalIgnoreCase))
                            return Response.Error("[MakeTarget] Invalid or missing action. Expected 'make_target'.");

                        var jsonTok = data?["json"];
                        if (jsonTok == null)
                            return Response.Error("[MakeTarget] Missing 'json'.");

                        JObject payload =
                            jsonTok.Type == JTokenType.Object ? (JObject)jsonTok :
                            jsonTok.Type == JTokenType.String ? JObject.Parse(jsonTok.ToString()) :
                            null;

                        if (payload == null)
                            return Response.Error("[MakeTarget] 'json' must be an object or a JSON string.");

                        string robotName = payload["robot"]?.ToString();
                        string pathName = payload["path"]?.ToString();
                        string targetNameIn = payload["name"]?.ToString();
                        string space = (payload["space"]?.ToString() ?? "local").ToLowerInvariant(); // world|local|robot|path

                        if (string.IsNullOrWhiteSpace(robotName))
                            return Response.Error("[MakeTarget] JSON must contain non-empty 'robot'.");
                        if (string.IsNullOrWhiteSpace(pathName))
                            return Response.Error("[MakeTarget] JSON must contain non-empty 'path'.");

                        // pose (position/rotation)
                        Vector3 pos = Vector3.zero;
                        if (payload["position"] is JArray pArr)
                            pos = new Vector3(
                                pArr.Count > 0 ? pArr[0].ToObject<float>() : 0f,
                                pArr.Count > 1 ? pArr[1].ToObject<float>() : 0f,
                                pArr.Count > 2 ? pArr[2].ToObject<float>() : 0f
                            );

                        Quaternion rot = Quaternion.identity;
                        if (payload["rotation"] is JArray rArr)
                            rot = Quaternion.Euler(
                                rArr.Count > 0 ? rArr[0].ToObject<float>() : 0f,
                                rArr.Count > 1 ? rArr[1].ToObject<float>() : 0f,
                                rArr.Count > 2 ? rArr[2].ToObject<float>() : 0f
                            );

                        // --- 1) robot & path ---
                        GameObject robotGO = FindGOAllScenes(robotName);
                        if (robotGO == null)
                            return Response.Error($"[MakeTarget] Robot '{robotName}' not found.");

                        Transform pathTF = robotGO.transform.GetComponentsInChildren<Transform>(true)
                                                .FirstOrDefault(x => x.name == pathName);
                        if (pathTF == null)
                            return Response.Error($"[MakeTarget] Path '{pathName}' not found under robot '{robotName}'.");

                        // --- 2) IKPath & RobotIK 타입/컴포넌트 ---
                        var ikPathType = Type.GetType("game4automation.IKPath") ?? FindTypeBySimpleName("IKPath");
                        if (ikPathType == null)
                            return Response.Error("[MakeTarget] IKPath type not found.");
                        var ikPathComp = pathTF.GetComponent(ikPathType);
                        if (ikPathComp == null)
                            return Response.Error("[MakeTarget] IKPath component not found on given path.");

                        var robotIkType = ResolveTypeFromHierarchy(robotGO, "RobotIK")
                                          ?? Type.GetType("game4automation.RobotIK")
                                          ?? FindTypeBySimpleName("RobotIK");
                        if (robotIkType == null)
                            return Response.Error("[MakeTarget] RobotIK type not found.");
                        var robotIK = robotGO.GetComponentInChildren(robotIkType, true);
                        if (robotIK == null)
                            return Response.Error("[MakeTarget] RobotIK component not found under robot.");

                        // IKTarget 런타임 타입 추정(리스트에서 or 하이어라키에서)
                        Type ikTargetType =
                            ResolveTypeFromHierarchy(pathTF.gameObject, "IKTarget")
                            ?? Type.GetType("game4automation.IKTarget")
                            ?? FindTypeBySimpleName("IKTarget");
                        if (ikTargetType == null)
                            return Response.Error("[MakeTarget] IKTarget type not found.");

                        // IKPath에 RobotIK / targetRoot 보정(안전)
                        TrySet(ikPathComp, "RobotIK", robotIK);
                        TrySet(ikPathComp, "robotIK", robotIK);
                        TrySet(ikPathComp, "targetRoot", pathTF);
                        TrySet(ikPathComp, "TargetRoot", pathTF);
                        TrySet(ikPathComp, "targetsRoot", pathTF);

                        // --- 3) 로컬 포즈 계산 (path 기준) ---
                        (Vector3 localPos, Quaternion localRot) = ToLocalPose(space, pos, rot, robotGO.transform, pathTF);

                        // --- 4) 이름 유니크 (path 하위에서만 유니크 보장) ---
                        string safeTargetName = MakeUniqueInParent(pathTF, string.IsNullOrWhiteSpace(targetNameIn) ? $"Target{pathTF.childCount}" : targetNameIn);

                        // --- 5) IKPath의 AddNewTargetToPath (또는 동등) 호출해 생성 ---
                        // 호출 전 존재하던 IKTarget 집합(InstanceID) 기록 → 무엇이 새로 생겼는지 추적
                        var before = new HashSet<int>(
                            pathTF.GetComponentsInChildren(ikTargetType, true)
                                  .Where(c => c != null)
                                  .Select(c => c.GetInstanceID())
                        );

                        GameObject createdGO = InvokeAddNewTargetToPath(ikPathComp, ikPathType, pathTF);

                        // 그래도 못찾았으면 실패 처리
                        if (createdGO == null)
                            return Response.Error("[MakeTarget] IKPath.AddNewTargetToPath (or equivalent) did not produce a target.");

                        // --- 6) 타겟 초기 세팅 (부모=path, 로컬 포즈, 이름, RobotIK 연결) ---
                        createdGO.transform.SetParent(pathTF, false);
                        createdGO.transform.localPosition = localPos;
                        createdGO.transform.localRotation = localRot;
                        createdGO.name = safeTargetName;

                        var ikComp = createdGO.GetComponent(ikTargetType) ?? createdGO.AddComponent(ikTargetType);
                        TrySet(ikComp, "RobotIK", robotIK);
                        TrySet(ikComp, "WaitForSignal", false);
                        ResetTargetRuntimeState(ikComp);

                        // --- 7) SolveIK로 도달성 체크 ---
                        var solveIK = robotIkType.GetMethod("SolveIK",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        try { solveIK?.Invoke(robotIK, new object[] { ikComp }); } catch { /* 무시 */ }

                        bool reachable = TryGet(ikComp, "Reachable", out bool r) && r;
                        if (!reachable)
                        {
                            UnityEngine.Object.Destroy(createdGO);
                            // IKPath가 내부 리스트에 이미 넣었을 수도 있으니 갱신
                            TryInvokeNoThrow(ikPathComp, ikPathType, "RefreshTargets");
                            return Response.Success($"[MakeTarget] Not reachable → discarded '{safeTargetName}'.",
                                new { reachable = false, name = safeTargetName });
                        }

                        // --- 8) IKPath 갱신 호출 (뷰/캐시 동기화) ---
                        TryInvokeNoThrow(ikPathComp, ikPathType, "RefreshTargets");
                        TryInvokeNoThrow(ikPathComp, ikPathType, "RebuildPath");
                        TryInvokeNoThrow(ikPathComp, ikPathType, "UpdatePath");
                        TryInvokeNoThrow(ikPathComp, ikPathType, "UpdateTargets");
                        TryInvokeNoThrow(ikPathComp, ikPathType, "OnValidate");


                        // IKTarget 바로 접근 (검증/예외 처리 안 함)
                        var ik = createdGO.GetComponent<game4automation.IKTarget>();

                        // 프리팹 로드 (Assets/Resources/TargetSphere.prefab)
                        var prefab = Resources.Load<GameObject>("TargetSphere");

                        // 인스턴스 생성 + 타깃 하위로 부착 (로컬 Pose 유지)
                        var inst = UnityEngine.Object.Instantiate(prefab, createdGO.transform, false);
                        inst.name = prefab.name;                 // "(Clone)" 제거
                        inst.transform.localPosition = Vector3.zero;
                        inst.transform.localRotation = Quaternion.identity;
                        inst.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

                        // --- 9) 결과 ---
                        string msg = $"Target '{safeTargetName}' created via IKPath.AddNewTargetToPath under '{robotName}/{pathName}'.";
                        var dataObj = Utils.GetGameObjectData(createdGO);
                        return Response.Success(msg, dataObj);
                    }
                    catch (Exception exInner)
                    {
                        if (exInner is TargetInvocationException tie && tie.InnerException != null)
                            return Response.Error($"[MakeTarget] {tie.InnerException.GetType().Name}: {tie.InnerException.Message}\n{tie.InnerException.StackTrace}");
                        return Response.Error($"[MakeTarget] Exception (inner): {exInner.GetType().Name} - {exInner.Message}\n{exInner.StackTrace}");
                    }
                }).Result;
            }
            catch (Exception exOuter)
            {
                return Response.Error($"[MakeTarget] Unhandled exception: {exOuter.GetType().Name} - {exOuter.Message}\n{exOuter.StackTrace}");
            }
        }

        // ---------------- 내부 유틸 ----------------
        private static GameObject ExtractGO(object ret)
        {
            if (ret == null) return null;
            if (ret is GameObject g) return g;
            if (ret is Component c) return c.gameObject;
            if (ret is Transform t) return t.gameObject;
            return null;
        }

        private static (Vector3, Quaternion) ToLocalPose(string space, Vector3 pos, Quaternion rot, Transform robot, Transform path)
        {
            switch (space)
            {
                case "world":
                    return (path.InverseTransformPoint(pos), Quaternion.Inverse(path.rotation) * rot);
                case "robot":
                    {
                        var wpos = robot.TransformPoint(pos);
                        var wrot = robot.rotation * rot;
                        return (path.InverseTransformPoint(wpos), Quaternion.Inverse(path.rotation) * wrot);
                    }
                case "local":
                case "path":
                default:
                    return (pos, rot);
            }
        }

        private static string MakeUniqueInParent(Transform parent, string baseName)
        {
            string nm = Regex.Replace(string.IsNullOrWhiteSpace(baseName) ? "Target" : baseName, @"\s+", "");
            string tryNm = nm; int i = 0;
            while (true)
            {
                var child = parent.Find(tryNm);
                if (child == null) return tryNm;
                i++; tryNm = $"{nm}_{i}";
            }
        }

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

        private static Type FindTypeBySimpleName(string simpleName)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = null;
                    try { t = asm.GetTypes().FirstOrDefault(x => x.Name == simpleName); } catch { }
                    if (t != null) return t;
                }
            }
            catch { }
            return null;
        }

        private static Type ResolveTypeFromHierarchy(GameObject root, string simpleName)
        {
            if (root == null) return null;
            var comp = root.GetComponentsInChildren<Component>(true)
                           .FirstOrDefault(c => c != null && c.GetType().Name == simpleName);
            return comp?.GetType();
        }

        private static void ResetTargetRuntimeState(Component ikComp)
        {
            if (ikComp == null) return;
            if (ikComp is Behaviour bh) bh.enabled = true;

            TrySet(ikComp, "Active", true);
            TrySet(ikComp, "Enabled", true);

            string[] toFalse =
            {
                "Finished","IsFinished","Visited","IsVisited","Reached","IsReached",
                "Started","IsStarted","InProgress","Busy","Done","Processing","WaitForSignal"
            };
            for (int i = 0; i < toFalse.Length; i++) TrySet(ikComp, toFalse[i], false);
        }

        private static void TryInvokeNoThrow(object comp, Type type, string method)
        {
            if (comp == null || type == null) return;
            var m = type.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (m == null) return;
            try { m.Invoke(comp, null); } catch { }
        }

        private static bool TrySet(object obj, string memberName, object value)
        {
            if (obj == null) return false;
            var t = obj.GetType();

            var f = t.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && (value == null || f.FieldType.IsInstanceOfType(value) || f.FieldType.IsAssignableFrom(value.GetType())))
            { f.SetValue(obj, value); return true; }

            var p = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite && (value == null || p.PropertyType.IsInstanceOfType(value) || p.PropertyType.IsAssignableFrom(value.GetType())))
            { p.SetValue(obj, value, null); return true; }

            return false;
        }

        private static bool TryGet<T>(object obj, string memberName, out T value)
        {
            value = default;
            if (obj == null) return false;
            var t = obj.GetType();

            var f = t.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && typeof(T).IsAssignableFrom(f.FieldType))
            {
                object v = f.GetValue(obj);
                if (v is T) { value = (T)v; return true; }
            }
            var p = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanRead && typeof(T).IsAssignableFrom(p.PropertyType))
            {
                object v = p.GetValue(obj, null);
                if (v is T) { value = (T)v; return true; }
            }
            return false;
        }
        private static GameObject InvokeAddNewTargetToPath(object ikPathComp, Type ikPathType, Transform pathTF)
        {
            // 이름 후보 (패키지 버전에 따라 다를 수 있음)
            string[] names =
            {
                "AddTargetToPath"
            };

            foreach (var n in names)
            {
                var methods = ikPathType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                        .Where(m => string.Equals(m.Name, n, System.StringComparison.OrdinalIgnoreCase))
                                        .ToArray();

                foreach (var m in methods)
                {
                    var ps = m.GetParameters();

                    // 1) 호출 전 스냅샷
                    var (beforeCount, _) = GetPathSnapshot(ikPathComp, ikPathType, pathTF);

                    object ret = null;
                    try
                    {
                        if (ps.Length == 0)
                        {
                            ret = m.Invoke(ikPathComp, null);
                        }
                        else if (ps.Length == 1)
                        {
                            var p0 = ps[0].ParameterType;
                            if (typeof(Transform).IsAssignableFrom(p0))
                                ret = m.Invoke(ikPathComp, new object[] { pathTF });
                            else if (typeof(GameObject).IsAssignableFrom(p0))
                                ret = m.Invoke(ikPathComp, new object[] { pathTF.gameObject });
                            else if (typeof(Component).IsAssignableFrom(p0))
                                ret = m.Invoke(ikPathComp, new object[] { pathTF });
                            else
                                continue; // 지원하지 않는 시그니처
                        }
                        else
                        {
                            continue; // 0~1개만 지원
                        }
                    }
                    catch
                    {
                        // 이 시그니처는 사용할 수 없음 → 다음 후보로
                        continue;
                    }

                    // 2) 반환값이 GO/Component/Transform이면 즉시 반환
                    var go = ExtractGO(ret);
                    if (go != null) return go;

                    // 3) 반환이 없거나 null이면 추가된 타깃을 스냅샷 차이로 탐지
                    var (afterCount, lastGO) = GetPathSnapshot(ikPathComp, ikPathType, pathTF);
                    if (afterCount > beforeCount && lastGO != null)
                        return lastGO;

                    // 호출은 됐지만 추가가 없었을 수도 있으니 다음 메서드 후보 시도
                }
            }

            // 어떤 후보도 사용 불가 or 추가 실패
            return null;
        }

        /// <summary>
        /// IKPath의 Path(필드/프로퍼티) 길이와 마지막 요소의 GameObject 스냅샷을 가져온다.
        /// Path 접근이 불가하면 pathTF의 자식 Transform을 사용한다.
        /// </summary>
        private static (int count, GameObject lastGO) GetPathSnapshot(object ikPathComp, Type ikPathType, Transform pathTF)
        {
            try
            {
                // 1) 우선 IKPath의 "Path" 멤버(필드 또는 프로퍼티) 시도
                var fi = ikPathType.GetField("Path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var pi = ikPathType.GetProperty("Path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                object val = fi != null ? fi.GetValue(ikPathComp)
                                        : (pi != null ? pi.GetValue(ikPathComp, null) : null);

                if (val != null)
                {
                    // Array
                    if (val is System.Array arr)
                    {
                        int len = arr.Length;
                        GameObject last = len > 0 ? ExtractGO(arr.GetValue(len - 1)) : null;
                        return (len, last);
                    }

                    // IList
                    if (val is IList list)
                    {
                        int len = list.Count;
                        GameObject last = len > 0 ? ExtractGO(list[len - 1]) : null;
                        return (len, last);
                    }

                    // IEnumerable (최후의 보루)
                    if (val is IEnumerable enumerable)
                    {
                        int len = 0;
                        object lastObj = null;
                        foreach (var e in enumerable) { len++; lastObj = e; }
                        GameObject last = len > 0 ? ExtractGO(lastObj) : null;
                        return (len, last);
                    }
                }
            }
            catch
            {
                // Path 접근 실패 시 아래로 폴백
            }

            // 2) 폴백: pathTF의 자식 Transform 사용
            if (pathTF != null)
            {
                int cc = pathTF.childCount;
                GameObject lastChild = cc > 0 ? pathTF.GetChild(cc - 1).gameObject : null;
                return (cc, lastChild);
            }

            return (0, null);
        }

    }
}
