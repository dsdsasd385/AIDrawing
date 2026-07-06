using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EarthCoding.Episodes;
using EarthCoding.UI;

namespace EarthCoding.Core
{
    /// <summary>
    /// 프로그램 전체를 총괄하는 최상위 매니저. (작업계획서 19장 유지보수 구조의 GameManager)
    /// 씬에는 이 컴포넌트가 붙은 오브젝트 하나만 존재하며, 실행 시
    /// 로그 → 데이터 → 점수 → 캔버스/UI → 오디오 → 에피소드 → 스토리 순으로 초기화한다.
    /// 체험 플로우(인트로 → Episode1~5 → 클로징 → 처음으로)의 상태 전환을 담당한다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>전역 접근용 싱글턴</summary>
        public static GameManager Instance { get; private set; }

        /// <summary>스토리 화면 (인트로/클로징)</summary>
        private StoryScreen _story;

        /// <summary>에피소드 전환/실행 매니저</summary>
        private EpisodeManager _episodeManager;

        /// <summary>마지막 입력 시각 (미사용 시 자동 복귀 판정용)</summary>
        private float _lastInputTime;

        /// <summary>현재 인트로 화면(대기 화면) 표시 중인지 여부</summary>
        private bool _atIntro;

        /// <summary>화면 보호 모드 (인트로에서 장시간 방치 시 진입)</summary>
        private ScreenSaver _screenSaver;

        /// <summary>
        /// 프로그램 시작점. 모든 시스템을 순서대로 초기화하고 인트로를 띄운다.
        /// </summary>
        private void Awake()
        {
            Instance = this;

            // 1) 로그: 다른 시스템 초기화 중 오류도 기록해야 하므로 가장 먼저
            LogManager.Initialize();

            // 2) 외부 데이터(JSON) 와 저장된 점수
            DataManager.LoadAll();
            ScoreManager.Load();

            // 3) 입력/캔버스 등 UI 기반 시설
            CreateEventSystem();
            var canvas = CreateRootCanvas();

            // 4) 각 매니저 생성 (모두 GameManager 오브젝트에 부착해 수명 통일)
            gameObject.AddComponent<AudioManager>();

            var ui = gameObject.AddComponent<UIManager>();
            ui.Build(canvas);

            _episodeManager = gameObject.AddComponent<EpisodeManager>();
            _episodeManager.Initialize();
            _episodeManager.OnAllEpisodesFinished += ShowClosing;

            _story = gameObject.AddComponent<StoryScreen>();
            _story.Build(canvas);

            // 5) 전시장 운영 기능: 관리자 모드 + 화면 보호 모드
            var admin = gameObject.AddComponent<AdminPanel>();
            admin.Initialize(canvas);
            _screenSaver = gameObject.AddComponent<ScreenSaver>();
            _screenSaver.Initialize(canvas);

            // 6) 체험 플로우 시작: 인트로 화면
            ShowIntro();
        }

        /// <summary>
        /// 매 프레임: 입력 감지로 미사용 시간을 재고,
        /// 일정 시간 입력이 없으면 메인(인트로) 화면으로 자동 복귀한다. (작업계획서 21장)
        /// </summary>
        private void Update()
        {
            // 마우스/터치 입력이 있으면 미사용 타이머 리셋
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)
                _lastInputTime = Time.unscaledTime;

            // 인트로(대기 화면)가 아닐 때만 자동 복귀를 검사한다
            float timeout = DataManager.Config.IdleTimeoutSec;
            if (!_atIntro && timeout > 0 && Time.unscaledTime - _lastInputTime > timeout)
            {
                LogManager.Write("Info", "미사용 시간 초과 → 메인 화면 자동 복귀");
                ResetToIntro();
            }
        }

        /// <summary>인트로 화면을 띄운다. 시작 버튼 → Episode 1.</summary>
        private void ShowIntro()
        {
            _atIntro = true;
            if (_screenSaver != null) _screenSaver.AtIntro = true;   // 화면 보호 진입 허용
            UIManager.Instance.SetEpisodeVisible(false);
            _story.ShowIntro(() =>
            {
                _atIntro = false;
                if (_screenSaver != null) _screenSaver.AtIntro = false;
                _lastInputTime = Time.unscaledTime;
                _story.Hide();
                UIManager.Instance.SetEpisodeVisible(true);
                _episodeManager.StartEpisode(1);
            });
            AudioManager.Instance.PlayBgm("MainBgm");
        }

        /// <summary>
        /// 스토리 화면을 강제로 숨긴다. 관리자 모드에서 에피소드로 바로 이동할 때 사용한다.
        /// </summary>
        public void HideStory()
        {
            _atIntro = false;
            if (_screenSaver != null) _screenSaver.AtIntro = false;
            _lastInputTime = Time.unscaledTime;
            _story.Hide();
        }

        /// <summary>클로징 화면을 띄운다. 처음으로 버튼 → 전체 초기화 후 인트로.</summary>
        private void ShowClosing()
        {
            UIManager.Instance.SetEpisodeVisible(false);
            _story.ShowOutro(ResetToIntro);
        }

        /// <summary>
        /// 다음 체험자를 위한 전체 초기화 후 인트로로 복귀한다.
        /// (점수 초기화 + 조립 영역 초기화 - 작업계획서 21장 '전체 초기화')
        /// </summary>
        public void ResetToIntro()
        {
            ScoreManager.ResetAll();
            UIManager.Instance.Assembly.Clear();
            UIManager.Instance.ClosePopup();
            ShowIntro();
        }

        /// <summary>
        /// UI 입력 처리용 EventSystem 을 생성한다. (씬에 없으면 버튼/드래그가 동작하지 않음)
        /// </summary>
        private void CreateEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(transform);
        }

        /// <summary>
        /// 1920×1080 기준 해상도의 루트 캔버스를 생성한다. (작업계획서 2장 해상도)
        /// </summary>
        private Canvas CreateRootCanvas()
        {
            var go = new GameObject("RootCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 다른 해상도 모니터에서도 1920×1080 비율로 스케일되도록 설정
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }
    }
}
