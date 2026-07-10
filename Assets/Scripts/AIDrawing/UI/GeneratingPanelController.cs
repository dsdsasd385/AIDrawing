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
        // 진행 문구 (말줄임표 애니메이션 대상)
        private Text _message;
        private RawImage _sketch;
        // 오류 표시 중에는 말줄임표 애니메이션을 멈춘다
        private bool _errorShown;

        private void Awake()
        {
            UiBuilder.Stretch((RectTransform)transform);

            Image background = UiBuilder.CreateImage(transform, "Background", new Color(0.12f, 0.12f, 0.16f));
            UiBuilder.Stretch((RectTransform)background.transform);

            // 스케치를 보여주며 "이 그림이 변하는 중"이라는 느낌을 준다
            Image frame = UiBuilder.CreateImage(background.transform, "SketchFrame", Color.white);
            UiBuilder.Place((RectTransform)frame.transform, new Vector2(0, 80), new Vector2(592, 400));

            _sketch = UiBuilder.CreateRawImage(frame.transform, "Sketch");
            UiBuilder.Place((RectTransform)_sketch.transform, Vector2.zero, new Vector2(576, 384));

            _message = UiBuilder.CreateText(background.transform, "Message",
                TextLibrary.Get("generating.message"), 48, Color.white);
            UiBuilder.Place((RectTransform)_message.transform, new Vector2(0, -280), new Vector2(1600, 160));
        }

        /// <summary>
        /// 생성 연출을 시작한다 (생성 요청 직후 AppFlowManager가 호출).
        /// </summary>
        /// <param name="sketch">관람객이 그린 그림 (색 레이어)</param>
        public void Begin(Texture sketch)
        {
            _errorShown = false;
            if (_sketch != null) _sketch.texture = sketch;
        }

        /// <summary>생성 실패 시 사과 문구를 표시한다 (계획서 12장: 해당 세션만 사과 후 초기화).</summary>
        public void ShowError(string message)
        {
            _errorShown = true;
            if (_message != null) _message.text = message;
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
