using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ObjectManager
{
    public static class ObjectStopper
    {
        public static bool StopObject(JObject data)
        {
            string name = data["target"]?.ToString();
            if (string.IsNullOrEmpty(name)) return false;

            GameObject go = GameObject.Find(name);
            if (go == null) return false;

            bool stopped = false;

            var mover = go.GetComponent<Mover>();
            if (mover != null)
            {
                Object.Destroy(mover);
                stopped = true;
            }

            var rotator = go.GetComponent<Rotator>();
            if (rotator != null)
            {
                Object.Destroy(rotator);
                stopped = true;
            }

            return stopped;
        }
    }
}
