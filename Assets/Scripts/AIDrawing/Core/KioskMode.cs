using UnityEngine;

namespace CarDrawing.Core
{
    /// <summary>
    /// 키오스크 표시 설정 (계획서 12장). 코어 시스템에 속하며 씬 시작 시 한 번 적용한다.
    /// 앱 안에서 할 수 있는 것만 한다 — Alt+Tab·작업표시줄 차단은 Windows 키오스크 설정의 몫이다(계획서 12장).
    /// 커서는 그리기에 필요하므로 숨기지 않고, 창 밖으로 나가지 않게 가둔다.
    /// </summary>
    public class KioskMode : MonoBehaviour
    {
        /// <summary>전체화면으로 고정할지. 에디터에서는 무시된다 (Game 뷰는 이미 창)</summary>
        [SerializeField] private bool fullscreen = true;
        /// <summary>커서를 앱 창 안에 가둘지. 두 번째 디스플레이(갤러리)로 커서가 넘어가는 것을 막는다</summary>
        [SerializeField] private bool confineCursor = true;
        /// <summary>화면 보호기·절전 진입 방지 (무인 전시)</summary>
        [SerializeField] private bool keepScreenOn = true;

        private void Start()
        {
            if (Application.isEditor) return; // 에디터에서 전체화면·커서 가두기는 개발을 방해한다

            if (fullscreen)
            {
                Resolution r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
            }

            if (confineCursor) Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true; // 마우스로 그리는 체험이라 커서는 보여야 한다

            if (keepScreenOn) Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // 창이 포커스를 잃어도 계속 돌아야 한다 — 워치독·갤러리 슬라이드쇼가 멈추면 안 된다
            Application.runInBackground = true;

            LogManager.Info($"[Kiosk] 키오스크 모드 적용 (fullscreen={fullscreen}, confineCursor={confineCursor})");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // 알림창 등으로 포커스를 뺏겼다 돌아오면 커서 가두기가 풀린다 — 다시 건다
            if (!Application.isEditor && hasFocus && confineCursor)
                Cursor.lockState = CursorLockMode.Confined;
        }
    }
}
