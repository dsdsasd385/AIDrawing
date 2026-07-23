using System.Collections;
using CarDrawing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.UI
{
    /// <summary>
    /// 네트워크 연결 상태를 화면 우측 상단 구석에 작은 원형 표시등으로 보여준다.
    /// 연결되면 표시등을 숨기고, 끊겼을 때만 빨간 점을 띄운다 — 문제가 있을 때만 눈에 띄게 한다.
    /// QR 업로드가 인터넷을 쓰므로 운영자가 한눈에 상태를 확인하게 하는 안내용이다 —
    /// 인터넷이 없어도 체험(로컬 생성·표시)은 계속되므로 이 표시등은 기능을 막지 않는다.
    ///
    /// 설계:
    /// - 자체 오버레이 캔버스(sortingOrder 높게)를 만들어 어떤 패널·화면 위에서도 항상 같은 자리에 보인다
    ///   (패널 상태머신과 무관, 관람객 입력도 가로채지 않는다).
    /// - 판정은 Application.internetReachability — 네트워크 인터페이스 유무만 본다. 외부 서버에 의존하지
    ///   않아 표시등 자체가 실패할 일이 없다(무인 운영). 단 LAN은 붙었는데 WAN(인터넷)만 죽은 경우는
    ///   초록으로 나올 수 있다 — 실제 인터넷 도달까지 보려면 CheckOnce를 HTTP 프로브로 바꾸면 된다.
    /// - 씬 배선 없이 자동 생성한다(RuntimeInitializeOnLoadMethod) — 어느 씬에서도 그냥 뜬다.
    /// </summary>
    public class NetworkIndicator : MonoBehaviour
    {
        // 눈에 거슬리지 않는 아주 작은 크기·여백 (1920×1080 설계 기준 픽셀). 우상단 구석에서 안쪽으로 11px
        private const float DotSize = 3f;
        private const float Offset = 11f;
        // 상태 확인 주기(초). 로컬 조회라 비용이 없어 촘촘히 봐도 무방하다
        private const float CheckIntervalSeconds = 2f;

        // 끊겼을 때만 보여줄 빨간 점 (연결되면 표시등 자체를 숨긴다)
        private static readonly Color DisconnectedColor = new Color(0.87f, 0.27f, 0.25f); // 빨강

        private static NetworkIndicator _instance;

        private Image _dot;
        private bool? _connected; // 첫 판정 전 null (첫 상태를 반드시 한 번 반영·로그하도록)

        // 씬 배선 없이 자동 생성 — 전시 운영자가 컴포넌트를 붙이는 걸 잊어도 표시등은 항상 뜬다.
        // 에디트 모드에서는 실행되지 않는다(플레이/빌드 전용)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("NetworkIndicator");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NetworkIndicator>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BuildDot();
        }

        private void OnEnable()
        {
            StartCoroutine(CheckRoutine());
        }

        // 자체 오버레이 캔버스 + 원형 표시등. 다른 UI보다 위(sortingOrder 높게) 그려 항상 보이게 한다
        private void BuildDot()
        {
            var canvasGo = new GameObject("NetworkIndicatorCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // KioskCanvas(기본 0)보다 위에 그린다

            // 다른 캔버스와 같은 스케일 규칙 (설계 1920×1080, Expand — 인수인계 §6 해상도 대응)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _dot = UiBuilder.CreateImage(canvasGo.transform, "NetworkDot", DisconnectedColor);
            _dot.sprite = UiBuilder.CircleSprite; // 매끄러운 원 (안티에일리어싱)
            _dot.type = Image.Type.Simple;
            _dot.raycastTarget = false;           // 관람객 입력(전체화면 클릭 시작 등)을 가로채지 않게

            var rt = (RectTransform)_dot.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); // 우상단 고정
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(DotSize, DotSize);
            rt.anchoredPosition = new Vector2(-Offset, -Offset); // xy 모두 -11

            _dot.gameObject.SetActive(false); // 첫 판정 전엔 숨김 (연결 상태를 기본으로 가정)
        }

        private IEnumerator CheckRoutine()
        {
            var wait = new WaitForSecondsRealtime(CheckIntervalSeconds);
            while (true)
            {
                SetState(Application.internetReachability != NetworkReachability.NotReachable);
                yield return wait;
            }
        }

        private void SetState(bool connected)
        {
            if (_connected == connected) return; // 바뀔 때만 반영·로그
            _connected = connected;
            // 연결되면 표시등을 숨기고(false), 끊기면 빨간 점을 보여준다
            if (_dot != null) _dot.gameObject.SetActive(!connected);
            LogManager.Info($"[Network] {(connected ? "연결됨 — 표시등 숨김" : "끊김 — 빨강 표시")} · QR 업로드 {(connected ? "가능" : "불가 (QR만 숨김, 체험은 계속)")}");
        }
    }
}
