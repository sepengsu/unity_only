using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ObjectManager
{
    public static class ObjectColorChanger
    {
        public static bool ChangeColor(JObject data)
        {
            string name = data["target"]?.ToString();
            if (string.IsNullOrEmpty(name)) return false;

            GameObject go = GameObject.Find(name);
            if (go == null) return false;

            if (!(data["color"] is JArray colorArr) || colorArr.Count < 3) return false;

            Color color = new Color(
                colorArr[0].ToObject<float>(),
                colorArr[1].ToObject<float>(),
                colorArr[2].ToObject<float>(),
                colorArr.Count > 3 ? colorArr[3].ToObject<float>() : 1f
            );

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return false;

            renderer.material.color = color;
            Debug.Log($"[ObjectColorChanger] '{name}' color set to {color}");
            return true;
        }
    }
}
