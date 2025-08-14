using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers;

namespace Functions.IKManager
{
    public static class CalcParams
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                try
                {
                    string robotName = data["robot"]?.ToString();
                    if (string.IsNullOrWhiteSpace(robotName))
                        return Response.Error("[IK.CalcParams] Missing 'robot'");

                    GameObject robotGo = GameObject.Find(robotName)
                        ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == robotName);
                    if (robotGo == null)
                        return Response.Error($"[IK.CalcParams] Robot '{robotName}' not found.");

                    var ik = GetComponentByCandidates(robotGo, "RobotIK", "RVRobotIK", "InverseKinematics", "IKRobot");
                    if (ik == null)
                        return Response.Error("[IK.CalcParams] RobotIK component not found on robot hierarchy.");

                    string[] methods = {
                        "CalcKinematicParameters",
                        "CalculateKinematicParameters",
                        "CalcKinematics",
                        "RecalculateKinematicParameters",
                        "RecalcKinematics"
                    };

                    MethodInfo called = null;
                    var t = ik.GetType();
                    foreach (var mn in methods)
                    {
                        var mi = t.GetMethod(mn, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                        if (mi == null) continue;
                        try { mi.Invoke(ik, null); called = mi; break; }
                        catch (Exception ex) { Debug.LogWarning($"[IK.CalcParams] Invoke {t.Name}.{mn} failed: {ex.Message}"); }
                    }

                    if (called == null)
                        return Response.Error("[IK.CalcParams] Could not find a kinematic calc method on RobotIK.");

                    return Response.Success($"Kinematic parameters calculated via {called.Name}.", new
                    {
                        robot = robotGo.name,
                        ikComponent = t.FullName,
                        method = called.Name
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[IK.CalcParams] Exception: {ex}");
                    return Response.Error($"IK.CalcParams exception: {ex.Message}");
                }
            }).Result;
        }

        private static Component GetComponentByCandidates(GameObject root, params string[] typeNames)
        {
            foreach (var name in typeNames)
            {
                var found = root.GetComponentsInChildren<Component>(true)
                                .FirstOrDefault(c => c != null && (c.GetType().Name == name || c.GetType().FullName == name));
                if (found != null) return found;

                var t = FindTypeByName(name);
                if (t != null)
                {
                    var byType = root.GetComponentInChildren(t, true);
                    if (byType != null) return byType;
                }
            }
            return null;
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
    }
}
