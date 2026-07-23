using CarDrawing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.UI
{
    /// <summary>
    /// 생성 중 화면(GeneratingPanel). 계획서 4장: 4~8초 동안 진행 연출을 보여준다.
    /// UI 시스템에 속하며 AppFlowManager가 Begin/ShowError를 호출한다.
    /// 실패 시 사과 문구를 표시하고, 복귀 타이밍은 AppFlowManager가 관리한다.
    /// </summary>
    public class GeneratingPanelController : MonoBehaviour
    {
        /// <summary>생성을 기다리지 않고 [다시 그리기]로 돌아갈 때 (2026-07-14 추가).
        /// 요청 자체는 서버에서 계속 돌지만 늦게 온 결과는 AppFlowManager의 상태 가드가 버린다</summary>
        public event System.Action BackRequested;

        // 진행 문구 (말줄임표 애니메이션 대상)
        private Text _message;
        private RawImage _sketch;
        private Button _backButton;
        // 오류 표시 중에는 말줄임표 애니메이션을 멈춘다
        private bool _errorShown;

        private void Awake()
        {
            UiBuilder.Stretch((RectTransform)transform);

            Image background = UiBuilder.CreateImage(transform, "Background", new Color(0.12f, 0.12f, 0.16f));
            UiBuilder.Stretch((RectTransform)background.transform);

            // 스케치를 보여주며 "이 그림이 변하는 중"이라는 느낌을 준다.
            // 그리기 캔버스와 같은 1152×768(3:2)로 화면 정중앙에 배치 — "내가 그린 그림 그대로가 변한다"는 연속감
            Image frame = UiBuilder.CreateImage(background.transform, "SketchFrame", Color.white);
            UiBuilder.Place((RectTransform)frame.transform, Vector2.zero, new Vector2(1168, 784));

            _sketch = UiBuilder.CreateRawImage(frame.transform, "Sketch");
            UiBuilder.Place((RectTransform)_sketch.transform, Vector2.zero, new Vector2(1152, 768));

            // 스케치(±384)가 화면 중앙을 채우므로 문구는 그 아래 여백에 둔다 (겹침 방지)
            _message = UiBuilder.CreateText(background.transform, "Message",
                TextLibrary.Get("generating.message"), 48, Color.white);
            UiBuilder.Place((RectTransform)_message.transform, new Vector2(0, -455), new Vector2(1600, 120));

            // 생성이 늦어지거나 스타일을 잘못 골랐을 때 빠져나갈 길 (스타일 화면과 같은 우측 상단 자리).
            // 실패 사과 중에는 숨긴다
            _backButton = UiBuilder.CreateButton(background.transform,
                TextLibrary.Get("generating.back"), new Color(0.45f, 0.48f, 0.55f), 30);
            UiBuilder.Place((RectTransform)_backButton.transform, new Vector2(790, 460), new Vector2(300, 88));
            _backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        /// <summary>
        /// 생성 연출을 시작한다 (생성 요청 직후 AppFlowManager가 호출).
        /// </summary>
        /// <param name="sketch">관람객이 그린 그림 (색 레이어)</param>
        public void Begin(Texture sketch)
        {
            _errorShown = false;
            if (_sketch != null) _sketch.texture = sketch;
            if (_backButton != null) _backButton.gameObject.SetActive(true);
        }

        /// <summary>생성 실패 시 사과 문구를 표시한다 (계획서 12장: 해당 세션만 사과 후 초기화).</summary>
        public void ShowError(string message)
        {
            _errorShown = true;
            if (_message != null) _message.text = message;
            // 실패 후에는 AppFlowManager가 잠시 뒤 대기 화면으로 되돌린다 — 버튼을 눌러도 의미가 없다
            if (_backButton != null) _backButton.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_errorShown || _message == null) return;
            // 말줄임표 0~3개 반복으로 진행 중임을 표현
            int dots = (int)(Time.unscaledTime * 2f) % 4;
            _message.text = TextLibrary.Get("generating.message") + new string('.', dots);
        }
    }
}
