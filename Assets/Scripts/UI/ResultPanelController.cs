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

        // 표시할 텍스처를 캐시한다. SetImages는 이 패널이 활성화(=Awake로 RawImage 생성)되기 전에
        // 호출될 수 있어(AppFlowManager가 EnterState보다 SetImages를 먼저 호출), 그 경우 참조가 아직 null이라
        // 지정이 무시된다. 값을 캐시해 두고 SetImages·Awake 어느 쪽이 먼저 실행돼도 최종적으로 반영되게 한다.
        private Texture _pendingSketch;
        private Texture _pendingResult;

        private void Awake()
        {
            // 씬에 이전 런타임 UI가 저장돼 있으면 Awake가 또 만들어 중복이 생기고,
            // 텍스처가 붙지 않은 쪽이 위에 보여 이미지가 비어 보인다. 생성 전에 기존 자식을 비운다.
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

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

            // SetImages가 Awake보다 먼저 호출됐다면 캐시해 둔 텍스처를 지금 반영한다
            ApplyTextures();
        }

        /// <summary>
        /// 비교 이미지 2장을 설정한다 (생성 성공 시 AppFlowManager가 호출).
        /// </summary>
        /// <param name="sketch">관람객이 그린 그림 (색 레이어)</param>
        /// <param name="result">AI 생성 결과</param>
        public void SetImages(Texture sketch, Texture result)
        {
            _pendingSketch = sketch;
            _pendingResult = result;
            ApplyTextures();
        }

        // 캐시된 텍스처를 RawImage에 반영한다. RawImage가 아직 없으면(Awake 전) 캐시만 남기고,
        // Awake가 UI를 만든 뒤 다시 호출돼 반영된다.
        private void ApplyTextures()
        {
            if (_sketch != null) _sketch.texture = _pendingSketch;
            if (_result != null) _result.texture = _pendingResult;
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
