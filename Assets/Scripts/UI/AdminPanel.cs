using UnityEngine;
using UnityEngine.UI;
using EarthCoding.Core;
using EarthCoding.Episodes;

namespace EarthCoding.UI
{
    /// <summary>
    /// 관리자 모드 화면. (작업계획서 9장, 22장 대응)
    /// 진입 방법: F12 키 또는 화면 왼쪽 아래 구석(100x100)을 3초 안에 5번 터치(터치 모니터용).
    /// 비밀번호(Config.json AdminPassword) 입력 후 다음 기능을 제공한다:
    /// 에피소드 선택 / JSON 다시 읽기 / 점수 확인 / 로그 확인 /
    /// 사운드·애니메이션 테스트 / 전체 점수 초기화 / 전체 초기화 / 버전 확인.
    /// GameManager 가 생성하고, 화면은 열 때마다 코드로 새로 구성한다.
    /// </summary>
    public class AdminPanel : MonoBehaviour
    {
        /// <summary>루트 캔버스 (오버레이 부착 대상)</summary>
        private Canvas _canvas;

        /// <summary>현재 떠 있는 관리자 오버레이 (비밀번호 화면 또는 메뉴)</summary>
        private GameObject _overlay;

        /// <summary>비밀번호 입력 중인 문자열</summary>
        private string _passwordInput = "";

        /// <summary>비밀번호 표시 텍스트 (● 마스킹)</summary>
        private Text _passwordDisplay;

        /// <summary>구석 연속 터치 횟수 (숨김 진입 제스처)</summary>
        private int _cornerTapCount;

        /// <summary>첫 구석 터치 시각 (제한 시간 판정)</summary>
        private float _firstTapTime;

        /// <summary>구석 터치 인식 영역 크기 (화면 픽셀)</summary>
        private const float CornerSize = 100f;

        /// <summary>연속 터치 제한 시간(초)</summary>
        private const float TapWindow = 3f;

        /// <summary>진입에 필요한 연속 터치 횟수</summary>
        private const int TapsRequired = 5;

        /// <summary>
        /// 초기화. GameManager 가 캔버스 생성 후 호출한다.
        /// </summary>
        /// <param name="canvas">루트 캔버스</param>
        public void Initialize(Canvas canvas)
        {
            _canvas = canvas;
        }

        /// <summary>
        /// 매 프레임: 숨김 진입 입력(F12 / 구석 5회 터치)을 감지한다.
        /// </summary>
        private void Update()
        {
            if (_overlay != null) return;   // 이미 열려 있으면 감지 중단

            // 개발/키보드 환경용: F12 로 바로 진입
            if (Input.GetKeyDown(KeyCode.F12))
            {
                ShowPasswordScreen();
                return;
            }

            // 터치 모니터용: 왼쪽 아래 구석(100x100)을 3초 안에 5번 터치
            if (Input.GetMouseButtonDown(0))
            {
                var pos = (Vector2)Input.mousePosition;
                bool inCorner = pos.x < CornerSize && pos.y < CornerSize;
                if (!inCorner) { _cornerTapCount = 0; return; }

                // 제한 시간이 지났으면 카운트를 다시 시작한다
                if (_cornerTapCount == 0 || Time.unscaledTime - _firstTapTime > TapWindow)
                {
                    _cornerTapCount = 0;
                    _firstTapTime = Time.unscaledTime;
                }

                _cornerTapCount++;
                if (_cornerTapCount >= TapsRequired)
                {
                    _cornerTapCount = 0;
                    ShowPasswordScreen();
                }
            }
        }

        // ==================== 비밀번호 화면 ====================

        /// <summary>비밀번호 입력 화면(숫자 키패드)을 띄운다.</summary>
        private void ShowPasswordScreen()
        {
            CloseOverlay();
            _passwordInput = "";

            var dim = UIFactory.Panel(_canvas.transform, "AdminPassword",
                Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.85f));
            _overlay = dim.gameObject;
            _overlay.transform.SetAsLastSibling();

            var box = UIFactory.Panel(dim.transform, "Box",
                new Vector2(0.35f, 0.15f), new Vector2(0.65f, 0.85f), UIFactory.PanelColor);

            var title = UIFactory.Label(box.transform, "Title", "관리자 모드", 28);
            SetAnchors(title, 0.05f, 0.88f, 0.95f, 0.98f);
            title.fontStyle = FontStyle.Bold;

            // 입력된 비밀번호 마스킹 표시
            var displayBg = UIFactory.Panel(box.transform, "DisplayBg",
                new Vector2(0.15f, 0.76f), new Vector2(0.85f, 0.86f), new Color(0, 0, 0, 0.4f));
            _passwordDisplay = UIFactory.Label(displayBg.transform, "Display", "", 30);

