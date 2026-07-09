using System.Collections;
using CarDrawing.Drawing;
using CarDrawing.Generation;
using CarDrawing.Results;
using CarDrawing.UI;
using UnityEngine;

namespace CarDrawing.Core
{
    /// <summary>앱 상태. 키오스크 패널과 1:1 대응한다 (계획서 5장)</summary>
    public enum AppState
    {
        Attract,    // 대기 화면
        Drawing,    // 그리기
        Style,      // 스타일 선택
        Generating, // 생성 중
        Result      // 결과 비교
    }

    /// <summary>
    /// 패널 전환 상태머신 (계획서 4장: 대기→그리기→스타일→생성→결과→복귀).
    /// 코어 시스템의 중심으로, 패널 컨트롤러들의 이벤트를 받아 상태를 전환하고
    /// 세션 산출물(스케치 PNG 쌍·결과 PNG)의 저장과 생성 요청을 조율한다.
    /// 시간 정책(방치·자동 복귀)은 Config.json의 timing 값을 따른다.
    /// </summary>
    public class AppFlowManager : MonoBehaviour
    {
        /// <summary>대기 화면 패널</summary>
        [SerializeField] private AttractPanelController attractPanel;
        /// <summary>그리기 화면 패널</summary>
        [SerializeField] private DrawingPanelController drawingPanel;
        /// <summary>스타일 선택 패널</summary>
        [SerializeField] private StylePanelController stylePanel;
        /// <summary>생성 중 패널</summary>
        [SerializeField] private GeneratingPanelController generatingPanel;
        /// <summary>결과 패널</summary>
        [SerializeField] private ResultPanelController resultPanel;
        /// <summary>그리기 캔버스 (이중 RenderTexture 보유)</summary>
        [SerializeField] private DrawingCanvas canvas;
        /// <summary>ComfyUI 연동 클라이언트</summary>
        [SerializeField] private ComfyUIClient comfyClient;
        /// <summary>무입력 시간 측정기</summary>
        [SerializeField] private IdleWatcher idleWatcher;

        private AppState _state;
        private string _sessionId;
        private byte[] _linePng;
        private byte[] _colorPng;
        // 결과 비교·미리보기용 CPU 텍스처. 세션 종료(대기 복귀) 시 파괴한다
        private Texture2D _sketchTexture;
        private Texture2D _resultTexture;
        // 방치 팝업 상태 (그리기 화면 전용, 계획서 4장: 90초 팝업 + 30초 유예)
        private bool _idlePopupShown;
        private float _idlePopupShownAt;
        private float _stateEnteredAt;

        private void Start()
        {
            ResolveReferences();

            attractPanel.StartRequested += OnStartRequested;
            drawingPanel.CompleteRequested += OnDrawingCompleted;
            drawingPanel.ContinueRequested += OnIdleContinue;
            stylePanel.StyleChosen += OnStyleChosen;
            resultPanel.RetryRequested += OnRetryRequested;

            EnterState(AppState.Attract);

            // 서버 예열: 첫 관람객이 오기 전에 모델을 미리 적재해 콜드 스타트 첫 생성 타임아웃(인수인계 §6)을 없앤다
            if (comfyClient != null) comfyClient.Warmup();
        }

        private void OnDestroy()
        {
            // 패널이 매니저보다 오래 살아남는 경우(씬 재로드 등) 파괴된 대상을 가리키는 구독이 남지 않도록 해제
            if (attractPanel != null) attractPanel.StartRequested -= OnStartRequested;
            if (drawingPanel != null)
            {
                drawingPanel.CompleteRequested -= OnDrawingCompleted;
                drawingPanel.ContinueRequested -= OnIdleContinue;
            }
            if (stylePanel != null) stylePanel.StyleChosen -= OnStyleChosen;
            if (resultPanel != null) resultPanel.RetryRequested -= OnRetryRequested;

            CleanupSession();
        }

        // 인스펙터 연결이 빠져도 동작하도록 씬에서 탐색한다 (전시장 무인 운영 원칙).
        // 패널은 상태 전환으로 비활성화될 수 있어 includeInactive로 찾는다
        private void ResolveReferences()
        {
            if (attractPanel == null) attractPanel = FindObjectOfType<AttractPanelController>(true);
            if (drawingPanel == null) drawingPanel = FindObjectOfType<DrawingPanelController>(true);
            if (stylePanel == null) stylePanel = FindObjectOfType<StylePanelController>(true);
            if (generatingPanel == null) generatingPanel = FindObjectOfType<GeneratingPanelController>(true);
            if (resultPanel == null) resultPanel = FindObjectOfType<ResultPanelController>(true);
            if (canvas == null) canvas = FindObjectOfType<DrawingCanvas>(true);
            if (comfyClient == null) comfyClient = FindObjectOfType<ComfyUIClient>(true);
            if (idleWatcher == null) idleWatcher = FindObjectOfType<IdleWatcher>(true);
        }

        private void Update()
        {
            TimingConfig timing = ConfigManager.Config.timing;
            switch (_state)
            {
                case AppState.Drawing:
                    if (!_idlePopupShown && idleWatcher.IdleSeconds >= timing.idlePopupSeconds)
                    {
                        _idlePopupShown = true;
                        _idlePopupShownAt = Time.unscaledTime;
                        drawingPanel.ShowIdlePopup();
                    }
                    // 팝업에 응답([계속 그리기] 클릭) 없이 유예 시간이 지나면 대기 복귀.
                    // 마우스가 움직여도 버튼을 누르지 않으면 '무응답'으로 본다 (계획서 4장)
                    else if (_idlePopupShown && Time.unscaledTime - _idlePopupShownAt >= timing.idlePopupGraceSeconds)
                    {
                        EnterState(AppState.Attract);
                    }
                    break;

                case AppState.Style:
                    // 스타일 선택 화면에서 이탈한 관람객 대비 자동 복귀
                    if (idleWatcher.IdleSeconds >= timing.styleTimeoutSeconds)
                        EnterState(AppState.Attract);
                    break;

                case AppState.Result:
                    // 결과 화면 자동 복귀 (계획서 4장: 60초)
                    if (Time.unscaledTime - _stateEnteredAt >= timing.resultReturnSeconds)
                        EnterState(AppState.Attract);
                    break;
            }
        }

