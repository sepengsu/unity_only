using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ObjectManager
{
    public static class ObjectRotator
    {
        public static bool RotateObject(JObject data)
        {
            string name = data["target"]?.ToString();

            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[ObjectRotator] 'target' parameter is required.");
                return false;
            }

            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[ObjectRotator] GameObject '{name}' not found.");
                return false;
            }

            if (data["rotation"] is JArray rot && rot.Count == 3)
            {
                Vector3 euler = new Vector3(
                    rot[0].ToObject<float>(),
                    rot[1].ToObject<float>(),
                    rot[2].ToObject<float>()
                );
                go.transform.eulerAngles = euler;
                Debug.Log($"[ObjectRotator] Rotated '{name}' to {euler}");
                return true;
            }

            Debug.LogWarning("[ObjectRotator] 'rotation' must be an array of 3 numbers.");
            return false;
        }
    }
}
