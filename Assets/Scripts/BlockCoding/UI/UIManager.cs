using System;
using UnityEngine;
using UnityEngine.UI;
using EarthCoding.Blocks;
using EarthCoding.Core;
using EarthCoding.Data;

namespace EarthCoding.UI
{
    /// <summary>
    /// 공통 UI 매니저. (작업계획서 4장 '공통 UI 구성' 대응)
    /// 모든 에피소드가 공유하는 5분할 레이아웃을 코드로 생성한다.
    ///   상단: 제목/설명 | 왼쪽: 블록 선택 | 가운데: 블록 조립 |
    ///   오른쪽: 실시간 결과 | 하단: 초기화/힌트/실행/다음
    /// 에피소드 컨트롤러(EpisodeBase)는 이 클래스가 만든 영역에 내용만 채운다.
    /// GameManager 가 생성하고 소유한다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        /// <summary>전역 접근용 싱글턴</summary>
        public static UIManager Instance { get; private set; }

        // ---------- 외부(에피소드)에서 접근하는 영역/컴포넌트 ----------

        /// <summary>최상단 캔버스 (팝업/드래그 잔상 기준)</summary>
        public Canvas RootCanvas { get; private set; }

        /// <summary>상단 제목 텍스트</summary>
        public Text TitleText { get; private set; }

        /// <summary>상단 설명 텍스트</summary>
        public Text DescriptionText { get; private set; }

        /// <summary>왼쪽 블록 팔레트 목록 콘텐츠 (PaletteBlockView 가 추가되는 곳)</summary>
        public RectTransform PaletteContent { get; private set; }

        /// <summary>가운데 블록 조립 패널</summary>
        public BlockAssemblyPanel Assembly { get; private set; }

        /// <summary>오른쪽 실시간 결과 영역 (에피소드가 자유롭게 구성)</summary>
        public RectTransform ResultArea { get; private set; }

        /// <summary>오른쪽 하단 점수 표시 텍스트</summary>
        public Text ScoreText { get; private set; }

        /// <summary>오른쪽 상태 표시 텍스트 (실행 중/성공/실패 등)</summary>
        public Text StatusText { get; private set; }

        // ---------- 하단 버튼 이벤트 (GameManager/에피소드가 구독) ----------

        /// <summary>초기화 버튼 클릭</summary>
        public event Action OnResetClicked;
        /// <summary>힌트 버튼 클릭</summary>
        public event Action OnHintClicked;
        /// <summary>실행 버튼 클릭</summary>
        public event Action OnRunClicked;
        /// <summary>다음 버튼 클릭</summary>
        public event Action OnNextClicked;

        /// <summary>실행 버튼 (실행 중 비활성화용)</summary>
        private Button _runButton;

        /// <summary>다음 버튼</summary>
        private Button _nextButton;

        /// <summary>현재 떠 있는 팝업 (중복 방지)</summary>
        private GameObject _popup;

        /// <summary>에피소드 화면 전체 루트 (스토리 화면 동안 숨기기 위함)</summary>
        private GameObject _episodeRoot;

        /// <summary>
        /// 전체 UI 를 생성한다. GameManager 초기화 시 1회 호출.
        /// </summary>
        /// <param name="canvas">GameManager 가 만든 루트 캔버스</param>
        public void Build(Canvas canvas)
        {
            Instance = this;
            RootCanvas = canvas;

            // 화면 전체 배경
            var bg = UIFactory.Panel(canvas.transform, "Background", Vector2.zero, Vector2.one, UIFactory.BgDark);
            bg.raycastTarget = false;

            // 에피소드 화면 루트 (스토리/관리자 화면과 분리)
            _episodeRoot = UIFactory.Rect(canvas.transform, "EpisodeRoot", Vector2.zero, Vector2.one).gameObject;
            var root = _episodeRoot.transform;

            BuildTopArea(root);
            BuildLeftPalette(root);
            BuildCenterAssembly(root);
            BuildRightResult(root);
            BuildBottomButtons(root);
        }

        /// <summary>상단 영역: 에피소드 제목 + 설명 (작업계획서 4장)</summary>
        private void BuildTopArea(Transform root)
        {
            var top = UIFactory.Panel(root, "TopArea", new Vector2(0, 0.88f), new Vector2(1, 1), UIFactory.PanelColor);

            TitleText = UIFactory.Label(top.transform, "Title", "", 36, TextAnchor.MiddleCenter);
            var titleRt = (RectTransform)TitleText.transform;
            titleRt.anchorMin = new Vector2(0, 0.45f);
            titleRt.anchorMax = new Vector2(1, 1);
            TitleText.fontStyle = FontStyle.Bold;

            DescriptionText = UIFactory.Label(top.transform, "Description", "", 20, TextAnchor.MiddleCenter);
            var descRt = (RectTransform)DescriptionText.transform;
            descRt.anchorMin = new Vector2(0, 0);
            descRt.anchorMax = new Vector2(1, 0.45f);
            DescriptionText.color = new Color(1, 1, 1, 0.85f);
        }

