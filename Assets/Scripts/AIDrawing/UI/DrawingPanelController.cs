using System;
using CarDrawing.Core;
using CarDrawing.Drawing;
using CarDrawing.Results;
using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;

namespace CarDrawing.UI
{
    /// <summary>
    /// 그리기 화면(DrawingPanel)의 도구 UI를 관리한다.
    /// UI 시스템에 속하며 DrawingCanvas를 조작한다.
    /// 색상 팔레트·펜 굵기·지우개·undo·전체 지우기 버튼은 런타임에 생성한다
    /// (디자인 리소스 적용 전까지 씬 수동 배치를 최소화하기 위함).
    /// [완성!]은 CompleteRequested 이벤트로 AppFlowManager에 알리고,
    /// 방치 팝업(계획서 4장: 90초 무입력)은 ShowIdlePopup/HideIdlePopup으로 제어된다.
    /// </summary>
    public class DrawingPanelController : MonoBehaviour
    {
        /// <summary>관람객이 [완성!]을 눌렀을 때 (빈 그림은 발생하지 않음)</summary>
        public event Action CompleteRequested;
        /// <summary>방치 팝업에서 [계속 그리기]를 선택했을 때</summary>
        public event Action ContinueRequested;

        /// <summary>조작 대상 캔버스</summary>
        [SerializeField] private DrawingCanvas canvas;
        /// <summary>색상 팔레트 버튼이 생성될 컨테이너 (세로 배치)</summary>
        [SerializeField] private RectTransform paletteContainer;
        /// <summary>도구 버튼(굵기/지우개/undo/지우기/완성)이 생성될 컨테이너 (가로 배치)</summary>
        [SerializeField] private RectTransform toolbarContainer;

        [SerializeField] private Button done;

        /// <summary>고정 색상 팔레트. 계획서 6장: 고정 색상 8~12개</summary>
        private static readonly Color[] Palette =
        {
            Color.black,
            new Color(0.86f, 0.20f, 0.18f), // 빨강
            new Color(0.95f, 0.55f, 0.15f), // 주황
            new Color(0.98f, 0.85f, 0.25f), // 노랑
            new Color(0.30f, 0.70f, 0.30f), // 초록
            new Color(0.25f, 0.55f, 0.90f), // 파랑
            new Color(0.45f, 0.30f, 0.75f), // 보라
            new Color(0.95f, 0.60f, 0.75f), // 분홍
            new Color(0.55f, 0.35f, 0.20f), // 갈색
            new Color(0.55f, 0.55f, 0.58f), // 회색
        };

        /// <summary>펜 굵기 3단계 (픽셀 반지름). 계획서 6장</summary>
        private static readonly float[] PenSizes = { 4f, 8f, 16f };

        // 지우개 토글 버튼의 배경 (활성 상태 표시용)
        private Image _eraserButtonImage;
        // 방치 팝업 루트 (첫 표시 시점에 생성)
        private GameObject _idlePopup;
        // 팔레트 각 버튼 위에 겹쳐 둔 선택 표시 링 (지금 고른 색 하나만 켠다)
        private readonly Image[] _paletteRings = new Image[Palette.Length];

        private void Start()
        {
            if (canvas == null)
                canvas = FindObjectOfType<DrawingCanvas>();

            BuildPalette();
            BuildToolbar();
            SetColor(Palette[0]); // 시작 색(검정)을 캔버스·표시에 함께 반영
        }

        /// <summary>브러시 색을 바꾸고 지우개 모드를 해제한다 (색을 고르면 그리기 의도로 간주).</summary>
        public void SetColor(Color color)
        {
            canvas.BrushColor = color;
            SetEraser(false);
            HighlightSelected(color);
        }

