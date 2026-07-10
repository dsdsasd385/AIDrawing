using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CarDrawing.Drawing
{
    /// <summary>
    /// 캔버스를 표시하는 RawImage 위의 마우스 입력을 UV 좌표로 변환해
    /// DrawingCanvas에 전달한다. 드로잉 시스템에 속하며 DrawingPanel(UI)의 캔버스 영역에 붙인다.
    /// RawImage.texture에는 DrawingCanvas.ColorLayer를 연결한다 (관람객이 보는 화면).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class CanvasMouseInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>입력을 전달할 캔버스</summary>
        [SerializeField] private DrawingCanvas canvas;

        private RectTransform _rect;

        private void Awake()
        {
            _rect = (RectTransform)transform;

            // 인스펙터 연결 누락 시에도 동작하도록 씬에서 탐색 (전시장 운영 중 강제 종료 방지 원칙)
            if (canvas == null)
                canvas = FindObjectOfType<DrawingCanvas>();
        }

        private void Start()
        {
            // 관람객에게는 색 레이어를 보여준다
            var image = GetComponent<RawImage>();
            if (image.texture == null && canvas != null)
                image.texture = canvas.ColorLayer;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (TryGetUv(eventData, out Vector2 uv))
                canvas.BeginStroke(uv);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (TryGetUv(eventData, out Vector2 uv))
                canvas.ContinueStroke(uv);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            canvas.EndStroke();
        }

        // 스크린 좌표 → RawImage 로컬 좌표 → UV(0~1, 좌하단 원점)
        private bool TryGetUv(PointerEventData eventData, out Vector2 uv)
        {
            uv = default;
            if (canvas == null) return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return false;

            Rect r = _rect.rect;
            uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            return true;
        }
    }
}