        /// <summary>왼쪽 영역: 블록 선택 팔레트 (스크롤 목록)</summary>
        private void BuildLeftPalette(Transform root)
        {
            // 팔레트 제목
            var header = UIFactory.Panel(root, "PaletteHeader",
                new Vector2(0.005f, 0.82f), new Vector2(0.21f, 0.87f), UIFactory.SlotColor);
            UIFactory.Label(header.transform, "Text", "블록 선택", 20);

            // 블록 견본이 쌓이는 스크롤 목록
            PaletteContent = UIFactory.ScrollList(root, "PaletteList",
                new Vector2(0.005f, 0.13f), new Vector2(0.21f, 0.82f));
        }

        /// <summary>가운데 영역: 블록 조립 패널 ([시작]~[종료] 세로 목록)</summary>
        private void BuildCenterAssembly(Transform root)
        {
            var header = UIFactory.Panel(root, "AssemblyHeader",
                new Vector2(0.215f, 0.82f), new Vector2(0.60f, 0.87f), UIFactory.SlotColor);
            UIFactory.Label(header.transform, "Text", "블록 조립 (왼쪽 블록을 끌어다 놓으세요)", 20);

            // 조립 영역 배경 = 드롭 판정 영역
            var content = UIFactory.ScrollList(root, "AssemblyList",
                new Vector2(0.215f, 0.13f), new Vector2(0.60f, 0.82f));
            var dropArea = (RectTransform)content.parent.parent;   // ScrollList 의 배경 패널

            Assembly = dropArea.gameObject.AddComponent<BlockAssemblyPanel>();
            Assembly.Initialize(RootCanvas, dropArea, content);
        }

        /// <summary>오른쪽 영역: 실시간 결과(애니메이션) + 점수 + 상태 표시</summary>
        private void BuildRightResult(Transform root)
        {
            var header = UIFactory.Panel(root, "ResultHeader",
                new Vector2(0.605f, 0.82f), new Vector2(0.995f, 0.87f), UIFactory.SlotColor);
            UIFactory.Label(header.transform, "Text", "실시간 결과", 20);

            // 에피소드별 애니메이션이 그려지는 자유 영역
            var resultBg = UIFactory.Panel(root, "ResultArea",
                new Vector2(0.605f, 0.28f), new Vector2(0.995f, 0.82f), UIFactory.PanelColor);
            ResultArea = resultBg.rectTransform;

            // 상태 표시 (실행 중 / 성공 / 실패)
            var statusBg = UIFactory.Panel(root, "StatusBar",
                new Vector2(0.605f, 0.205f), new Vector2(0.995f, 0.275f), UIFactory.SlotColor);
            StatusText = UIFactory.Label(statusBg.transform, "Status", "블록을 조립하고 실행을 눌러보세요!", 18);

            // 점수 표시
            var scoreBg = UIFactory.Panel(root, "ScoreBar",
                new Vector2(0.605f, 0.13f), new Vector2(0.995f, 0.20f), UIFactory.SlotColor);
            ScoreText = UIFactory.Label(scoreBg.transform, "Score", "점수: -", 22);
            ScoreText.fontStyle = FontStyle.Bold;
        }

        /// <summary>하단 영역: 초기화 / 힌트 / 실행 / 다음 버튼 (작업계획서 4장)</summary>
        private void BuildBottomButtons(Transform root)
        {
            var bottom = UIFactory.Panel(root, "BottomArea", new Vector2(0, 0), new Vector2(1, 0.12f), UIFactory.PanelColor);
            var t = bottom.transform;

            // 각 버튼은 하단 바를 4등분한 위치에 배치한다
            MakeBottomButton(t, "초기화", UIFactory.WarnColor, 0.03f, 0.23f, () => OnResetClicked?.Invoke());
            MakeBottomButton(t, "힌트", UIFactory.ButtonColor, 0.27f, 0.47f, () => OnHintClicked?.Invoke());
            _runButton = MakeBottomButton(t, "실행", UIFactory.AccentColor, 0.53f, 0.73f, () => OnRunClicked?.Invoke());
            _nextButton = MakeBottomButton(t, "다음", UIFactory.ButtonColor, 0.77f, 0.97f, () => OnNextClicked?.Invoke());
        }

