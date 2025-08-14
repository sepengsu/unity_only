using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Helpers; // Vector3Helper, Response
using Utils = Functions.GameObjectManager.Utils;

namespace Functions.GameObjectManager
{
    public static class Modify
    {
        public static object Execute(JObject data)
        {
            return MainThreadDispatcher.Run(() =>
            {
                string targetName = data["target"]?.ToString();
                if (string.IsNullOrEmpty(targetName))
                    return Response.Error("Missing 'target' GameObject name.");

                GameObject go = GameObject.Find(targetName);
                if (go == null)
                    return Response.Error($"GameObject '{targetName}' not found.");

                bool modified = false;

                // 이름 변경
                string newName = data["name"]?.ToString();
                if (!string.IsNullOrEmpty(newName) && go.name != newName)
                {
                    go.name = newName;
                    modified = true;
                }

                // 위치, 회전, 스케일
                if (data["position"] is JArray posArr)
                {
                    go.transform.localPosition = Vector3Helper.ParseVector3(posArr);
                    modified = true;
                }
                if (data["rotation"] is JArray rotArr)
                {
                    go.transform.localEulerAngles = Vector3Helper.ParseVector3(rotArr);
                    modified = true;
                }
                if (data["scale"] is JArray scaleArr)
                {
                    go.transform.localScale = Vector3Helper.ParseVector3(scaleArr);
                    modified = true;
                }

                // 태그
                if (data["tag"] != null)
                {
                    try
                    {
                        go.tag = data["tag"].ToString();
                        modified = true;
                    }
                    catch { /* 무시 */ }
                }

                // 레이어
                if (data["layer"] != null)
                {
                    int layer = LayerMask.NameToLayer(data["layer"].ToString());
                    if (layer != -1)
                    {
                        go.layer = layer;
                        modified = true;
                    }
                }

                // Active 상태 설정
                if (data["setActive"]?.Type == JTokenType.Boolean)
                {
                    bool isActive = data["setActive"].ToObject<bool>();
                    if (go.activeSelf != isActive)
                    {
                        go.SetActive(isActive);
                        modified = true;
                    }
                }

                // 부모 설정
                if (data["parent"] != null)
                {
                    string parentName = data["parent"].ToString();
                    GameObject parentGo = GameObject.Find(parentName);
                    if (parentGo != null && go.transform.parent != parentGo.transform)
                    {
                        go.transform.SetParent(parentGo.transform, true);
                        modified = true;
                    }
                }

                if (modified)
                    return Response.Success($"GameObject '{go.name}' modified.", Utils.GetGameObjectData(go));
                else
                    return Response.Success($"No changes applied to '{go.name}'.", Utils.GetGameObjectData(go));
            }).Result;
        }
    }
}
