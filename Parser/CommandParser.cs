#nullable enable
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Handler;
using Helpers;

namespace Parser
{
    public static class CommandParser
    {
        public static string HandleCommand(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return WrapError("Empty command");

                JObject root = JObject.Parse(json);
                string? type = root["type"]?.ToString()?.ToLower();
                JObject @params = root["params"] as JObject ?? new JObject();

                if (string.IsNullOrEmpty(type))
                    return WrapError("Missing 'type' field");

                object result = type switch
                {
                    "manage_gameobject" => GameObjectHandler.Handle(@params),
                    "manage_asset" => AssetHandler.Handle(@params),
                    _ => Response.Error($"Unknown type '{type}'")
                };

                // ✅ 에러 객체인지 확인
                if (result is JObject resObj && resObj["status"]?.ToString() == "error")
                {
                    return JsonConvert.SerializeObject(resObj);
                }

                return JsonConvert.SerializeObject(new
                {
                    status = "success",
                    result
                });
            }
            catch (JsonReaderException je)
            {
                return WrapError("Invalid JSON", je.Message);
            }
            catch (Exception e)
            {
                return WrapError("Unhandled exception", e.Message);
            }
        }

        private static string WrapError(string message, string? detail = null)
        {
            return JsonConvert.SerializeObject(new
            {
                status = "error",
                error = message,
                detail
            });
        }
    }
}
