using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ObjectManager
{
    public static class ObjectScaler
    {
        public static bool RescaleObject(JObject data)
        {
            string name = data["target"]?.ToString();
            if (string.IsNullOrEmpty(name)) return false;

            GameObject go = GameObject.Find(name);
            if (go == null) return false;

            if (!(data["scale"] is JArray scaleArr) || scaleArr.Count != 3) return false;

            Vector3 newScale = new Vector3(
                scaleArr[0].ToObject<float>(),
                scaleArr[1].ToObject<float>(),
                scaleArr[2].ToObject<float>()
            );

            go.transform.localScale = newScale;
            Debug.Log($"[ObjectScaler] '{name}' scaled to {newScale}");
            return true;
        }
    }
}
