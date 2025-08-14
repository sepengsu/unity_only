using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Functions.AssetManager
{
    public static class Utils
    {
        public static string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            path = path.Replace('\\', '/');
            if (path.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("Assets/Resources/".Length);
            if (path.EndsWith(".prefab"))
                path = path.Substring(0, path.Length - ".prefab".Length);
            return path;
        }

        public static bool AssetExists(string resourcesPath)
        {
            var go = Resources.Load<GameObject>(resourcesPath);
            return go != null;
        }

        public static object ConvertJTokenToType(JToken token, Type targetType)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            try
            {
                if (targetType == typeof(string))
                    return token.ToObject<string>();
                if (targetType == typeof(int))
                    return token.ToObject<int>();
                if (targetType == typeof(float))
                    return token.ToObject<float>();
                if (targetType == typeof(bool))
                    return token.ToObject<bool>();

                if (targetType == typeof(Vector3) && token is JArray vec3 && vec3.Count == 3)
                    return new Vector3(vec3[0].ToObject<float>(), vec3[1].ToObject<float>(), vec3[2].ToObject<float>());

                if (targetType == typeof(Color) && token is JArray color && color.Count >= 3)
                    return new Color(
                        color[0].ToObject<float>(),
                        color[1].ToObject<float>(),
                        color[2].ToObject<float>(),
                        color.Count > 3 ? color[3].ToObject<float>() : 1.0f
                    );

                if (targetType.IsEnum)
                    return Enum.Parse(targetType, token.ToString(), true);

                return token.ToObject(targetType);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetUtils] Failed to convert JToken to {targetType.Name}: {e.Message}");
                return null;
            }
        }

        public static bool SetPropertyOrField(object target, string memberName, JToken value)
        {
            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            try
            {
                PropertyInfo prop = type.GetProperty(memberName, flags);
                if (prop != null && prop.CanWrite)
                {
                    object converted = ConvertJTokenToType(value, prop.PropertyType);
                    if (converted != null)
                    {
                        prop.SetValue(target, converted);
                        return true;
                    }
                }

                FieldInfo field = type.GetField(memberName, flags);
                if (field != null)
                {
                    object converted = ConvertJTokenToType(value, field.FieldType);
                    if (converted != null)
                    {
                        field.SetValue(target, converted);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetUtils] Failed to set {memberName} on {type.Name}: {e.Message}");
            }

            return false;
        }

        public static bool ApplyObjectProperties(object target, JObject properties)
        {
            if (target == null || properties == null)
                return false;

            bool modified = false;
            foreach (var prop in properties.Properties())
            {
                if (SetPropertyOrField(target, prop.Name, prop.Value))
                    modified = true;
            }
            return modified;
        }
    }
}