            // 숫자 키패드 (1~9, 지우기, 0, 확인)
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "←", "0", "확인" };
            for (int i = 0; i < keys.Length; i++)
            {
                int row = i / 3;     // 0~3행
                int col = i % 3;     // 0~2열
                string key = keys[i];

                float xMin = 0.15f + col * 0.245f;
                float yMax = 0.72f - row * 0.145f;
                var holder = UIFactory.Rect(box.transform, $"Key_{key}",
                    new Vector2(xMin, yMax - 0.125f), new Vector2(xMin + 0.22f, yMax));

                var color = key == "확인" ? UIFactory.AccentColor
                          : key == "←" ? UIFactory.WarnColor : UIFactory.ButtonColor;
                UIFactory.TextButton(holder, "Btn", key, color, 24, () => OnKeypad(key));
            }

            // 닫기 버튼
            var closeHolder = UIFactory.Rect(box.transform, "CloseHolder",
                new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.1f));
            UIFactory.TextButton(closeHolder, "Close", "닫기", UIFactory.SlotColor, 20, CloseOverlay);
        }

        /// <summary>
        /// 키패드 입력 처리: 숫자 추가 / 지우기 / 비밀번호 확인.
        /// </summary>
        /// <param name="key">눌린 키 문자열</param>
        private void OnKeypad(string key)
        {
            switch (key)
            {
                case "←":
                    if (_passwordInput.Length > 0)
                        _passwordInput = _passwordInput.Substring(0, _passwordInput.Length - 1);
                    break;

                case "확인":
                    if (_passwordInput == DataManager.Config.AdminPassword)
                    {
                        LogManager.Write("Info", "관리자 모드 진입");
                        ShowAdminMenu();
                        return;
                    }
                    // 비밀번호 불일치: 입력 초기화로 피드백
                    _passwordInput = "";
                    _passwordDisplay.text = "다시 입력하세요";
                    return;

                default:
                    if (_passwordInput.Length < 8) _passwordInput += key;
                    break;
            }
            // 입력 내용은 ● 로 마스킹해 표시한다
            _passwordDisplay.text = new string('●', _passwordInput.Length);
        }

        // ==================== 관리자 메뉴 ====================

        /// <summary>관리자 기능 메뉴를 띄운다. (작업계획서 22장 기능 목록)</summary>
        private void ShowAdminMenu()
        {
            CloseOverlay();

            var dim = UIFactory.Panel(_canvas.transform, "AdminMenu",
                Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.85f));
            _overlay = dim.gameObject;
            _overlay.transform.SetAsLastSibling();

            var box = UIFactory.Panel(dim.transform, "Box",
                new Vector2(0.25f, 0.1f), new Vector2(0.75f, 0.9f), UIFactory.PanelColor);

            var title = UIFactory.Label(box.transform, "Title",
                $"관리자 메뉴  (버전 {DataManager.Config.Version})", 26);
            SetAnchors(title, 0.05f, 0.9f, 0.95f, 0.98f);
            title.fontStyle = FontStyle.Bold;

            // ----- 에피소드 선택 (1~5) -----
            var epLabel = UIFactory.Label(box.transform, "EpLabel", "에피소드 선택", 18, TextAnchor.MiddleLeft);
            SetAnchors(epLabel, 0.07f, 0.82f, 0.93f, 0.88f);
            for (int i = 1; i <= DataManager.EpisodeCount; i++)
            {
                int ep = i;   // 클로저 캡처용 지역 변수
                float xMin = 0.07f + (i - 1) * 0.175f;
                var holder = UIFactory.Rect(box.transform, $"Ep{i}",
                    new Vector2(xMin, 0.73f), new Vector2(xMin + 0.15f, 0.81f));
                UIFactory.TextButton(holder, "Btn", $"Ep {i}", UIFactory.ButtonColor, 20, () =>
                {
                    CloseOverlay();
                    UIManager.Instance.SetEpisodeVisible(true);
                    GameManager.Instance.HideStory();
                    EpisodeManager.Instance.StartEpisode(ep);
                });
            }

            // ----- 기능 버튼 목록 (2열 배치) -----
            AddMenuButton(box.transform, 0, "JSON 다시 읽기", () =>
            {
                DataManager.LoadAll();
                // 현재 에피소드가 있으면 새 데이터로 다시 시작한다
                if (EpisodeManager.Instance.CurrentEpisodeId > 0)
                    EpisodeManager.Instance.StartEpisode(EpisodeManager.Instance.CurrentEpisodeId);
                UIManager.Instance.ShowPopup("완료", "JSON 데이터를 다시 읽었습니다.");
            });

            AddMenuButton(box.transform, 1, "점수 확인", () =>
            {
                string msg = "";
                for (int i = 1; i <= DataManager.EpisodeCount; i++)
                    msg += $"Episode {i}: {ScoreManager.GetScore(i)}점\n";
                msg += $"\n총점: {ScoreManager.TotalScore}점";
                UIManager.Instance.ShowPopup("점수 확인", msg);
            });

            AddMenuButton(box.transform, 2, "로그 확인", ShowLogViewer);

            AddMenuButton(box.transform, 3, "사운드 테스트", () =>
            {
                AudioManager.Instance.PlaySfx("Success");
                UIManager.Instance.ShowPopup("사운드 테스트",
                    DataManager.Config.UseSfx
                        ? "효과음 'Success' 재생을 요청했습니다.\n(리소스가 없으면 소리가 나지 않습니다)"
                        : "Config 에서 효과음이 꺼져 있습니다.");
            });

            AddMenuButton(box.transform, 4, "애니메이션 테스트", () =>
            {
                UIManager.Instance.ShowPopup("애니메이션 테스트",
                    DataManager.Config.UseAnimation
                        ? "애니메이션이 켜져 있습니다.\n에피소드 실행 시 연출이 재생됩니다."
                        : "Config 에서 애니메이션이 꺼져 있습니다.\n실행 결과가 즉시 표시됩니다.");
            });

            AddMenuButton(box.transform, 5, "전체 점수 초기화", () =>
            {
                ScoreManager.ResetAll();
                UIManager.Instance.ShowPopup("완료", "모든 점수를 초기화했습니다.");
            });

            AddMenuButton(box.transform, 6, "전체 초기화 (처음으로)", () =>
            {
                CloseOverlay();
                GameManager.Instance.ResetToIntro();
            });

            AddMenuButton(box.transform, 7, "닫기", CloseOverlay);
        }

        /// <summary>
        /// 관리자 메뉴 버튼 1개를 2열 그리드에 배치하는 도우미.
        /// </summary>
        /// <param name="parent">메뉴 박스</param>
        /// <param name="index">버튼 순번 (0부터, 왼쪽→오른쪽, 위→아래)</param>
        /// <param name="label">버튼 문구</param>
        /// <param name="onClick">클릭 동작</param>
        private void AddMenuButton(Transform parent, int index, string label, UnityEngine.Events.UnityAction onClick)
        {
            int row = index / 2;
            int col = index % 2;
            float xMin = 0.07f + col * 0.45f;
            float yMax = 0.68f - row * 0.16f;

            var holder = UIFactory.Rect(parent, $"Menu_{label}",
                new Vector2(xMin, yMax - 0.13f), new Vector2(xMin + 0.41f, yMax));
            var color = label == "닫기" ? UIFactory.SlotColor
                      : label.Contains("초기화") ? UIFactory.WarnColor : UIFactory.ButtonColor;
            UIFactory.TextButton(holder, "Btn", label, color, 20, onClick);
        }

        // ==================== 로그 확인 화면 ====================

        /// <summary>최근 로그를 스크롤 목록으로 보여준다.</summary>
        private void ShowLogViewer()
        {
            CloseOverlay();

            var dim = UIFactory.Panel(_canvas.transform, "LogViewer",
                Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.9f));
            _overlay = dim.gameObject;
            _overlay.transform.SetAsLastSibling();

            var title = UIFactory.Label(dim.transform, "Title",
                $"최근 로그 (파일: {LogManager.TodayLogFilePath})", 20);
            SetAnchors(title, 0.05f, 0.92f, 0.95f, 0.98f);

            // 로그 스크롤 목록 (최신이 위로 오도록 역순 추가)
            var content = UIFactory.ScrollList(dim.transform, "LogList",
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.9f));
            var logs = LogManager.RecentLogs;
            for (int i = logs.Count - 1; i >= 0; i--)
            {
                var line = UIFactory.Label(content, $"Log{i}", logs[i], 14, TextAnchor.MiddleLeft);
                var layout = line.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 24;
            }

            var backHolder = UIFactory.Rect(dim.transform, "BackHolder",
                new Vector2(0.4f, 0.02f), new Vector2(0.6f, 0.1f));
            UIFactory.TextButton(backHolder, "Back", "돌아가기", UIFactory.ButtonColor, 20, ShowAdminMenu);
        }

        /// <summary>현재 관리자 오버레이를 닫는다.</summary>
        private void CloseOverlay()
        {
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
        }

        /// <summary>텍스트 요소의 앵커를 간단히 설정하는 도우미.</summary>
        private static void SetAnchors(Text text, float xMin, float yMin, float xMax, float yMax)
        {
            var rt = (RectTransform)text.transform;
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
        }
    }
}
