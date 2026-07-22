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

        // 팔레트용 원형·링 스프라이트 캐시. 앱 실행당 한 번만 만든다
        private static Sprite _circleSprite;
        private static Sprite _ringSprite;

        /// <summary>
        /// 매끄러운 원형 스프라이트 (팔레트 색 버튼용).
        /// 씬의 기본 Knob(40×40)을 140px 셀에 3.5배 늘려 쓰면 외곽선이 계단처럼 깨진다 —
        /// 안티에일리어싱을 넣어 큰 텍스처로 직접 만든다 (2026-07-14 실측 대응).
        /// </summary>
        public static Sprite CircleSprite => _circleSprite != null
            ? _circleSprite
            : _circleSprite = CreateRadialSprite(256, 0f);

        /// <summary>선택 표시용 링(도넛) 스프라이트. 현재 고른 색 버튼 위에 겹쳐 보여 준다</summary>
        public static Sprite RingSprite => _ringSprite != null
            ? _ringSprite
            : _ringSprite = CreateRadialSprite(256, 0.80f);

        // innerRatio = 0이면 꽉 찬 원, >0이면 그 비율 안쪽이 비는 링.
        // 가장자리 1px 구간을 알파로 보간해 계단을 없앤다 (텍스처가 커도 확대 시 부드럽게 남는다)
        private static Sprite CreateRadialSprite(int size, float innerRatio)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float outer = center;          // 바깥 반지름
            float inner = outer * innerRatio;
            const float edge = 1.5f;       // 알파가 0→1로 넘어가는 폭(px)

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float a = Mathf.Clamp01((outer - d) / edge);                       // 바깥 경계
                    if (inner > 0f) a = Mathf.Min(a, Mathf.Clamp01((d - inner) / edge)); // 안쪽 경계(링)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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