        /// <summary>하단 버튼 1개를 생성하는 도우미</summary>
        private Button MakeBottomButton(Transform parent, string label, Color color,
            float xMin, float xMax, UnityEngine.Events.UnityAction onClick)
        {
            var holder = UIFactory.Rect(parent, $"Btn_{label}", new Vector2(xMin, 0.15f), new Vector2(xMax, 0.85f));
            return UIFactory.TextButton(holder, "Button", label, color, 26, onClick);
        }

        // ---------- 에피소드 전환/실행 상태 지원 기능 ----------

        /// <summary>
        /// 에피소드 데이터로 상단 제목/설명을 갱신하고 조립 영역과 결과 영역을 비운다.
        /// EpisodeManager 가 에피소드를 시작할 때 호출한다.
        /// </summary>
        /// <param name="data">표시할 에피소드 데이터</param>
        public void SetupEpisodeUI(EpisodeData data)
        {
            TitleText.text = $"Episode {data.EpisodeId}. {data.EpisodeName}";
            DescriptionText.text = data.Description;
            StatusText.text = "블록을 조립하고 실행을 눌러보세요!";
            ScoreText.text = "점수: -";
            Assembly.Clear();
            ClearPalette();
            ClearResultArea();
        }

        /// <summary>왼쪽 팔레트의 모든 블록 견본을 제거한다.</summary>
        public void ClearPalette()
        {
            foreach (Transform child in PaletteContent)
                Destroy(child.gameObject);
        }

        /// <summary>오른쪽 결과 영역의 에피소드별 UI 를 모두 제거한다.</summary>
        public void ClearResultArea()
        {
            foreach (Transform child in ResultArea)
                Destroy(child.gameObject);
        }

        /// <summary>
        /// 팔레트에 블록 견본 1개를 추가한다.
        /// </summary>
        /// <param name="entry">블록 정의</param>
        public void AddPaletteBlock(BlockEntry entry)
        {
            var go = new GameObject($"Palette_{entry.Id}", typeof(RectTransform));
            go.transform.SetParent(PaletteContent, false);
            go.AddComponent<PaletteBlockView>().Setup(entry, Assembly);
        }

        /// <summary>
        /// 실행 중 실행/다음 버튼을 잠근다. (중복 실행 방지)
        /// </summary>
        /// <param name="interactable">true = 누를 수 있음</param>
        public void SetButtonsInteractable(bool interactable)
        {
            _runButton.interactable = interactable;
            _nextButton.interactable = interactable;
        }

        /// <summary>에피소드 화면 전체를 보이거나 숨긴다. (스토리 화면 전환용)</summary>
        /// <param name="visible">true = 표시</param>
        public void SetEpisodeVisible(bool visible) => _episodeRoot.SetActive(visible);

        /// <summary>
        /// 확인 버튼이 있는 팝업 메시지를 띄운다. (힌트/오류/결과 안내 공용)
        /// </summary>
        /// <param name="title">팝업 제목</param>
        /// <param name="message">본문 문구</param>
        public void ShowPopup(string title, string message)
        {
            // 기존 팝업이 있으면 닫고 새로 연다 (중복 방지)
            if (_popup != null) Destroy(_popup);

            // 뒤 클릭을 막는 반투명 딤 배경
            var dim = UIFactory.Panel(RootCanvas.transform, "PopupDim",
                Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.6f));
            _popup = dim.gameObject;

            // 팝업 본체
            var box = UIFactory.Panel(dim.transform, "PopupBox",
                new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f), UIFactory.PanelColor);

            var titleText = UIFactory.Label(box.transform, "Title", title, 30);
            var titleRt = (RectTransform)titleText.transform;
            titleRt.anchorMin = new Vector2(0, 0.75f);
            titleRt.anchorMax = new Vector2(1, 1);
            titleText.fontStyle = FontStyle.Bold;

            var msgText = UIFactory.Label(box.transform, "Message", message, 22);
            var msgRt = (RectTransform)msgText.transform;
            msgRt.anchorMin = new Vector2(0.05f, 0.3f);
            msgRt.anchorMax = new Vector2(0.95f, 0.75f);

            // 확인 버튼: 팝업 닫기
            var btnHolder = UIFactory.Rect(box.transform, "BtnHolder", new Vector2(0.35f, 0.07f), new Vector2(0.65f, 0.24f));
            UIFactory.TextButton(btnHolder, "OkButton", "확인", UIFactory.ButtonColor, 24, ClosePopup);
        }

        /// <summary>현재 팝업을 닫는다.</summary>
        public void ClosePopup()
        {
            if (_popup != null) Destroy(_popup);
            _popup = null;
        }
    }
}
