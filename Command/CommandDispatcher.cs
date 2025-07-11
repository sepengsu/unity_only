using Newtonsoft.Json.Linq;
using UnityEngine;
using ObjectManager;
using EnvironmentManager;  // ✅ 추가

namespace Command
{
    public static class CommandDispatcher
    {
        public static object Execute(JObject command)
        {
            string action = command["action"]?.ToString()?.ToLower();

            if (string.IsNullOrEmpty(action))
                return Error("Missing 'action' field");

            switch (action)
            {
                case "create":
                    GameObject created = ObjectCreator.CreateObject(command);
                    return Success($"Created GameObject '{created.name}'");

                case "move":
                    bool moved = ObjectMover.MoveObject(command);
                    return moved ? Success("Object moved successfully") : Error("Failed to move object");

                case "describe": 
                    string envDescription = EnvironmentDescriber.DescribeEnvironment();
                    Debug.Log(envDescription);  // 콘솔에 전체 환경 정보 출력
                    return Success("Environment described in Console");

                case "remove":
                    bool removed = ObjectRemover.RemoveObject(command);
                    return removed ? Success("Object removed") : Error("Failed to remove object");
                
                case "rotate": 
                    bool rotated = ObjectRotator.RotateObject(command);
                    return rotated ? Success("Object rotated successfully") : Error("Failed to rotate object");
                
                case "move_with_speed":
                    bool moveSpd = ObjectMoverWithSpeed.AddMovement(command);
                    return moveSpd ? Success("Move with speed applied") : Error("Failed to move with speed");

                case "rotate_with_speed":
                    bool rotSpd = ObjectRotatorWithSpeed.AddRotation(command);
                    return rotSpd ? Success("Rotation with speed applied") : Error("Failed to rotate with speed");
                case "stop":
                    bool stopped = ObjectStopper.StopObject(command);
                    return stopped ? Success("Stopped object motion") : Error("No motion components to stop");
                case "rescale":
                    bool scaled = ObjectScaler.RescaleObject(command);
                    return scaled ? Success("Object rescaled") : Error("Failed to rescale object");

                case "color":
                    bool colored = ObjectColorChanger.ChangeColor(command);
                    return colored ? Success("Color changed") : Error("Failed to change color");
                    
                default:
                    return Error($"Unknown action '{action}'");
            }
        }

        private static object Success(string message) => new { status = "success", message };
        private static object Error(string message) => new { status = "error", message };
    }
}
