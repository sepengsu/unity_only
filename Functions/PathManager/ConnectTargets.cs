using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Helpers;
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.PathManager
{
    public static class ConnectTargets
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string action = data?["action"]?.ToString();
                if (!string.Equals(action, "connect_targets", StringComparison.OrdinalIgnoreCase))
                    return Response.Error("[ConnectTargets] Invalid or missing action. Expected 'connect_targets'.");

                string json = data?["json"]?.ToString();
                if (string.IsNullOrWhiteSpace(json))
                    return Response.Error("[ConnectTargets] Missing 'json' (string).");

                JObject payload;
                try { payload = JObject.Parse(json); }
                catch (Exception e) { return Response.Error($"[ConnectTargets] JSON parse error: {e.Message}"); }

                string robotName = payload["robot"]?.ToString();
                string pathName  = payload["path"]?.ToString();
                string order     = payload["order"]?.ToString() ?? "by_name";

                bool autoLinkRobotIK = payload["auto_link_robotik"]?.ToObject<bool?>() ?? true;
                bool autoCorrection  = payload["auto_correction"]?.ToObject<bool?>() ?? true;
                bool refreshPath     = payload["refresh_path"]?.ToObject<bool?>() ?? true;
                bool pruneNulls      = payload["prune_nulls"]?.ToObject<bool?>() ?? true;

                // ▼ LineRenderer 미리보기 옵션
                bool  drawPreview   = payload["draw_preview_line"]?.ToObject<bool?>() ?? true;
                float previewWidth  = payload["preview_width"]?.ToObject<float?>() ?? 0.01f;
                bool  previewLoop   = payload["preview_loop"]?.ToObject<bool?>() ?? false;
                string previewColorStr = payload["preview_color"]?.ToString();
                Color previewColor = TryParseColor(previewColorStr, out var pc) ? pc : new Color(0f, 1f, 1f, 1f); // default: cyan

                if (string.IsNullOrWhiteSpace(robotName))
                    return Response.Error("[ConnectTargets] JSON must contain non-empty 'robot'.");
                if (string.IsNullOrWhiteSpace(pathName))
                    return Response.Error("[ConnectTargets] JSON must contain non-empty 'path'.");

                // 로봇 찾기
                GameObject robotGO = GameObject.Find(robotName);
                if (robotGO == null)
                {
                    var scene = SceneManager.GetActiveScene();
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        var t = root.transform.Find(robotName);
                        if (t != null) { robotGO = t.gameObject; break; }
                        var found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == robotName);
                        if (found != null) { robotGO = found.gameObject; break; }
                    }
                }
                if (robotGO == null)
                    return Response.Error($"[ConnectTargets] Robot '{robotName}' not found in scene.");

                // 경로 찾기
                Transform pathTF = robotGO.transform.GetComponentsInChildren<Transform>(true)
                                        .FirstOrDefault(x => x.name == pathName);
                if (pathTF == null)
                    return Response.Error($"[ConnectTargets] Path '{pathName}' not found under robot '{robotName}'.");

                // 타입
                var ikPathType   = FindTypeBySimpleName("IKPath")   ?? Type.GetType("game4automation.IKPath")   ?? Type.GetType("realvirtual.IKPath");
                var ikTargetType = FindTypeBySimpleName("IKTarget") ?? Type.GetType("game4automation.IKTarget") ?? Type.GetType("realvirtual.IKTarget");
                var robotIkType  = FindTypeBySimpleName("RobotIK")  ?? Type.GetType("game4automation.RobotIK")  ?? Type.GetType("realvirtual.RobotIK");
                if (ikPathType == null)   return Response.Error("[ConnectTargets] IKPath type not found.");
                if (ikTargetType == null) return Response.Error("[ConnectTargets] IKTarget type not found.");

                var ikPathComp = pathTF.GetComponent(ikPathType);
                if (ikPathComp == null)
                    return Response.Error($"[ConnectTargets] IKPath component not found on '{pathName}'.");

                // (선택) RobotIK 진단
                Component robotIK = null;
                if (robotIkType != null)
                {
                    robotIK = robotGO.GetComponent(robotIkType)
                           ?? robotGO.GetComponentInChildren(robotIkType, true)
                           ?? robotGO.GetComponentInParent(robotIkType);
                }
                if (robotIK != null)
                {
                    var axisField = robotIkType.GetField("Axis", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var axisArr   = axisField?.GetValue(robotIK) as Array;
                    if (axisArr == null || axisArr.Length == 0)
                        return Response.Error("[ConnectTargets] RobotIK.Axis is empty. Assign 6 Drive components (J1~J6).");
                }

                // IKTarget 수집
                var targets = pathTF.GetComponentsInChildren(ikTargetType, true).Cast<Component>().ToList();
                if (pruneNulls)
                    targets = targets.Where(t => t != null && t.gameObject != null).ToList();
                if (targets.Count == 0)
                    return Response.Error($"[ConnectTargets] No IKTarget found under '{robotName}/{pathName}'.");

                // 정렬
                if (string.Equals(order, "by_hierarchy", StringComparison.OrdinalIgnoreCase))
                    targets = targets.OrderBy(c => GetHierarchyKey(c.transform)).ToList();
                else
                    targets = targets.OrderBy(c => c.name, new NaturalComparer()).ToList();

                // ▼ 실제 연결 전에 LineRenderer로 미리보기 라인 그리기
                if (drawPreview)
                {
                    DrawPreviewLineWithLR(pathTF, targets.Select(t => ((Component)t).transform).ToList(), previewWidth, previewColor, previewLoop);
                }
                else
                {
                    // draw_preview_line=false면 기존 프리뷰 제거
                    var old = pathTF.Find("PreConnectLine");
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }

                // ▼ (여기서 실제 IKPath.Path 배열/리스트를 targets 순서로 세팅하는 로직을 이어서 작성)
                //    현재 요청은 라인만 그리도록이므로 생략.

                string msg = $"Connected {targets.Count} target(s) to IKPath on '{robotName}/{pathName}'. (preview line {(drawPreview ? "drawn" : "skipped")})";
                var dataObj = Utils.GetGameObjectData(pathTF.gameObject);
                return Response.Success(msg, dataObj);
            }).Result;
        }

        // ---------------- Helpers ----------------

        private static void DrawPreviewLineWithLR(Transform pathTF, List<Transform> points, float width, Color color, bool loop)
        {
            if (pathTF == null) return;

            const string kLineName = "PreConnectLine";

            // 기존 라인 재사용 or 생성
            Transform child = pathTF.Find(kLineName);
            LineRenderer lr = null;

            if (child == null)
            {
                var go = new GameObject(kLineName);
                go.transform.SetParent(pathTF, false);
                lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.alignment = LineAlignment.View;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.numCapVertices = 4;
                lr.numCornerVertices = 2;

                // 간단 머티리얼(런타임 생성)
                lr.material = new Material(Shader.Find("Sprites/Default"));
            }
            else
            {
                lr = child.GetComponent<LineRenderer>() ?? child.gameObject.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
            }

            lr.widthMultiplier = Mathf.Max(1e-6f, width);
            lr.startColor = color;
            lr.endColor = color;
            lr.loop = loop;

            int count = points?.Count ?? 0;
            lr.positionCount = count;

            if (count > 0)
            {
                // 한 번에 세팅
                var arr = new Vector3[count];
                for (int i = 0; i < count; i++) arr[i] = points[i].position;
                lr.SetPositions(arr);
            }
        }

        private static string GetHierarchyKey(Transform t)
        {
            var stack = new Stack<Transform>();
            var cur = t;
            while (cur != null) { stack.Push(cur); cur = cur.parent; }
            return string.Join("/", stack.Select(x => $"{x.name}#{x.GetSiblingIndex():D4}"));
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

        private static bool TryParseColor(string s, out Color c)
        {
            c = default;
            if (string.IsNullOrWhiteSpace(s)) return false;

            // #RRGGBB, #RRGGBBAA 등 HTML
            if (s[0] == '#' && ColorUtility.TryParseHtmlString(s, out c)) return true;

            // r,g,b[,a] (0~1)
            var parts = s.Split(',');
            if (parts.Length == 3 || parts.Length == 4)
            {
                if (float.TryParse(parts[0], out float r) &&
                    float.TryParse(parts[1], out float g) &&
                    float.TryParse(parts[2], out float b))
                {
                    float a = 1f;
                    if (parts.Length == 4) float.TryParse(parts[3], out a);
                    c = new Color(r, g, b, a);
                    return true;
                }
            }
            return false;
        }

        private class NaturalComparer : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return -1; if (b == null) return 1;
                int ia = 0, ib = 0;
                while (ia < a.Length && ib < b.Length)
                {
                    if (char.IsDigit(a[ia]) && char.IsDigit(b[ib]))
                    {
                        long va = 0, vb = 0;
                        while (ia < a.Length && char.IsDigit(a[ia])) { va = va * 10 + (a[ia] - '0'); ia++; }
                        while (ib < b.Length && char.IsDigit(b[ib])) { vb = vb * 10 + (b[ib] - '0'); ib++; }
                        int cmp = va.CompareTo(vb);
                        if (cmp != 0) return cmp;
                    }
                    else
                    {
                        int cmp = a[ia].CompareTo(b[ib]);
                        if (cmp != 0) return cmp;
                        ia++; ib++;
                    }
                }
                return (a.Length - ia).CompareTo(b.Length - ib);
            }
        }
    }
}
