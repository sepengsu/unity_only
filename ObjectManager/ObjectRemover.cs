using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ObjectManager
{
    public static class ObjectRemover
    {
        public static bool RemoveObject(JObject data)
        {
            string name = data["target"]?.ToString();

            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[ObjectRemover] 'target' parameter is required.");
                return false;
            }

            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[ObjectRemover] GameObject '{name}' not found.");
                return false;
            }

            Object.Destroy(go);
            Debug.Log($"[ObjectRemover] Destroyed GameObject '{name}'");
            return true;
        }
    }
}
