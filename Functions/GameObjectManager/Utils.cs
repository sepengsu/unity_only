using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Functions.GameObjectManager
{
    public static class Utils
    {
        public static Type FindType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;

            // Try UnityEngine types
            type = Type.GetType("UnityEngine." + typeName + ", UnityEngine");
            return type;
        }

        public static object ConvertJTokenToType(JToken value, Type targetType)
        {
            if (targetType == typeof(Color) && value is JArray arr)
            {
                float r = arr.Count > 0 ? arr[0].ToObject<float>() : 1f;
                float g = arr.Count > 1 ? arr[1].ToObject<float>() : 1f;
                float b = arr.Count > 2 ? arr[2].ToObject<float>() : 1f;
                float a = arr.Count > 3 ? arr[3].ToObject<float>() : 1f;
                return new Color(r, g, b, a);
            }

            if (targetType == typeof(Vector3) && value is JArray vecArr)
            {
                return new Vector3(
                    vecArr.Count > 0 ? vecArr[0].ToObject<float>() : 0,
                    vecArr.Count > 1 ? vecArr[1].ToObject<float>() : 0,
                    vecArr.Count > 2 ? vecArr[2].ToObject<float>() : 0
                );
            }

            return value.ToObject(targetType);
        }

        public static string[] SplitPropertyPath(string path)
        {
            return path.Split('.');
        }

        public static void SetProperty(object obj, string key, JToken value)
        {
            var type = obj.GetType();

            var prop = type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanWrite)
            {
                var converted = ConvertJTokenToType(value, prop.PropertyType);
                prop.SetValue(obj, converted);
                return;
            }

            var field = type.GetField(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                var converted = ConvertJTokenToType(value, field.FieldType);
                field.SetValue(obj, converted);
            }
        }

        public static void SetNestedProperty(object obj, string path, JToken value)
        {
            var keys = SplitPropertyPath(path);
            object current = obj;

            for (int i = 0; i < keys.Length - 1; i++)
            {
                var prop = current.GetType().GetProperty(keys[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    current = prop.GetValue(current);
                    continue;
                }

                var field = current.GetType().GetField(keys[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    current = field.GetValue(current);
                    continue;
                }

                return; // Invalid path
            }

            SetProperty(current, keys.Last(), value);
        }

        public static object GetGameObjectData(GameObject go)
        {
            if (go == null)
                return null;
            return new
            {
                name = go.name,
                instanceID = go.GetInstanceID(),
                tag = go.tag,
                layer = go.layer,
                activeSelf = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy,
                isStatic = go.isStatic,
                scenePath = go.scene.path, // Identify which scene it belongs to
                transform = new // Serialize transform components carefully to avoid JSON issues
                {
                    // Serialize Vector3 components individually to prevent self-referencing loops.
                    // The default serializer can struggle with properties like Vector3.normalized.
                    position = new
                    {
                        x = go.transform.position.x,
                        y = go.transform.position.y,
                        z = go.transform.position.z,
                    },
                    localPosition = new
                    {
                        x = go.transform.localPosition.x,
                        y = go.transform.localPosition.y,
                        z = go.transform.localPosition.z,
                    },
                    rotation = new
                    {
                        x = go.transform.rotation.eulerAngles.x,
                        y = go.transform.rotation.eulerAngles.y,
                        z = go.transform.rotation.eulerAngles.z,
                    },
                    localRotation = new
                    {
                        x = go.transform.localRotation.eulerAngles.x,
                        y = go.transform.localRotation.eulerAngles.y,
                        z = go.transform.localRotation.eulerAngles.z,
                    },
                    scale = new
                    {
                        x = go.transform.localScale.x,
                        y = go.transform.localScale.y,
                        z = go.transform.localScale.z,
                    },
                    forward = new
                    {
                        x = go.transform.forward.x,
                        y = go.transform.forward.y,
                        z = go.transform.forward.z,
                    },
                    up = new
                    {
                        x = go.transform.up.x,
                        y = go.transform.up.y,
                        z = go.transform.up.z,
                    },
                    right = new
                    {
                        x = go.transform.right.x,
                        y = go.transform.right.y,
                        z = go.transform.right.z,
                    },
                },
                parentInstanceID = go.transform.parent?.gameObject.GetInstanceID() ?? 0, // 0 if no parent
                // Optionally include components, but can be large
                // components = go.GetComponents<Component>().Select(c => GetComponentData(c)).ToList()
                // Or just component names:
                componentNames = go.GetComponents<Component>()
                    .Select(c => c.GetType().FullName)
                    .ToList(),
            };
        }

        public static object GetComponentData(Component c)
        {
            if (c == null) return null;

            return new
            {
                typeName = c.GetType().FullName,
                instanceID = c.GetInstanceID()
            };
        }

        public static List<GameObject> GetAllSceneObjects(bool includeInactive = false)
        {
            return GameObject.FindObjectsOfType<Transform>(includeInactive)
                .Select(t => t.gameObject)
                .Distinct()
                .ToList();
        }

        public static object AddComponentWithProperties(GameObject target, string typeName, JObject properties)
        {
            var type = FindType(typeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
                return $"Invalid component type: {typeName}";

            if (type == typeof(Transform))
                return "Cannot add another Transform component.";

            try
            {
                var comp = target.AddComponent(type);
                if (properties != null)
                {
                    foreach (var prop in properties.Properties())
                    {
                        try { SetProperty(comp, prop.Name, prop.Value); } catch { }
                    }
                }
                return null; // success
            }
            catch (Exception e)
            {
                return $"Failed to add component: {e.Message}";
            }
        }
        public static Color ParseColor(JArray arr)
        {
            if (arr == null || arr.Count < 3)
                throw new Exception("Color must be an array of 3 or 4 floats [r, g, b, (a)]");

            float r = arr[0].ToObject<float>();
            float g = arr[1].ToObject<float>();
            float b = arr[2].ToObject<float>();
            float a = arr.Count > 3 ? arr[3].ToObject<float>() : 1f;

            return new Color(r, g, b, a);
        }
        public static GameObject CustomFinder(string target, string method)
        {
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(method))
                return null;

            var all = GameObject.FindObjectsOfType<GameObject>(true);

            return method switch
            {
                "by_name" => all.FirstOrDefault(go => go.name == target),
                "by_tag" => all.FirstOrDefault(go => go.CompareTag(target)),
                "by_path" => GameObject.Find(target),
                "by_id" => int.TryParse(target, out int id) ? all.FirstOrDefault(go => go.GetInstanceID() == id) : null,
                _ => null
            };
        }  
    }
}
