using UnityEngine;
using Network;

public static class AutoStartInterface
{
    // 씬 로드 후 한 번만 자동 시작
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Init()
    {
        Debug.Log("🚀 AutoStartInterface.Init: Starting Unity MCP server...");
        Interface.StartServer();

        // 플레이/앱 종료 시 반드시 서버 종료
        Application.quitting += Interface.StopServer;
    }

    // (선택) 스플래시 전에 간단 로그만
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    public static void PreInit()
    {
        Debug.Log("🔄 Pre-init MCP server...");
    }
}
