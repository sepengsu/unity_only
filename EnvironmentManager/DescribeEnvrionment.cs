using System.Linq;
using System.Text;
using UnityEngine;

namespace EnvironmentManager
{
    public class EnvironmentDescriber : MonoBehaviour
    {
        public static string DescribeEnvironment()
        {
            var sb = new StringBuilder();

            // === 1. View 정보 ===
            sb.AppendLine("== View (Camera) ==");
            var cameras = Camera.allCameras;
            foreach (var cam in cameras)
            {
                sb.AppendLine($"- Camera: {cam.name}");
                sb.AppendLine($"  - Position: {cam.transform.position}");
                sb.AppendLine($"  - Rotation: {cam.transform.eulerAngles}");
                sb.AppendLine($"  - Field of View: {cam.fieldOfView}");
                sb.AppendLine($"  - Is Main Camera: {cam.CompareTag("MainCamera")}");
            }

            // === 2. Scene의 GameObjects ===
            sb.AppendLine("\n== Objects in Scene ==");
            var allObjects = FindObjectsOfType<GameObject>()
                .Where(go => go.hideFlags == HideFlags.None && go.activeInHierarchy)
                .OrderBy(go => go.name)
                .ToList();

            foreach (var go in allObjects)
            {
                sb.AppendLine($"- {go.name}");
                sb.AppendLine($"  - Position: {go.transform.position}");
                sb.AppendLine($"  - Tag: {go.tag}, Layer: {LayerMask.LayerToName(go.layer)}");
                sb.AppendLine($"  - Components: {string.Join(", ", go.GetComponents<Component>().Select(c => c.GetType().Name))}");
            }

            // === 3. Lighting 환경 (기본 Directional Light) ===
            var lights = FindObjectsOfType<Light>();
            sb.AppendLine("\n== Lights ==");
            foreach (var light in lights)
            {
                sb.AppendLine($"- Light: {light.name}");
                sb.AppendLine($"  - Type: {light.type}, Intensity: {light.intensity}, Color: {light.color}");
            }

            return sb.ToString();
        }
}

}

