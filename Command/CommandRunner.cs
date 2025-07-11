using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json.Linq;
using Command;  // 위 Dispatcher 네임스페이스

public class CommandRunner : MonoBehaviour
{
    public TMP_InputField jsonInputField;
    public Button runButton;

    void Start()
    {
        runButton.onClick.AddListener(RunCommandFromInput);
    }

    public void RunCommandFromInput()
    {
        string json = jsonInputField.text;
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[CommandRunner] 입력이 비어있습니다.");
            return;
        }

        try
        {
            JObject jobj = JObject.Parse(json);
            var result = CommandDispatcher.Execute(jobj);
            Debug.Log($"[CommandRunner] 실행 결과: {result}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CommandRunner] JSON 파싱 오류: {e.Message}");
        }
    }
}