        private void EnterState(AppState next)
        {
            _state = next;
            _stateEnteredAt = Time.unscaledTime;
            if (idleWatcher != null) idleWatcher.ResetIdle();

            attractPanel.gameObject.SetActive(next == AppState.Attract);
            drawingPanel.gameObject.SetActive(next == AppState.Drawing);
            stylePanel.gameObject.SetActive(next == AppState.Style);
            generatingPanel.gameObject.SetActive(next == AppState.Generating);
            resultPanel.gameObject.SetActive(next == AppState.Result);

            switch (next)
            {
                case AppState.Attract:
                    CleanupSession();
                    break;
                case AppState.Drawing:
                    _idlePopupShown = false;
                    drawingPanel.HideIdlePopup();
                    break;
            }
        }

        // 세션 산출물 정리. CPU 텍스처는 명시적으로 파괴해야 메모리가 회수된다 (장시간 무인 운영 대비)
        private void CleanupSession()
        {
            _sessionId = null;
            _linePng = null;
            _colorPng = null;
            if (_sketchTexture != null) { Destroy(_sketchTexture); _sketchTexture = null; }
            if (_resultTexture != null) { Destroy(_resultTexture); _resultTexture = null; }
        }

        // ── 패널 이벤트 처리 ─────────────────────────────────

        private void OnStartRequested()
        {
            if (_state != AppState.Attract) return;
            // 새 관람객 — 이전 그림을 지우고 시작한다
            canvas.ClearAll();
            EnterState(AppState.Drawing);
        }

        private void OnDrawingCompleted()
        {
            if (_state != AppState.Drawing) return;
            if (canvas == null || !canvas.HasStrokes) return; // 빈 그림 제출 방지

            _sessionId = SessionStore.NewSessionId();
            _linePng = CanvasExporter.ToPng(canvas.LineLayer);
            _colorPng = CanvasExporter.ToPng(canvas.ColorLayer);

            try
            {
                SessionStore.SaveSketchPair(_sessionId, _linePng, _colorPng);
            }
            catch (System.Exception e)
            {
                // 디스크 기록 실패는 체험을 막지 않는다 — 생성은 메모리의 PNG로 계속 진행
                LogManager.Error($"[AppFlow] 스케치 저장 실패: {e.Message}");
            }

            // 미리보기·비교용 CPU 텍스처 (캔버스가 이후 바뀌어도 이 시점 그림을 유지)
            if (_sketchTexture != null) Destroy(_sketchTexture);
            _sketchTexture = new Texture2D(2, 2);
            _sketchTexture.LoadImage(_colorPng);

            stylePanel.SetPreview(_sketchTexture);
            EnterState(AppState.Style);
        }

        private void OnIdleContinue()
        {
            // 팝업에서 [계속 그리기]를 선택 — 방치 타이머를 처음부터 다시 센다
            if (idleWatcher != null) idleWatcher.ResetIdle();
            _idlePopupShown = false;
        }

        private void OnStyleChosen(StylePreset style)
        {
            if (_state != AppState.Style) return;

            EnterState(AppState.Generating);
            generatingPanel.Begin(_sketchTexture);
            LogManager.Info($"[AppFlow] 생성 요청: 세션 {_sessionId}, 스타일 {style.id}");
            comfyClient.Generate(_sessionId, _linePng, _colorPng, style, OnGenerationSucceeded, OnGenerationFailed);
        }

        private void OnGenerationSucceeded(byte[] resultPng)
        {
            // 생성 도중 상태가 바뀌었으면(이론상 없음) 늦게 온 결과를 버린다
            if (_state != AppState.Generating) return;

            try
            {
                SessionStore.SaveResult(_sessionId, resultPng);
            }
            catch (System.Exception e)
            {
                LogManager.Error($"[AppFlow] 결과 저장 실패: {e.Message}");
            }

            if (_resultTexture != null) Destroy(_resultTexture);
            _resultTexture = new Texture2D(2, 2);
            _resultTexture.LoadImage(resultPng);

            resultPanel.SetImages(_sketchTexture, _resultTexture);
            EnterState(AppState.Result);
        }

        private void OnGenerationFailed(string reason)
        {
            if (_state != AppState.Generating) return;

            // 계획서 12장: 해당 세션만 사과 안내 후 초기화, 앱은 계속 동작
            LogManager.Error($"[AppFlow] 생성 실패 (세션 {_sessionId}): {reason}");
            generatingPanel.ShowError(TextLibrary.Get("generating.error"));
            StartCoroutine(ReturnToAttractAfter(ConfigManager.Config.timing.errorNoticeSeconds));
        }

        private IEnumerator ReturnToAttractAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_state == AppState.Generating)
                EnterState(AppState.Attract);
        }

        private void OnRetryRequested()
        {
            if (_state != AppState.Result) return;
            // 그림을 유지한 채 그리기 화면으로 — 수정 후 재생성하는 체험 흐름
            EnterState(AppState.Drawing);
        }
    }
}
