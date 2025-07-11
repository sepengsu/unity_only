using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ObjectManager
{

    public class ObjectMover : MonoBehaviour
    {
        public static bool MoveObject(JObject data)
        {
            string targetName = data["target"]?.ToString();
            if (string.IsNullOrEmpty(targetName))
            {
                Debug.LogWarning("[ObjectMover] 'target' parameter is required.");
                return false;
            }

            GameObject target = GameObject.Find(targetName);
            if (target == null)
            {
                Debug.LogWarning($"[ObjectMover] GameObject '{targetName}' not found.");
                return false;
            }

            // Position (absolute)
            if (data["position"] is JArray pos && pos.Count == 3)
            {
                target.transform.position = ParseVector3(pos);
            }

            // Offset (relative movement)
            if (data["offset"] is JArray offset && offset.Count == 3)
            {
                target.transform.position += ParseVector3(offset);
            }

            // Rotation (absolute)
            if (data["rotation"] is JArray rot && rot.Count == 3)
            {
                target.transform.eulerAngles = ParseVector3(rot);
            }

            // RotationOffset (relative rotation)
            if (data["rotationOffset"] is JArray rotOffset && rotOffset.Count == 3)
            {
                target.transform.eulerAngles += ParseVector3(rotOffset);
            }

            Debug.Log($"[ObjectMover] Moved '{target.name}' to {target.transform.position}");
            return true;
        }

        private static Vector3 ParseVector3(JArray arr)
        {
            return new Vector3(
                arr[0].ToObject<float>(),
                arr[1].ToObject<float>(),
                arr[2].ToObject<float>()
            );
        }
    }

}