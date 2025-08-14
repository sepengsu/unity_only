using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using Parser;

namespace Network
{
    public class UIInterface : MonoBehaviour
    {
        [Header("UI 연결")]
        public TMP_InputField jsonInputField;
        public Button runCommandButton;
        public TMP_Text outputText;

        private void Start()
        {
            if (runCommandButton != null)
                runCommandButton.onClick.AddListener(OnRunCommandClicked);

            Debug.Log("[UIInterface] Start: " +
                      $"jsonInputField={(jsonInputField != null)}, " +
                      $"runCommandButton={(runCommandButton != null)}, " +
                      $"outputText={(outputText != null)}");
        }

        // ✅ 비동기처럼 처리되지만 Parser는 동기
        private async void OnRunCommandClicked()
        {
            string json = jsonInputField?.text?.Trim();
            if (string.IsNullOrEmpty(json))
            {
                outputText.text = "Please enter JSON command.";
                return;
            }

            outputText.text = "Processing...";

            try
            {
                // ✅ Parser는 동기지만 백그라운드에서 실행
                string result = await Task.Run(() =>
                {
                    return CommandParser.HandleCommand(json);  // 그대로 동기 호출
                });

                outputText.text = result;
            }
            catch (System.Exception ex)
            {
                outputText.text = $"ERROR: {ex.Message}";
                Debug.LogError("[UIInterface] Command exception: " + ex);
            }
        }
    }
}
