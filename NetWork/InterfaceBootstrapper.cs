// 위치: Assets/Scripts/Network/InterfaceBootstrapper.cs

using UnityEngine;

namespace Network
{
    public class InterfaceBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            // 플레이 모드 시작 시 서버 자동 실행
            Interface.StartServer();
        }

        private void OnDestroy()
        {
            // 플레이 모드 종료 시 서버 정리
            Interface.StopServer();
        }
    }
}
