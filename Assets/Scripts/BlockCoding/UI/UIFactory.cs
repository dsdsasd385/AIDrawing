using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EarthCoding.UI
{
    /// <summary>
    /// 코드 기반 UI 생성 도우미. (작업계획서 8장: 리소스 제작 미포함 대응)
    /// 아직 아트 리소스가 없으므로 모든 UI(패널/텍스트/버튼/드롭다운)를
    /// 단색 이미지 + 텍스트로 코드에서 직접 생성한다.
    /// 추후 리소스가 제작되면 이 클래스가 만든 Image 의 sprite 만 교체하면 된다.
    /// 프로그램의 모든 화면(UIManager, 블록, 에피소드 결과 화면)이 이 클래스를 사용한다.
    /// </summary>
    public static class UIFactory
    {
        /// <summary>한글 표시용 공유 폰트 (맑은 고딕 → 실패 시 Unity 내장 폰트)</summary>
        private static Font _font;

        /// <summary>
        /// 한글이 표시되는 동적 폰트를 반환한다. 최초 호출 시 OS 폰트(맑은 고딕)를 만들고,
        /// 실패하면 Unity 내장 폰트로 대체한다(오류 대응).
        /// </summary>
        public static Font KoreanFont
        {
            get
            {
                if (_font != null) return _font;
                // 전시 PC 는 Windows 이므로 맑은 고딕을 기본으로 사용한다
                _font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 24);
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        // ---------- 공통 색상 팔레트 (리소스 적용 전 임시 테마) ----------

        /// <summary>화면 배경색 (짙은 남색 - 우주 느낌)</summary>
        public static readonly Color BgDark = new Color(0.10f, 0.13f, 0.22f);
        /// <summary>패널 배경색</summary>
        public static readonly Color PanelColor = new Color(0.16f, 0.20f, 0.32f);
        /// <summary>밝은 패널/슬롯 색</summary>
        public static readonly Color SlotColor = new Color(0.22f, 0.27f, 0.40f);
        /// <summary>기본 버튼 색</summary>
        public static readonly Color ButtonColor = new Color(0.25f, 0.55f, 0.85f);
        /// <summary>강조(실행) 버튼 색</summary>
        public static readonly Color AccentColor = new Color(0.15f, 0.70f, 0.45f);
        /// <summary>경고(초기화) 버튼 색</summary>
        public static readonly Color WarnColor = new Color(0.85f, 0.45f, 0.25f);
        /// <summary>기본 글자 색</summary>
        public static readonly Color TextColor = new Color(0.95f, 0.96f, 1f);

        /// <summary>
        /// 자식 RectTransform 을 만들고 앵커/오프셋을 설정한다. 모든 UI 요소의 기반.
        /// </summary>
        /// <param name="parent">부모 트랜스폼</param>
        /// <param name="name">오브젝트 이름</param>
        /// <param name="anchorMin">앵커 최소 (0~1)</param>
        /// <param name="anchorMax">앵커 최대 (0~1)</param>
        /// <returns>생성된 RectTransform</returns>
        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>
        /// 단색 배경 패널을 생성한다.
        /// </summary>
        /// <param name="parent">부모</param>
        /// <param name="name">이름</param>
        /// <param name="anchorMin">앵커 최소</param>
        /// <param name="anchorMax">앵커 최대</param>
        /// <param name="color">배경색</param>
        /// <returns>패널의 Image (rectTransform 으로 위치 조정 가능)</returns>
        public static Image Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>
        /// 텍스트 요소를 생성한다.
        /// </summary>
        /// <param name="parent">부모</param>
        /// <param name="name">이름</param>
        /// <param name="content">표시할 문자열</param>
        /// <param name="fontSize">글자 크기</param>
        /// <param name="anchor">정렬</param>
        /// <returns>생성된 Text 컴포넌트</returns>
        public static Text Label(Transform parent, string name, string content, int fontSize,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = KoreanFont;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = TextColor;
            // 글자가 영역보다 길어도 잘리지 않고 보이도록 설정
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// 클릭 가능한 버튼을 생성한다. (배경 Image + 자식 Text)
        /// </summary>
        /// <param name="parent">부모</param>
        /// <param name="name">이름</param>
        /// <param name="label">버튼에 표시할 문구</param>
        /// <param name="color">버튼 배경색</param>
        /// <param name="fontSize">글자 크기</param>
        /// <param name="onClick">클릭 시 실행할 동작</param>
        /// <returns>생성된 Button 컴포넌트</returns>
        public static Button TextButton(Transform parent, string name, string label, Color color,
            int fontSize, UnityAction onClick)
        {
            var img = Panel(parent, name, Vector2.zero, Vector2.one, color);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            // 눌렀을 때 살짝 어두워지는 기본 색상 트랜지션
            var colors = btn.colors;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);

            var text = Label(img.transform, "Label", label, fontSize);
            text.raycastTarget = false;   // 텍스트가 클릭을 가로채지 않도록
            return btn;
        }

        /// <summary>
        /// 드롭다운(선택 상자)을 생성한다. Episode 3/4 에서 재난/대응/횟수 선택에 사용한다.
        /// </summary>
        /// <param name="parent">부모</param>
        /// <param name="name">이름</param>
        /// <param name="options">선택지 문자열 배열</param>
        /// <param name="fontSize">글자 크기</param>
        /// <returns>생성된 Dropdown 컴포넌트</returns>
        public static Dropdown DropdownBox(Transform parent, string name, string[] options, int fontSize)
        {
            // 본체 배경
            var img = Panel(parent, name, Vector2.zero, Vector2.one, Color.white);
            var dd = img.gameObject.AddComponent<Dropdown>();
            dd.targetGraphic = img;

            // 현재 선택값 표시 텍스트
            var caption = Label(img.transform, "Caption", "", fontSize);
            caption.color = Color.black;
            caption.raycastTarget = false;
            dd.captionText = caption;

            // ----- 펼침 목록(Template) 구성: uGUI Dropdown 필수 구조 -----
            var template = Panel(img.transform, "Template", new Vector2(0, 0), new Vector2(1, 0), Color.white);
            var templateRt = template.rectTransform;
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.sizeDelta = new Vector2(0, 150);   // 펼침 목록 높이
            template.gameObject.AddComponent<ScrollRect>();

            var viewport = Panel(template.transform, "Viewport", Vector2.zero, Vector2.one, Color.white);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var content = Rect(viewport.transform, "Content", new Vector2(0, 1), new Vector2(1, 1));
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0, 30);

            // 항목 1개의 원형(Item) - Dropdown 이 복제해서 목록을 만든다
            var item = Rect(content, "Item", new Vector2(0, 0.5f), new Vector2(1, 0.5f));
            item.sizeDelta = new Vector2(0, 30);
            var toggle = item.gameObject.AddComponent<Toggle>();

            var itemBg = Panel(item, "Item Background", Vector2.zero, Vector2.one, Color.white);
            var itemCheck = Panel(item, "Item Checkmark", new Vector2(0, 0), new Vector2(0.1f, 1), new Color(0.6f, 0.85f, 1f));
            var itemLabel = Label(item, "Item Label", "", fontSize);
            itemLabel.color = Color.black;
            itemLabel.alignment = TextAnchor.MiddleLeft;
            ((RectTransform)itemLabel.transform).offsetMin = new Vector2(20, 0);

            toggle.targetGraphic = itemBg;
            toggle.graphic = itemCheck;

            var scroll = template.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;

            dd.template = templateRt;
            dd.itemText = itemLabel;
            template.gameObject.SetActive(false);   // Template 은 평소 숨김

            // 선택지 채우기
            dd.options.Clear();
            foreach (var opt in options)
                dd.options.Add(new Dropdown.OptionData(opt));
            dd.RefreshShownValue();
            return dd;
        }

        /// <summary>
        /// 세로 스크롤 목록을 생성한다. 왼쪽 블록 팔레트와 로그 화면에서 사용한다.
        /// </summary>
        /// <param name="parent">부모</param>
        /// <param name="name">이름</param>
        /// <param name="anchorMin">앵커 최소</param>
        /// <param name="anchorMax">앵커 최대</param>
        /// <returns>항목을 추가할 Content 트랜스폼</returns>
        public static RectTransform ScrollList(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var bg = Panel(parent, name, anchorMin, anchorMax, PanelColor);
            var scroll = bg.gameObject.AddComponent<ScrollRect>();

            var viewport = Panel(bg.transform, "Viewport", Vector2.zero, Vector2.one, Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();   // 영역 밖 항목 잘라내기

            var content = Rect(viewport.transform, "Content", new Vector2(0, 1), new Vector2(1, 1));
            content.pivot = new Vector2(0.5f, 1f);

            // 항목이 위에서 아래로 쌓이는 세로 레이아웃
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 10;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            // 항목 수에 따라 Content 높이 자동 조절
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }
    }
}
