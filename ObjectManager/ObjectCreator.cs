using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ObjectManager
{
    public class ObjectCreator : MonoBehaviour
    {
        public static GameObject CreateObject(JObject data)
        {
            // --- 기본 파라미터 ---
            string name = data["name"]?.ToString() ?? "NewObject";
            string primitiveType = data["primitiveType"]?.ToString(); // Optional
            GameObject go;

            // --- 프리미티브 종류에 따라 생성 ---
            if (!string.IsNullOrEmpty(primitiveType))
            {
                if (!Enum.TryParse(primitiveType, out PrimitiveType type))
                {
                    Debug.LogWarning($"Invalid primitiveType '{primitiveType}', creating empty object.");
                    go = new GameObject(name);
                }
                else
                {
                    go = GameObject.CreatePrimitive(type);
                    go.name = name;
                }
            }
            else
            {
                go = new GameObject(name);
            }

            // --- 위치 / 회전 / 스케일 설정 ---
            if (data["position"] is JArray pos && pos.Count == 3)
            {
                go.transform.position = ParseVector3(pos);
            }

            if (data["rotation"] is JArray rot && rot.Count == 3)
            {
                go.transform.eulerAngles = ParseVector3(rot);
            }

            if (data["scale"] is JArray scale && scale.Count == 3)
            {
                go.transform.localScale = ParseVector3(scale);
            }

            Debug.Log($"[ObjectCreator] Created: {go.name} at {go.transform.position}");
            return go;
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
