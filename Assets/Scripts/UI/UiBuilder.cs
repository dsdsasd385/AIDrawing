using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.UI
{
    /// <summary>
    /// 런타임 uGUI 요소 생성 헬퍼. UI 시스템에 속하며 패널 컨트롤러들이 공용으로 쓴다.
    /// 디자인 리소스 적용 전까지 씬 수동 배치를 최소화하는 방침 (DrawingPanelController와 동일).
    /// </summary>
    public static class UiBuilder
    {
        /// <summary>한글 지원 기본 폰트. TMP 미사용 방침 (인수인계 §2)</summary>
        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>부모 전체를 덮도록 앵커를 스트레치한다.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>중앙 앵커 기준으로 위치와 크기를 지정한다 (1920×1080 기준 좌표).</summary>
        public static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        /// <summary>단색 Image를 생성한다 (배경·상자용).</summary>
        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>RawImage를 생성한다 (RenderTexture/Texture2D 표시용).</summary>
        public static RawImage CreateRawImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RawImage>();
        }

        /// <summary>가운데 정렬 Text를 생성한다.</summary>
        public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            // 문구가 상자보다 길어도 잘리지 않게 (문구는 JSON에서 바뀔 수 있다)
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// 배경색 + 라벨 텍스트를 가진 버튼을 생성한다.
        /// 레이아웃 그룹 아래에 두거나, 아니라면 Place()로 위치를 지정한다.
        /// </summary>
        public static Button CreateButton(Transform parent, string label, Color background, int fontSize = 28)
        {
            var go = new GameObject(string.IsNullOrEmpty(label) ? "Swatch" : label,
                typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = background;

            if (!string.IsNullOrEmpty(label))
            {
                Text text = CreateText(go.transform, "Text", label, fontSize, Color.black);
                Stretch((RectTransform)text.transform);
            }
            return go.GetComponent<Button>();
        }
    }
}
