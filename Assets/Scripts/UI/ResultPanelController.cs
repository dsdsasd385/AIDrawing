using System;
using CarDrawing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.UI
{
    /// <summary>
    /// 결과 화면(ResultPanel). 계획서 4장: 스케치 vs 완성본 비교 + 다시 그리기.
    /// UI 시스템에 속하며 AppFlowManager가 SetImages로 텍스처를 넣고 RetryRequested를 받는다.
    /// QR 코드와 [전시장에 내 작품 걸기]는 마일스톤 ④에서 추가한다.
    /// </summary>
    public class ResultPanelController : MonoBehaviour
    {
        /// <summary>[다시 그리기]를 눌렀을 때 (그림 유지 상태로 그리기 화면 복귀)</summary>
        public event Action RetryRequested;

        private RawImage _sketch;
        private RawImage _result;

        private void Awake()
        {
            UiBuilder.Stretch((RectTransform)transform);

            Image background = UiBuilder.CreateImage(transform, "Background", new Color(0.95f, 0.96f, 0.99f));
            UiBuilder.Stretch((RectTransform)background.transform);

            Text title = UiBuilder.CreateText(background.transform, "Title",
                TextLibrary.Get("result.title"), 64, new Color(0.15f, 0.17f, 0.25f));
            UiBuilder.Place((RectTransform)title.transform, new Vector2(0, 430), new Vector2(1600, 90));

            // 좌: 스케치, 우: AI 완성본 (비교 표시)
            _sketch = CreateLabeledImage(background.transform, "Sketch",
                TextLibrary.Get("result.sketchLabel"), new Vector2(-390, 60));
            _result = CreateLabeledImage(background.transform, "Result",
                TextLibrary.Get("result.resultLabel"), new Vector2(390, 60));

            Button retry = UiBuilder.CreateButton(background.transform,
                TextLibrary.Get("result.retry"), new Color(0.35f, 0.75f, 0.45f), 40);
            UiBuilder.Place((RectTransform)retry.transform, new Vector2(0, -400), new Vector2(400, 100));
            retry.onClick.AddListener(() => RetryRequested?.Invoke());
        }

        /// <summary>
        /// 비교 이미지 2장을 설정한다 (생성 성공 시 AppFlowManager가 호출).
        /// </summary>
        /// <param name="sketch">관람객이 그린 그림 (색 레이어)</param>
        /// <param name="result">AI 생성 결과</param>
        public void SetImages(Texture sketch, Texture result)
        {
            if (_sketch != null) _sketch.texture = sketch;
            if (_result != null) _result.texture = result;
        }

        // 흰 테두리 + 이미지 + 하단 라벨 한 벌을 만든다
        private static RawImage CreateLabeledImage(Transform parent, string name, string label, Vector2 center)
        {
            Image frame = UiBuilder.CreateImage(parent, name + "Frame", Color.white);
            UiBuilder.Place((RectTransform)frame.transform, center, new Vector2(716, 484));

            RawImage image = UiBuilder.CreateRawImage(frame.transform, name);
            UiBuilder.Place((RectTransform)image.transform, Vector2.zero, new Vector2(700, 468));

            Text caption = UiBuilder.CreateText(parent, name + "Label", label, 40, new Color(0.25f, 0.28f, 0.35f));
            UiBuilder.Place((RectTransform)caption.transform, center + new Vector2(0, -290), new Vector2(700, 60));

            return image;
        }
    }
}
