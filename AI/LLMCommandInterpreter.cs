using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System.Collections;

public class LLMCommandInterpreter : MonoBehaviour
{
    [TextArea(3, 10)]
    public string userInput;

    public void RunLLMCommand()
    {
        StartCoroutine(SendPrompt(userInput));
    }

    IEnumerator SendPrompt(string prompt)
    {
        var apiKey = "YOUR_OPENAI_API_KEY";
        var url = "https://api.openai.com/v1/chat/completions";

        var payload = new
        {
            model = "gpt-4o-mini", // or gpt-3.5-turbo
            messages = new[]
            {
                new { role = "system", content = "You are a Unity assistant. Convert instructions into Unity JSON commands." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2
        };

        string jsonBody = JsonUtility.ToJson(payload);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"LLM API Error: {request.error}");
        }
        else
        {
            var result = request.downloadHandler.text;
            Debug.Log($"LLM Response: {result}");

            JObject parsed = JObject.Parse(result);
            string jsonCommand = parsed["choices"]?[0]?["message"]?["content"]?.ToString();

            if (!string.IsNullOrEmpty(jsonCommand))
            {
                JObject cmd = JObject.Parse(jsonCommand);
                var executionResult = Command.CommandDispatcher.Execute(cmd);
                Debug.Log($"Command Executed: {executionResult}");
            }
        }
    }
}
