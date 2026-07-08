using UnityEngine;
using UnityEngine.UI;

namespace EarthCoding.Blocks
{
    /// <summary>
    /// 드래그 중 손가락/마우스를 따라다니는 반투명 블록 잔상.
    /// 팔레트에서 꺼낼 때와 조립 영역에서 옮길 때 공통으로 사용한다.
    /// 화면 최상단 캔버스에 하나만 존재하며 드래그가 끝나면 숨겨진다.
    /// </summary>
    public static class DragGhost
    {
        /// <summary>잔상 오브젝트 (재사용)</summary>
        private static GameObject _ghost;

        /// <summary>잔상 배경 이미지</summary>
        private static Image _image;

        /// <summary>잔상 텍스트</summary>
        private static Text _label;

        /// <summary>
        /// 드래그 잔상을 표시한다.
        /// </summary>
        /// <param name="canvas">최상단 캔버스 (잔상이 모든 UI 위에 보이도록)</param>
        /// <param name="blockName">블록 이름</param>
        /// <param name="color">블록 색상</param>
        public static void Show(Canvas canvas, string blockName, Color color)
        {
            if (_ghost == null)
            {
                // 최초 1회만 생성하고 이후에는 재사용한다
                _ghost = new GameObject("DragGhost", typeof(RectTransform));
                _image = _ghost.AddComponent<Image>();
                _image.raycastTarget = false;   // 잔상이 드롭 판정을 가로막지 않도록

                var rt = (RectTransform)_ghost.transform;
                rt.sizeDelta = new Vector2(240, 56);

                _label = UI.UIFactory.Label(_ghost.transform, "Label", "", 22);
                _label.raycastTarget = false;
            }

            _ghost.transform.SetParent(canvas.transform, false);
            _ghost.transform.SetAsLastSibling();   // 항상 맨 위에 그리기

            // 반투명하게 표시하여 '옮기는 중' 임을 표현
            _image.color = new Color(color.r, color.g, color.b, 0.75f);
            _label.text = blockName;
            _ghost.SetActive(true);
        }

        /// <summary>
        /// 잔상을 포인터 위치로 이동시킨다. 드래그 중 매 프레임 호출된다.
        /// </summary>
        /// <param name="screenPosition">현재 포인터 화면 좌표</param>
        /// <param name="canvas">기준 캔버스</param>
        public static void Move(Vector2 screenPosition, Canvas canvas)
        {
            if (_ghost == null || !_ghost.activeSelf) return;

            // 스크린 좌표를 캔버스 로컬 좌표로 변환 (Screen Space - Overlay 기준)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)canvas.transform, screenPosition, canvas.worldCamera, out var localPos);
            ((RectTransform)_ghost.transform).anchoredPosition = localPos;
        }

        /// <summary>드래그 종료 시 잔상을 숨긴다.</summary>
        public static void Hide()
        {
            if (_ghost != null) _ghost.SetActive(false);
        }
    }
}
