using System.Collections.Generic;
using CarDrawing.Drawing;
using CarDrawing.Results;
using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.UI
{
    /// <summary>
    /// 그리기 화면(DrawingPanel)의 도구 UI를 관리한다.
    /// UI 시스템에 속하며 DrawingCanvas를 조작한다.
    /// 색상 팔레트·펜 굵기·지우개·undo·전체 지우기 버튼은 런타임에 생성한다
    /// (디자인 리소스 적용 전까지 씬 수동 배치를 최소화하기 위함).
    /// </summary>
    public class DrawingPanelController : MonoBehaviour
    {
        /// <summary>조작 대상 캔버스</summary>
        [SerializeField] private DrawingCanvas canvas;
        /// <summary>색상 팔레트 버튼이 생성될 컨테이너 (세로 배치)</summary>
        [SerializeField] private RectTransform paletteContainer;
        /// <summary>도구 버튼(굵기/지우개/undo/지우기/완성)이 생성될 컨테이너 (가로 배치)</summary>
        [SerializeField] private RectTransform toolbarContainer;

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

        private void Start()
        {
            if (canvas == null)
                canvas = FindObjectOfType<DrawingCanvas>();

            BuildPalette();
            BuildToolbar();
        }

        /// <summary>브러시 색을 바꾸고 지우개 모드를 해제한다 (색을 고르면 그리기 의도로 간주).</summary>
        public void SetColor(Color color)
        {
            canvas.BrushColor = color;
            SetEraser(false);
        }

        /// <summary>펜 굵기를 설정한다.</summary>
        public void SetPenSize(float radius)
        {
            canvas.BrushRadius = radius;
        }

        /// <summary>지우개 모드를 켜거나 끈다.</summary>
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
        /// 마일스톤 ②의 검증 지점이며, 이후 AppFlowManager가 생성 요청으로 대체 호출한다.
        /// </summary>
        public void SaveSketch()
        {
            string sessionId = SessionStore.NewSessionId();
            byte[] linePng = CanvasExporter.ToPng(canvas.LineLayer);
            byte[] colorPng = CanvasExporter.ToPng(canvas.ColorLayer);
            var (linePath, colorPath) = SessionStore.SaveSketchPair(sessionId, linePng, colorPng);
            Debug.Log($"[DrawingPanel] 스케치 저장 완료: {linePath} / {colorPath}");
        }

        // ── 이하 런타임 UI 생성 ──────────────────────────────

        private void BuildPalette()
        {
            foreach (Color color in Palette)
            {
                Button button = CreateButton(paletteContainer, "", color);
                Color captured = color; // 클로저에 루프 변수 직접 캡처 방지
                button.onClick.AddListener(() => SetColor(captured));
            }
        }

        private void BuildToolbar()
        {
            // 펜 굵기 (가는/중간/굵은)
            string[] sizeLabels = { "가는 펜", "중간 펜", "굵은 펜" };
            for (int i = 0; i < PenSizes.Length; i++)
            {
                float captured = PenSizes[i];
                CreateButton(toolbarContainer, sizeLabels[i], Color.white)
                    .onClick.AddListener(() => SetPenSize(captured));
            }

            Button eraser = CreateButton(toolbarContainer, "지우개", Color.white);
            _eraserButtonImage = eraser.GetComponent<Image>();
            eraser.onClick.AddListener(() => SetEraser(!canvas.IsEraser));

            CreateButton(toolbarContainer, "되돌리기", Color.white).onClick.AddListener(Undo);
            CreateButton(toolbarContainer, "전체 지우기", Color.white).onClick.AddListener(ClearAll);

            Button done = CreateButton(toolbarContainer, "완성!", new Color(0.35f, 0.75f, 0.45f));
            done.onClick.AddListener(SaveSketch);
        }

        private static Button CreateButton(RectTransform parent, string label, Color background)
        {
            var go = new GameObject(string.IsNullOrEmpty(label) ? "Swatch" : label,
                typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = background;

            if (!string.IsNullOrEmpty(label))
            {
                var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(go.transform, false);
                var text = textGo.GetComponent<Text>();
                text.text = label;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 28;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.black;
                var textRect = (RectTransform)textGo.transform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }

            return go.GetComponent<Button>();
        }
    }
}
