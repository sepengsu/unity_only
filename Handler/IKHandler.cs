using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Handler
{
    /// <summary>
    /// IK 관련 명령을 라우팅하는 엔트리 포인트.
    /// 요청 JSON 예:
    /// { "action":"path.add_target", "robot":"Robot01", "pathName":"PickAndPlace", ... }
    /// </summary>
    public static class IKHandler
    {
        public static object Handle(JObject command)
        {
            string action = command["action"]?.ToString()?.ToLower();
            if (string.IsNullOrEmpty(action))
                return Error("Missing 'action' field in params");

            try
            {
                switch (action)
                {
                    case "ensure_env":
                        return Functions.IKManager.EnsureEnv.Execute(command);

                    case "spawn_robot":
                        return Functions.IKManager.SpawnRobot.Execute(command);

                    case "calc_params":
                        return Functions.IKManager.CalcParams.Execute(command);

                    case "path_create":
                        return Functions.IKManager.PathCreate.Execute(command);

                    case "path_add_target":
                        return Functions.IKManager.PathAddTarget.Execute(command);

                    case "path_run":
                        return Functions.IKManager.PathRun.Execute(command);

                    // 필요 시 여기서 더 추가:
                    // case "path.set_props": return Functions.IK.PathSetProps.Execute(command);
                    // case "target.set_correction": return Functions.IK.TargetSetCorrection.Execute(command);

                    default:
                        return Error($"Unknown action '{action}' in IKHandler");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[IKHandler] Unhandled exception: {ex}");
                return Error($"IKHandler exception: {ex.Message}");
            }
        }

        private static object Error(string message) =>
            new { success = false, error = message };
    }
}
