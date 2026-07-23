using System;
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
        Result,     // 결과 비교
        Admin       // 관리자 모드 (계획서 11장 — 숨김 키 조합 진입, 시간 정책 적용 안 함)
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
        /// <summary>기본 업로더 — Backblaze B2 (QR용, 계획서 9-2). 미설정이면 QR이 자동으로 숨는다</summary>
        [SerializeField] private B2Uploader b2Uploader;
        /// <summary>대안 업로더 — GCS. B2가 미설정일 때만 쓰인다</summary>
        [SerializeField] private GcsUploader gcsUploader;
        /// <summary>갤러리 게이트 필터 (계획서 10장)</summary>
        [SerializeField] private ContentFilter contentFilter;
        /// <summary>결과 영상 생성기 (마일스톤 ⑥) — 로컬 ComfyUI(AnimateDiff) 구현. 없거나 꺼져 있으면 이미지-only</summary>
        [SerializeField] private ComfyUIVideoGenerator videoGenerator;
        /// <summary>관리자 패널 (계획서 11장). Ctrl+Shift+지정키로 진입</summary>
        [SerializeField] private AdminPanelController adminPanel;
        /// <summary>ComfyUI 워치독 (계획서 12장). 무응답이면 새 체험 시작을 막는다</summary>
        [SerializeField] private ComfyUIWatchdog watchdog;

        private AppState _state;
        private string _sessionId;
        private byte[] _linePng;
        private byte[] _colorPng;
        // 결과 비교·미리보기용 CPU 텍스처. 세션 종료(대기 복귀) 시 파괴한다
        private Texture2D _sketchTexture;
        private Texture2D _resultTexture;
        // 현재 세션의 영상 생성이 백그라운드에서 진행 중인지. 결과 화면 자동 복귀를 보류하는 데 쓴다
        private bool _videoInProgress;
        // 이번 세션에서 선택된 스타일. 결과 후처리(픽셀화)가 참조한다
        private StylePreset _chosenStyle;
        // 관리자 터치 진입(구석 연타) 상태 — 키보드 없는 키오스크용
        private int _cornerTapCount;
        private float _cornerFirstTapAt;
        // 방치 팝업 상태 (그리기 화면 전용, 계획서 4장: 90초 팝업 + 30초 유예)
        private bool _idlePopupShown;
        private float _idlePopupShownAt;
        private float _stateEnteredAt;
        // 현재 이미지 생성 요청. 화면 상태가 다시 Generating이 되어도 이전 요청과 구분하기 위해 핸들 자체를 비교한다
        private ComfyUIClient.GenerationRequest _activeGeneration;
        // 현재 영상 생성 요청. 결과 화면을 떠날 때 서버 prompt까지 취소한다
        private VideoGenerationRequest _activeVideo;

        private void Start()
        {
            ResolveReferences();
            StorageMaintenance.RunOnce();

            attractPanel.StartRequested += OnStartRequested;
            drawingPanel.CompleteRequested += OnDrawingCompleted;
            drawingPanel.ContinueRequested += OnIdleContinue;
            stylePanel.StyleChosen += OnStyleChosen;
            stylePanel.BackRequested += OnBackToDrawing;
            generatingPanel.BackRequested += OnBackToDrawing;
            resultPanel.RetryRequested += OnRetryRequested;
            resultPanel.GalleryRequested += OnGalleryRequested;
            if (adminPanel != null) adminPanel.CloseRequested += OnAdminClose;
            if (watchdog != null) watchdog.HealthChanged += OnServerHealthChanged;

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
            if (stylePanel != null)
            {
                stylePanel.StyleChosen -= OnStyleChosen;
                stylePanel.BackRequested -= OnBackToDrawing;
            }
            if (generatingPanel != null) generatingPanel.BackRequested -= OnBackToDrawing;
            if (resultPanel != null)
            {
                resultPanel.RetryRequested -= OnRetryRequested;
                resultPanel.GalleryRequested -= OnGalleryRequested;
            }
            if (adminPanel != null) adminPanel.CloseRequested -= OnAdminClose;
            if (watchdog != null) watchdog.HealthChanged -= OnServerHealthChanged;

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
            if (b2Uploader == null) b2Uploader = FindObjectOfType<B2Uploader>(true);
            if (gcsUploader == null) gcsUploader = FindObjectOfType<GcsUploader>(true);
            if (contentFilter == null) contentFilter = FindObjectOfType<ContentFilter>(true);
            if (videoGenerator == null) videoGenerator = FindObjectOfType<ComfyUIVideoGenerator>(true);
            if (adminPanel == null) adminPanel = FindObjectOfType<AdminPanelController>(true);
            if (watchdog == null) watchdog = FindObjectOfType<ComfyUIWatchdog>(true);
        }

        private void Update()
        {
            if (IsAdminHotkeyPressed() || IsAdminCornerTapped())
            {
                // 관리자 모드는 어느 화면에서든 열린다. 닫으면 대기 화면으로 (진행 중이던 세션은 버린다)
                if (_state != AppState.Admin) EnterState(AppState.Admin);
                return;
            }

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
                    // 결과 화면 자동 복귀 (계획서 4장: 60초).
                    // 단 영상 생성이 진행 중이면 보류한다 — 영상(웜 ~45초, 콜드는 60초 초과)이 도착하기 전에
                    // 세션이 만료되는 문제 실측(2026-07-13). 생성기 타임아웃(video.generateTimeoutSeconds)이
                    // 반드시 콜백을 부르므로 무한 보류는 없지만, 콜백 유실 대비 상한을 한 번 더 둔다
                    float resultLimit = _videoInProgress
                        ? timing.resultReturnSeconds + ConfigManager.Config.video.generateTimeoutSeconds
                        : timing.resultReturnSeconds;
                    if (Time.unscaledTime - _stateEnteredAt >= resultLimit)
                        EnterState(AppState.Attract);
                    break;
            }
        }

        // 관리자 모드 진입 키 조합 (계획서 11장: Ctrl+Shift+F12). 주 키는 Config.json에서 바꿀 수 있다.
        // 관람객이 우연히 누를 수 없는 조합이어야 하므로 Ctrl·Shift 동시 입력을 요구한다
        private bool IsAdminHotkeyPressed()
        {
            if (adminPanel == null) return false;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!ctrl || !shift) return false;

            // 설정값이 오타여도 죽지 않는다 — 기본 F12로 폴백 (무인 운영)
            if (!Enum.TryParse(ConfigManager.Config.admin.hotkey, true, out KeyCode key))
                key = KeyCode.F12;
            return Input.GetKeyDown(key);
        }

        // 터치 진입 (계획서 11장 보강, 2026-07-14): 전시 키오스크에는 키보드가 없다.
        // 화면 좌측 하단 구석(기본 100×100px)을 정해진 시간 안에 연타(기본 3초 내 10회)하면 관리자 모드로 간다.
        // 관람객이 우연히 채울 수 없는 조합이고(그리기 캔버스 밖 구석 + 연타), 운영자는 손가락만으로 들어올 수 있다.
        // 화면 위 어떤 UI가 덮고 있든 동작한다 — Input 폴링이라 uGUI 레이캐스트와 무관하다
        private bool IsAdminCornerTapped()
        {
            if (adminPanel == null || _state == AppState.Admin) return false;
            if (!Input.GetMouseButtonDown(0)) return false;

            AdminConfig cfg = ConfigManager.Config.admin;
            float size = Mathf.Max(20f, cfg.cornerSize);
            Vector3 p = Input.mousePosition;
            if (p.x > size || p.y > size || p.x < 0f || p.y < 0f)
            {
                // 구석 밖을 누르면 연타가 끊긴 것으로 본다 (그리기 중 우연 누적 방지)
                _cornerTapCount = 0;
                return false;
            }

            float now = Time.unscaledTime;
            // 첫 탭이거나 인정 시간이 지났으면 처음부터 다시 센다
            if (_cornerTapCount == 0 || now - _cornerFirstTapAt > Mathf.Max(1f, cfg.cornerTapSeconds))
            {
                _cornerFirstTapAt = now;
                _cornerTapCount = 1;
                return false;
            }

            _cornerTapCount++;
            if (_cornerTapCount < Mathf.Max(2, cfg.cornerTapCount)) return false;

            _cornerTapCount = 0;
            LogManager.Info("[AppFlow] 관리자 모드 진입 (구석 연타)");
            return true;
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
            if (adminPanel != null) adminPanel.gameObject.SetActive(next == AppState.Admin);

            switch (next)
            {
                case AppState.Attract:
                    CleanupSession();
                    // 서버가 죽어 있으면 대기 화면에 안내를 띄운 채로 둔다 (시작 클릭도 막힌다)
                    UpdateAttractServerNotice();
                    break;
                case AppState.Drawing:
                    _idlePopupShown = false;
                    drawingPanel.HideIdlePopup();
                    break;
                case AppState.Admin:
                    // 관리자 화면에서는 시간 정책을 적용하지 않는다 (운영자가 오래 머물 수 있다)
                    CleanupSession();
                    break;
            }
        }

        // ── 워치독 (계획서 12장) ──────────────────────────────

        // 서버가 죽으면 새 체험 시작을 막고 대기 화면에 안내를 띄운다. 앱 자체는 계속 돈다.
        // 진행 중이던 세션은 건드리지 않는다 — 생성 타임아웃이 알아서 사과 문구로 마무리한다
        private void OnServerHealthChanged(bool healthy)
        {
            UpdateAttractServerNotice();
            if (!healthy) LogManager.Warn("[AppFlow] ComfyUI 무응답 — 새 체험 시작을 잠급니다");
            else LogManager.Info("[AppFlow] ComfyUI 복구 — 체험 시작 잠금 해제");
        }

        private void UpdateAttractServerNotice()
        {
            if (attractPanel == null) return;
            bool down = watchdog != null && !watchdog.IsHealthy;
            attractPanel.SetNotice(down ? TextLibrary.Get("attract.serverDown") : null);
        }

        // 세션 산출물 정리. CPU 텍스처는 명시적으로 파괴해야 메모리가 회수된다 (장시간 무인 운영 대비)
        private void CleanupSession()
        {
            CancelActiveGeneration();
            CancelActiveVideo();
            _sessionId = null;
            _linePng = null;
            _colorPng = null;
            _videoInProgress = false; // 세션이 끝나면 보류도 해제 — 늦은 콜백은 세션 ID 가드가 걸러낸다
            if (_sketchTexture != null) { Destroy(_sketchTexture); _sketchTexture = null; }
            if (_resultTexture != null) { Destroy(_resultTexture); _resultTexture = null; }
        }

        // ── 패널 이벤트 처리 ─────────────────────────────────

        private void OnStartRequested()
        {
            if (_state != AppState.Attract) return;

            // 서버가 죽어 있으면 시작을 막는다 (계획서 12장: 생성 기능 잠금 + 안내).
            // 그리게 두고 마지막에 실패시키는 것보다, 그리기 전에 막는 편이 관람객에게 덜 억울하다
            if (watchdog != null && !watchdog.IsHealthy)
            {
                UpdateAttractServerNotice();
                return;
            }

            // 새 관람객 — 이전 그림을 지우고 시작한다
            canvas.ClearAll();
            EnterState(AppState.Drawing);
        }

        // 스타일·생성 화면에서 [다시 그리기] — 그림을 유지한 채 그리기 화면으로 되돌린다.
        // 생성 중이면 UI만 닫지 않고 ComfyUI 대기/실행 작업도 취소한다.
        private void OnBackToDrawing()
        {
            if (_state != AppState.Style && _state != AppState.Generating) return;
            CancelActiveGeneration();
            EnterState(AppState.Drawing);
        }

        // 관리자 모드 종료 — 대기 화면으로 복귀 (계획서 11장)
        private void OnAdminClose()
        {
            if (_state != AppState.Admin) return;
            EnterState(AppState.Attract);
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
            if (_activeGeneration != null && !_activeGeneration.IsTerminal) return;

            _chosenStyle = style; // 결과 후처리(픽셀화 등)가 스타일 설정을 참조한다
            EnterState(AppState.Generating);
            generatingPanel.Begin(_sketchTexture);
            LogManager.Info($"[AppFlow] 생성 요청: 세션 {_sessionId}, 스타일 {style.id}");
            _activeGeneration = comfyClient.GenerateTracked(
                _sessionId, _linePng, _colorPng, style, OnGenerationSucceeded, OnGenerationFailed);
        }

        private bool IsCurrentGeneration(ComfyUIClient.GenerationRequest request)
        {
            return request != null && ReferenceEquals(_activeGeneration, request) &&
                   request.SessionId == _sessionId && _state == AppState.Generating;
        }

        private void CancelActiveGeneration()
        {
            if (_activeGeneration == null) return;
            comfyClient?.Cancel(_activeGeneration);
            _activeGeneration = null;
        }

        private void CancelActiveVideo()
        {
            if (_activeVideo == null) return;
            videoGenerator?.Cancel(_activeVideo);
            _activeVideo = null;
            _videoInProgress = false;
        }

        private void OnGenerationSucceeded(ComfyUIClient.GenerationRequest request, byte[] resultPng)
        {
            if (!IsCurrentGeneration(request))
            {
                LogManager.Warn($"[AppFlow] 만료된 생성 결과 무시: 세션 {request?.SessionId}, 요청 {request?.GenerationId}");
                return;
            }
            _activeGeneration = null;

            // 픽셀아트 등 후처리 스타일이면 여기서 변환 — 이후의 저장·표시·QR 업로드·영상이 전부 같은 그림을 쓴다
            resultPng = PixelArtFilter.Apply(resultPng, _chosenStyle);

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

            // QR용 업로드 (계획서 9-2: 생성 직후 비동기, 실패해도 체험 계속).
            // 세션 ID를 붙잡아 두는 이유: 업로드가 끝났을 때 이미 다음 관람객 세션이면 QR을 붙이지 않아야 한다
            string uploadSessionId = _sessionId;
            IResultUploader uploader = ActiveUploader;
            if (uploader != null)
            {
                uploader.Upload(uploadSessionId, resultPng, url =>
                {
                    if (url == null) return; // 실패 — QR 영역이 숨겨진 채 유지된다
                    if (_state != AppState.Result || _sessionId != uploadSessionId) return; // 늦게 온 결과는 버린다
                    resultPanel.ShowQr(url);
                });
            }

            StartVideoGeneration(resultPng);
        }

        // 결과 영상화 (마일스톤 ⑥): 결과 화면을 먼저 보여주고 백그라운드에서 영상을 만든다.
        // 도착하면 이미지→영상 교체, 실패·타임아웃이면 이미지가 그대로 남는다 (폴백 — 관람객은 실패를 모른다)
        private void StartVideoGeneration(byte[] resultPng)
        {
            CancelActiveVideo();
            _videoInProgress = false; // 이전 세션의 잔재 플래그 제거 (다시 그리기 경로)
            if (videoGenerator == null || !videoGenerator.IsEnabled) return;

            string videoSessionId = _sessionId;
            _videoInProgress = true; // 진행 중에는 결과 화면 자동 복귀를 보류한다 (Update의 Result 분기)
            resultPanel.ShowVideoPending();
            LogManager.Info($"[AppFlow] 영상 생성 시작 (백그라운드): 세션 {videoSessionId}");

            // 스타일을 넘겨 영상도 같은 화풍을 유지하게 한다 (픽셀아트 LoRA·픽셀화 — 안 넘기면 영상이 매끈하게 재해석됨)
            VideoGenerationRequest request = null;
            request = videoGenerator.Generate(videoSessionId, resultPng, _linePng, _chosenStyle, mp4 =>
            {
                if (!ReferenceEquals(_activeVideo, request)) return;
                _activeVideo = null;
                // 그 사이 다음 관람객으로 넘어갔으면 표시하지 않는다 (파일은 저장해 기록만 남긴다)
                string path = null;
                try { path = SessionStore.SaveResultVideo(videoSessionId, mp4); }
                catch (System.Exception e) { LogManager.Error($"[AppFlow] 영상 저장 실패: {e.Message}"); }

                if (_state != AppState.Result || _sessionId != videoSessionId) return;
                _videoInProgress = false;
                if (path == null) { resultPanel.HideVideoPending(); return; }
                resultPanel.ShowVideo(path);
                // 영상이 체험의 하이라이트라 감상 시간을 새로 준다 — 자동 복귀 타이머 리셋
                _stateEnteredAt = Time.unscaledTime;
                LogManager.Info($"[AppFlow] 영상 표시: {path}");
            }, reason =>
            {
                if (!ReferenceEquals(_activeVideo, request)) return;
                _activeVideo = null;
                // 실패는 로그만 — 결과 화면은 이미지로 계속 (Generate 내부가 이미 Warn을 남겼다)
                if (_state == AppState.Result && _sessionId == videoSessionId)
                {
                    _videoInProgress = false; // 보류를 풀어 자동 복귀 타이머가 다시 돌게 한다
                    resultPanel.HideVideoPending();
                }
            });
        }

        // 설정이 갖춰진 업로더를 고른다. 기본 B2(무료 티어), 대안 GCS — IResultUploader 계약 덕에 흐름은 동일
        private IResultUploader ActiveUploader
        {
            get
            {
                if (b2Uploader != null && b2Uploader.IsConfigured) return b2Uploader;
                if (gcsUploader != null && gcsUploader.IsConfigured) return gcsUploader;
                return null;
            }
        }

        private void OnGalleryRequested()
        {
            if (_state != AppState.Result || _sessionId == null) return;

            // 세션 ID를 붙잡아 둔다 — 필터 판정은 세션이 끝난 뒤 도착할 수 있고, 파일은 Sessions/에 남아 있다
            string sessionId = _sessionId;
            LogManager.Info($"[AppFlow] 갤러리 전시 신청: 세션 {sessionId}");

            if (contentFilter == null)
            {
                // 공개 전시는 판정 불가를 통과로 간주하지 않는다. 운영자가 관리자 화면에서 복원할 수 있다.
                string dest = SessionStore.AddToQuarantine(sessionId);
                LogManager.Warn($"[AppFlow] 필터 없음 — 안전을 위해 격리: {dest}");
                return;
            }

            contentFilter.Evaluate(sessionId, pass =>
            {
                // 통과 → 갤러리(슬라이드쇼가 폴더 감시로 자동 반영), 부적합/판정 불가 → 격리(관리자 복원 가능)
                string dest = pass ? SessionStore.AddToGallery(sessionId) : SessionStore.AddToQuarantine(sessionId);
                LogManager.Info($"[AppFlow] 갤러리 판정 {(pass ? "통과" : "격리")} (세션 {sessionId}): {dest}");
            });
        }

        private void OnGenerationFailed(ComfyUIClient.GenerationRequest request, string reason)
        {
            if (!IsCurrentGeneration(request))
            {
                LogManager.Warn($"[AppFlow] 만료된 생성 오류 무시: 세션 {request?.SessionId}, 요청 {request?.GenerationId}, 사유 {reason}");
                return;
            }
            _activeGeneration = null;

            // 계획서 12장: 해당 세션만 사과 안내 후 초기화, 앱은 계속 동작
            LogManager.Error($"[AppFlow] 생성 실패 (세션 {request.SessionId}, 요청 {request.GenerationId}, " +
                             $"prompt {request.PromptId ?? "없음"}, 종류 {request.FailureKind}, " +
                             $"상태 {request.Status}, 경과 {request.ElapsedSeconds:F1}초): {reason}");
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
            CancelActiveVideo();
            resultPanel.HideVideoPending();
            EnterState(AppState.Drawing);
        }
    }
}