        // 지금 고른 색 버튼에만 링을 켠다. 어두운 색 위에는 흰 링, 밝은 색 위에는 검은 링을 써서
        // 어떤 색에서도 선택 표시가 보이게 한다
        private void HighlightSelected(Color color)
        {
            for (int i = 0; i < _paletteRings.Length; i++)
            {
                if (_paletteRings[i] == null) continue;
                bool selected = ApproximatelyEqual(Palette[i], color);
                _paletteRings[i].enabled = selected;
                if (selected)
                {
                    float luminance = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
                    _paletteRings[i].color = luminance > 0.6f ? new Color(0.15f, 0.17f, 0.25f) : Color.white;
                }
            }
        }

        private static bool ApproximatelyEqual(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        /// <summary>펜 굵기를 설정하고 지우개 모드를 끈다.</summary>
        public void SetPenSize(float radius)
        {
            SetEraser(false);
            canvas.BrushRadius = radius;
        }

        /// <summary>지우개 모드를 켜거나 끈다. 버튼 배경색은 켜짐(주황)/꺼짐(흰색)으로 상태를 표시한다.</summary>
        public void SetEraser(bool on)
        {
            canvas.IsEraser = on;
            if (_eraserButtonImage != null)
                _eraserButtonImage.color = on ? new Color(1f, 0.8f, 0.4f) : Color.white;
        }

        /// <summary>마지막 스트로크를 취소한다.</summary>
        public void Undo() => canvas.Undo();

        /// <summary>캔버스를 전부 지운다.</summary>
        public void ClearAll() => canvas.ClearAll();

        /// <summary>
        /// 현재 그림을 PNG 2장(선/색)으로 저장한다.
        /// 상태머신 없이 패널 단독 테스트할 때의 폴백 경로 (마일스톤 ② 검증 지점).
        /// </summary>
        public void SaveSketch()
        {
            string sessionId = SessionStore.NewSessionId();
            byte[] linePng = CanvasExporter.ToPng(canvas.LineLayer);
            byte[] colorPng = CanvasExporter.ToPng(canvas.ColorLayer);
            var (linePath, colorPath) = SessionStore.SaveSketchPair(sessionId, linePng, colorPng);
            Debug.Log($"[DrawingPanel] 스케치 저장 완료: {linePath} / {colorPath}");
        }

        [Button("Idle 팝업 표시")]
        /// <summary>방치 팝업을 표시한다 (AppFlowManager가 무입력 90초에 호출).</summary>
        public void ShowIdlePopup()
        {
            if (_idlePopup == null) BuildIdlePopup();
            _idlePopup.SetActive(true);
        }

        /// <summary>방치 팝업을 숨긴다.</summary>
        public void HideIdlePopup()
        {
            if (_idlePopup != null) _idlePopup.SetActive(false);
        }

        // ── 이하 런타임 UI 생성 ──────────────────────────────

        private void BuildPalette()
        {
            // 씬에 배치해 둔 버튼을 계층 순서대로 팔레트 색에 대응시킨다.
            // 버튼 수가 색 수와 달라도 죽지 않게 둘 중 작은 쪽까지만 처리한다 (전시장 무인 운영)
            Button[] buttons = paletteContainer.GetComponentsInChildren<Button>(true);
            if (buttons.Length != Palette.Length)
                LogManager.Warn($"[DrawingPanel] 팔레트 버튼 {buttons.Length}개 / 색 {Palette.Length}개 — 개수가 다름");

            int count = Mathf.Min(buttons.Length, Palette.Length);
            for (int i = 0; i < count; i++)
            {
                Button button = buttons[i];
                Color color = Palette[i];

                // 씬의 기본 Knob 스프라이트는 40×40이라 140px 셀로 늘리면 외곽선이 깨진다 —
                // 런타임 생성한 256px 원형(안티에일리어싱)으로 교체한다 (2026-07-14)
                button.image.sprite = UiBuilder.CircleSprite;
                button.image.type = Image.Type.Simple;
                button.image.color = color;

                // 선택 표시 링 (버튼과 같은 크기로 겹쳐 두고 평소엔 꺼 둔다)
                Image ring = UiBuilder.CreateImage(button.transform, "SelectionRing", Color.white);
                ring.sprite = UiBuilder.RingSprite;
                ring.raycastTarget = false; // 링이 클릭을 가로채면 색 선택이 안 된다
                UiBuilder.Stretch((RectTransform)ring.transform);
                ring.enabled = false;
                _paletteRings[i] = ring;

                Color captured = color; // 클로저에 루프 변수 직접 캡처 방지
                button.onClick.AddListener(() => SetColor(captured));
            }
        }

        private void BuildToolbar()
        {
            // 펜 굵기 (가는/중간/굵은)
            string[] sizeLabels =
            {
                TextLibrary.Get("drawing.tool.penThin"),
                TextLibrary.Get("drawing.tool.penMid"),
                TextLibrary.Get("drawing.tool.penThick"),
            };
            for (int i = 0; i < PenSizes.Length; i++)
            {
                float captured = PenSizes[i];
                UiBuilder.CreateButton(toolbarContainer, sizeLabels[i], Color.white)
                    .onClick.AddListener(() => SetPenSize(captured));
            }

            Button eraser = UiBuilder.CreateButton(toolbarContainer, TextLibrary.Get("drawing.tool.eraser"), Color.white);
            _eraserButtonImage = eraser.GetComponent<Image>();
            eraser.onClick.AddListener(() => SetEraser(!canvas.IsEraser));

            UiBuilder.CreateButton(toolbarContainer, TextLibrary.Get("drawing.tool.undo"), Color.white)
                .onClick.AddListener(Undo);
            UiBuilder.CreateButton(toolbarContainer, TextLibrary.Get("drawing.tool.clear"), Color.white)
                .onClick.AddListener(ClearAll);

            // Button done = UiBuilder.CreateButton(toolbarContainer, TextLibrary.Get("drawing.tool.done"),
            //     new Color(0.35f, 0.75f, 0.45f));
            
            done.onClick.AddListener(OnDoneClicked); // 생성해둔 버튼으로 연결
        }

        private void OnDoneClicked()
        {
            // 빈 캔버스 제출 방지 (아무것도 안 그리고 완성을 누르는 경우)
            if (canvas == null || !canvas.HasStrokes) return;

            // 상태머신이 구독 중이면 흐름에 맡기고, 없으면 기존 단독 저장 동작을 유지한다
            if (CompleteRequested != null) CompleteRequested.Invoke();
            else SaveSketch();
        }

        private void BuildIdlePopup()
        {
            // 반투명 차단막 — 팝업이 떠 있는 동안 캔버스 입력을 막는다 (계획서 4장: 응답을 요구)
            Image overlay = UiBuilder.CreateImage(transform, "IdlePopup", new Color(0f, 0f, 0f, 0.6f));
            UiBuilder.Stretch((RectTransform)overlay.transform);

            Image box = UiBuilder.CreateImage(overlay.transform, "Box", Color.white);
            UiBuilder.Place((RectTransform)box.transform, Vector2.zero, new Vector2(800, 400));

            Text message = UiBuilder.CreateText(box.transform, "Message",
                TextLibrary.Get("drawing.idle.message"), 48, Color.black);
            UiBuilder.Place((RectTransform)message.transform, new Vector2(0, 70), new Vector2(700, 100));

            Button continueButton = UiBuilder.CreateButton(box.transform,
                TextLibrary.Get("drawing.idle.continue"), new Color(0.35f, 0.75f, 0.45f), 36);
            UiBuilder.Place((RectTransform)continueButton.transform, new Vector2(0, -80), new Vector2(420, 100));
            continueButton.onClick.AddListener(() =>
            {
                HideIdlePopup();
                ContinueRequested?.Invoke();
            });

            _idlePopup = overlay.gameObject;
            _idlePopup.SetActive(false);
        }
    }
}
