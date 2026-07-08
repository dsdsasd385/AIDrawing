using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using EarthCoding.Core;

namespace EarthCoding.UI
{
    /// <summary>
    /// 스토리 화면 (인트로/클로징) 담당. (작업계획서 3장 체험 플로우 대응)
    /// 인트로 애니메이션 → 스토리 문구 → 시작 버튼 / 클로징 애니메이션 → 종료 문구 흐름을
    /// 전체 화면 오버레이로 표시한다. 문구는 Story.json 에서 읽는다.
    /// 아트 리소스 적용 전에는 지구 원 + 문구 페이드로 연출을 대신한다.
    /// GameManager 가 생성하고 호출한다.
    /// </summary>
    public class StoryScreen : MonoBehaviour
    {
        /// <summary>스토리 화면 루트 (표시/숨김 전환)</summary>
        private GameObject _root;

        /// <summary>제목 텍스트</summary>
        private Text _title;

        /// <summary>본문 문구 텍스트</summary>
        private Text _body;

        /// <summary>시작/종료 버튼</summary>
        private Button _button;

        /// <summary>버튼 라벨 (인트로: 시작하기 / 클로징: 처음으로)</summary>
        private Text _buttonLabel;

        /// <summary>연출용 지구 이미지 (원형 대신 단색 사각 + 둥근 느낌의 스케일 연출)</summary>
        private Image _earth;

        /// <summary>버튼을 눌렀을 때 실행할 동작 (표시 시점에 결정)</summary>
        private Action _onButton;

        /// <summary>
        /// 스토리 화면 UI 를 생성한다. GameManager 초기화 시 1회 호출.
        /// </summary>
        /// <param name="canvas">루트 캔버스</param>
        public void Build(Canvas canvas)
        {
            // 전체 화면 덮개 (에피소드 UI 위에 그려짐)
            var bg = UIFactory.Panel(canvas.transform, "StoryScreen", Vector2.zero, Vector2.one, UIFactory.BgDark);
            _root = bg.gameObject;

            // 연출용 지구 (가운데 큰 사각형 - 리소스 적용 시 지구 일러스트로 교체)
            _earth = UIFactory.Panel(_root.transform, "Earth",
                new Vector2(0.42f, 0.45f), new Vector2(0.58f, 0.73f), new Color(0.25f, 0.55f, 0.90f));
            _earth.raycastTarget = false;

            // 제목
            _title = UIFactory.Label(_root.transform, "Title", "", 52);
            var titleRt = (RectTransform)_title.transform;
            titleRt.anchorMin = new Vector2(0, 0.76f);
            titleRt.anchorMax = new Vector2(1, 0.92f);
            _title.fontStyle = FontStyle.Bold;

            // 본문 문구
            _body = UIFactory.Label(_root.transform, "Body", "", 28);
            var bodyRt = (RectTransform)_body.transform;
            bodyRt.anchorMin = new Vector2(0.1f, 0.22f);
            bodyRt.anchorMax = new Vector2(0.9f, 0.42f);

            // 진행 버튼
            var btnHolder = UIFactory.Rect(_root.transform, "BtnHolder",
                new Vector2(0.4f, 0.08f), new Vector2(0.6f, 0.17f));
            _button = UIFactory.TextButton(btnHolder, "Button", "", UIFactory.AccentColor, 28,
                () => _onButton?.Invoke());
            _buttonLabel = _button.GetComponentInChildren<Text>();

            _root.SetActive(false);
        }

        /// <summary>
        /// 인트로 화면을 표시한다. (인트로 애니메이션 + 시작 문구)
        /// </summary>
        /// <param name="onStart">시작 버튼을 눌렀을 때 실행할 동작</param>
        public void ShowIntro(Action onStart)
        {
            Show(DataManager.Story.IntroTitle, DataManager.Story.IntroText, "시작하기", onStart);
        }

        /// <summary>
        /// 클로징 화면을 표시한다. (클로징 애니메이션 + 종료 문구 + 총점)
        /// </summary>
        /// <param name="onFinish">처음으로 버튼을 눌렀을 때 실행할 동작</param>
        public void ShowOutro(Action onFinish)
        {
            var body = DataManager.Story.OutroText + $"\n\n총 점수: {ScoreManager.TotalScore}점";
            Show(DataManager.Story.OutroTitle, body, "처음으로", onFinish);
        }

        /// <summary>스토리 화면을 숨긴다.</summary>
        public void Hide()
        {
            StopAllCoroutines();
            _root.SetActive(false);
        }

        /// <summary>
        /// 스토리 화면을 구성하고 등장 연출을 재생한다.
        /// </summary>
        /// <param name="title">제목</param>
        /// <param name="body">본문 문구</param>
        /// <param name="buttonLabel">버튼 문구</param>
        /// <param name="onButton">버튼 동작</param>
        private void Show(string title, string body, string buttonLabel, Action onButton)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();   // 에피소드 UI 위에 표시
            _title.text = title;
            _body.text = body;
            _buttonLabel.text = buttonLabel;
            _onButton = onButton;

            // 간단한 등장 애니메이션 (지구가 커지며 문구 페이드 인)
            if (DataManager.Config.UseAnimation)
                StartCoroutine(PlayIntroAnimation());
        }

        /// <summary>지구 스케일 업 + 문구 페이드 인 연출 코루틴</summary>
        private IEnumerator PlayIntroAnimation()
        {
            const float duration = 1.2f;
            float t = 0f;

            var earthRt = _earth.rectTransform;
            var titleColor = _title.color;
            var bodyColor = _body.color;

            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);

                // 지구: 작았다가 원래 크기로 (탄성 없는 부드러운 확대)
                earthRt.localScale = Vector3.one * Mathf.SmoothStep(0.3f, 1f, p);
                // 문구: 서서히 나타남
                _title.color = new Color(titleColor.r, titleColor.g, titleColor.b, p);
                _body.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, p);
                yield return null;
            }

            // 연출 종료 후 완전 표시 상태 보장
            earthRt.localScale = Vector3.one;
            _title.color = new Color(titleColor.r, titleColor.g, titleColor.b, 1f);
            _body.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, 1f);
        }
    }
}
