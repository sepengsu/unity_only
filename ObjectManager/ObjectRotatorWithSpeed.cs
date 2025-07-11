using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ObjectManager
{
    public static class ObjectRotatorWithSpeed
    {
        public static bool AddRotation(JObject data)
        {
            string name = data["target"]?.ToString();
            if (string.IsNullOrEmpty(name)) return false;

            GameObject go = GameObject.Find(name);
            if (go == null) return false;

            if (!(data["speed"] is JArray speedArr) || speedArr.Count != 3) return false;

            float duration = data["duration"]?.ToObject<float>() ?? -1f;

            var rotator = go.GetComponent<Rotator>() ?? go.AddComponent<Rotator>();
            rotator.rotationSpeed = new Vector3(
                speedArr[0].ToObject<float>(),
                speedArr[1].ToObject<float>(),
                speedArr[2].ToObject<float>()
            );
            rotator.duration = duration;

            return true;
        }
    }
}
