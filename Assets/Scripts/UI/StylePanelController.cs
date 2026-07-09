using System;
using CarDrawing.Core;
using CarDrawing.Generation;
using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.UI
{
    /// <summary>
    /// 스타일 선택 화면(StylePanel). 계획서 4·8장: 그림 미리보기를 유지한 채 스타일 프리셋을 고른다.
    /// UI 시스템에 속하며, 버튼 목록은 StyleLibrary(Styles.json)에서 만들고
    /// 선택 즉시 AppFlowManager가 StyleChosen을 받아 생성을 시작한다.
    /// </summary>
    public class StylePanelController : MonoBehaviour
    {
        /// <summary>관람객이 스타일을 선택했을 때 (선택 즉시 생성 시작)</summary>
        public event Action<StylePreset> StyleChosen;

        // 관람객이 그린 그림 미리보기
        private RawImage _preview;

        private void Awake()
        {
            UiBuilder.Stretch((RectTransform)transform);

            Image background = UiBuilder.CreateImage(transform, "Background", new Color(0.93f, 0.94f, 0.97f));
            UiBuilder.Stretch((RectTransform)background.transform);

            Text title = UiBuilder.CreateText(background.transform, "Title",
                TextLibrary.Get("style.title"), 64, new Color(0.15f, 0.17f, 0.25f));
            UiBuilder.Place((RectTransform)title.transform, new Vector2(0, 420), new Vector2(1600, 90));

            // 미리보기 흰 테두리 (스케치가 배경과 섞여 보이지 않도록)
            Image frame = UiBuilder.CreateImage(background.transform, "PreviewFrame", Color.white);
            UiBuilder.Place((RectTransform)frame.transform, new Vector2(0, 60), new Vector2(784, 528));

            _preview = UiBuilder.CreateRawImage(frame.transform, "Preview");
            UiBuilder.Place((RectTransform)_preview.transform, Vector2.zero, new Vector2(768, 512));

            BuildStyleButtons(background.transform);
        }

        /// <summary>
        /// 미리보기에 표시할 스케치를 설정한다 (그리기 완료 시 AppFlowManager가 호출).
        /// </summary>
        /// <param name="sketch">색 레이어 텍스처</param>
        public void SetPreview(Texture sketch)
        {
            if (_preview != null) _preview.texture = sketch;
        }

        private void BuildStyleButtons(Transform parent)
        {
            var styles = StyleLibrary.Styles;

            // 가로 일렬 중앙 정렬 (v1은 1개, v2에서 3~4개로 확장 — 계획서 8장)
            const float buttonWidth = 320f, buttonHeight = 110f, spacing = 40f;
            float totalWidth = styles.Count * buttonWidth + (styles.Count - 1) * spacing;

            for (int i = 0; i < styles.Count; i++)
            {
                StylePreset style = styles[i];
                Button button = UiBuilder.CreateButton(parent, style.name, new Color(0.30f, 0.55f, 0.90f), 40);
                float x = -totalWidth / 2f + buttonWidth / 2f + i * (buttonWidth + spacing);
                UiBuilder.Place((RectTransform)button.transform, new Vector2(x, -380), new Vector2(buttonWidth, buttonHeight));
                button.onClick.AddListener(() => StyleChosen?.Invoke(style));
            }
        }
    }
}
