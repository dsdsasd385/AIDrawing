using System;
using CarDrawing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.UI
{
    /// <summary>
    /// 대기 화면(AttractPanel). 계획서 4장: 시작 안내 + 화면 클릭으로 체험 시작.
    /// UI 시스템에 속하며 AppFlowManager가 StartRequested 이벤트를 받아 그리기 화면으로 전환한다.
    /// 미니 슬라이드쇼는 갤러리 구현(마일스톤 ④) 때 추가한다.
    /// </summary>
    public class AttractPanelController : MonoBehaviour
    {
        /// <summary>관람객이 화면을 클릭해 시작을 요청했을 때</summary>
        public event Action StartRequested;

        // 깜빡임 연출 대상 (시선 유도)
        private Text _startHint;

        private void Awake()
        {
            UiBuilder.Stretch((RectTransform)transform);

            Image background = UiBuilder.CreateImage(transform, "Background", new Color(0.10f, 0.12f, 0.20f));
            UiBuilder.Stretch((RectTransform)background.transform);

            // 화면 전체가 시작 버튼 역할을 한다 (계획서 4장: 클릭으로 시작)
            var startButton = background.gameObject.AddComponent<Button>();
            startButton.transition = Selectable.Transition.None;
            startButton.onClick.AddListener(() => StartRequested?.Invoke());

            Text title = UiBuilder.CreateText(background.transform, "Title",
                TextLibrary.Get("attract.title"), 96, Color.white);
            UiBuilder.Place((RectTransform)title.transform, new Vector2(0, 170), new Vector2(1600, 140));

            Text subtitle = UiBuilder.CreateText(background.transform, "Subtitle",
                TextLibrary.Get("attract.subtitle"), 44, new Color(0.75f, 0.80f, 0.90f));
            UiBuilder.Place((RectTransform)subtitle.transform, new Vector2(0, 40), new Vector2(1600, 70));

            _startHint = UiBuilder.CreateText(background.transform, "StartHint",
                TextLibrary.Get("attract.start"), 48, new Color(1f, 0.85f, 0.30f));
            UiBuilder.Place((RectTransform)_startHint.transform, new Vector2(0, -300), new Vector2(1200, 80));
        }

        private void Update()
        {
            // 시작 안내 문구 깜빡임 (전시장에서 시작 방법을 알아채도록)
            if (_startHint == null) return;
            Color c = _startHint.color;
            c.a = 0.45f + 0.55f * Mathf.PingPong(Time.unscaledTime, 1f);
            _startHint.color = c;
        }
    }
}
