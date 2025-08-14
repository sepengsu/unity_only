using Newtonsoft.Json.Linq;
using UnityEngine;
using Functions.GameObjectManager;
using Helpers;

namespace Handler
{
    public static class GameObjectHandler
    {
        public static object Handle(JObject command)
        {
            string action = command["action"]?.ToString()?.ToLower();
            if (string.IsNullOrEmpty(action))
                return Error("Missing 'action' field in params");

            return action switch
            {
                "create" => Create.Execute(command),
                "modify" => Modify.Execute(command),
                "delete" => Delete.Execute(command),
                "find" => Find.Execute(command),
                "get_components" => GetComponent.Execute(command),
                "add_component" => AddComponentToTarget.Execute(command),
                "remove_component" => RemoveComponentFromTarget.Execute(command),
                "set_component_property" => SetComponentPropertyOnTarget.Execute(command),


                // 추가적인 액션 핸들러

                _ => Error($"Unknown action '{action}' in manage_gameobject")
            };
        }

        private static object Error(string message) =>
            new { success = false, error = message };
    }
}
