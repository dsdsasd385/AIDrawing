using System;
using System.IO;
using CarDrawing.Core;
using CarDrawing.Generation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.UI
{
    /// <summary>
    /// 스타일 선택 화면(StylePanel). 계획서 4·8장: 스타일 프리셋을 골라 생성을 시작한다.
    /// UI 시스템에 속하며, UI는 씬에 미리 배치한 구조를 사용한다(런타임 생성 아님).
    ///
    /// 씬 구조(각 슬롯 = 스타일 1개, styleGroups에 순서대로 연결):
    ///   VerticalGroup            ← styleGroups[i], Styles.json의 i번째 스타일과 대응
    ///   ├── ExampleImg (Image)   ← 예시 이미지 (경로는 Styles.json의 thumbnail, StreamingAssets 기준)
    ///   └── Btn (Button)
    ///       └── (Text)           ← 라벨, Texts.json의 "style.<id>" 문구
    ///
    /// 선택 즉시 StyleChosen을 발생시키고 AppFlowManager가 받아 생성을 시작한다.
    /// </summary>
    public class StylePanelController : MonoBehaviour
    {
        /// <summary>관람객이 스타일을 선택했을 때 (선택 즉시 생성 시작)</summary>
        public event Action<StylePreset> StyleChosen;

        /// <summary>스타일 슬롯(VerticalGroup)들. Styles.json 순서대로 인스펙터에서 연결한다.
        /// 슬롯 수보다 스타일이 적으면 남는 슬롯은 자동으로 숨긴다 (v1 실사 1종 → 나머지 비표시)</summary>
        [SerializeField] private Transform[] styleGroups;

        /// <summary>제목 텍스트 (선택, TMP). 연결되면 Texts.json "style.title"로 채운다</summary>
        [SerializeField] private TMP_Text title;

        /// <summary>그린 그림 미리보기 (선택). 연결되면 스케치를 표시한다</summary>
        [SerializeField] private RawImage preview;

        // 자식 오브젝트를 이름으로 찾을 때 쓰는 규약 이름
        private const string ExampleImageName = "ExampleImg";

        // 바인딩은 Awake가 아니라 Start에서 한다. StylePanel은 비활성으로 시작하므로
        // 첫 활성화 시 Awake→Start 순서로 실행되는데, TMP 텍스트는 그 컴포넌트의 Awake가 끝난 뒤(=Start 시점)
        // 설정해야 값이 유지된다 (Awake에서 넣으면 TMP 초기화가 직렬화값 "선택"으로 덮어씀).
        private void Start()
        {
            if (title != null) title.text = TextLibrary.Get("style.title");
            BindStyleGroups();
        }

        /// <summary>
        /// 미리보기에 표시할 스케치를 설정한다 (그리기 완료 시 AppFlowManager가 호출).
        /// 미리보기 슬롯이 없으면 아무 것도 하지 않는다.
        /// </summary>
        /// <param name="sketch">색 레이어 텍스처</param>
        public void SetPreview(Texture sketch)
        {
            if (preview != null) preview.texture = sketch;
        }

        // 슬롯을 스타일에 순서대로 대응시킨다. 각 슬롯에서 예시 이미지·버튼·라벨을 이름으로 찾아 채운다.
        private void BindStyleGroups()
        {
            if (styleGroups == null || styleGroups.Length == 0)
            {
                LogManager.Warn("[StylePanel] styleGroups가 비어 있음 — 인스펙터에서 VerticalGroup들을 연결할 것");
                return;
            }

            var styles = StyleLibrary.Styles;
            for (int i = 0; i < styleGroups.Length; i++)
            {
                Transform slot = styleGroups[i];
                if (slot == null) continue;

                // 대응할 스타일이 없는 슬롯은 숨긴다 (스타일 수 < 슬롯 수)
                bool hasStyle = i < styles.Count;
                slot.gameObject.SetActive(hasStyle);
                if (hasStyle) BindSlot(slot, styles[i]);
            }
        }

        private void BindSlot(Transform slot, StylePreset style)
        {
            // 예시 이미지: 이름으로 찾아 Styles.json 경로의 그림을 넣는다.
            // 파일이 없으면 씬에 배치해 둔 임시 이미지를 그대로 둔다 (예외로 죽지 않기)
            Transform imgTr = slot.Find(ExampleImageName);
            Image exampleImage = imgTr != null ? imgTr.GetComponent<Image>() : null;
            if (exampleImage != null)
            {
                Sprite sprite = LoadExampleSprite(style.thumbnail);
                if (sprite != null)
                {
                    exampleImage.sprite = sprite;
                    // 씬의 임시 이미지는 색이 검정일 수 있어 스프라이트가 검게 물든다 — 흰색으로 되돌린다
                    exampleImage.color = Color.white;
                }
            }
            else
            {
                LogManager.Warn($"[StylePanel] '{slot.name}'에서 {ExampleImageName}(Image)를 못 찾음");
            }

            // 버튼: 슬롯 하위의 Button을 찾아 선택 이벤트를 건다
            Button button = slot.GetComponentInChildren<Button>(true);
            if (button != null)
            {
                StylePreset captured = style; // 클로저 캡처 (루프 변수 직접 캡처 방지)
                button.onClick.AddListener(() => StyleChosen?.Invoke(captured));

                // 버튼 라벨: Texts.json "style.<id>" 문구로 채운다 (하드코딩 금지)
                SetLabel(button.transform, TextLibrary.Get("style." + style.id), slot.name);
            }
            else
            {
                LogManager.Warn($"[StylePanel] '{slot.name}'에서 Button을 못 찾음");
            }
        }

        // 버튼 라벨을 설정한다. 씬은 TMP를 쓰지만, 레거시 Text로 만든 슬롯도 동작하도록 둘 다 지원한다.
        private static void SetLabel(Transform buttonTransform, string text, string slotName)
        {
            TMP_Text tmp = buttonTransform.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) { tmp.text = text; return; }

            Text legacy = buttonTransform.GetComponentInChildren<Text>(true);
            if (legacy != null) { legacy.text = text; return; }

            LogManager.Warn($"[StylePanel] '{slotName}' 버튼에서 라벨(Text/TMP)을 못 찾음");
        }

        // StreamingAssets 기준 상대 경로의 PNG를 스프라이트로 읽는다. 실패 시 null (호출부가 임시 이미지 유지)
        private static Sprite LoadExampleSprite(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            try
            {
                string full = Path.Combine(Application.streamingAssetsPath, relativePath);
                if (!File.Exists(full))
                {
                    LogManager.Warn($"[StylePanel] 예시 이미지 없음: {full}");
                    return null;
                }

                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(full)); // 크기는 LoadImage가 자동 조정
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            catch (Exception e)
            {
                LogManager.Error($"[StylePanel] 예시 이미지 로드 실패({relativePath}): {e.Message}");
                return null;
            }
        }
    }
}
